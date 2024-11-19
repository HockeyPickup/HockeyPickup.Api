using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using KeyNotFoundException = GreenDonut.KeyNotFoundException;

namespace HockeyPickup.Api.Helpers;

[ExcludeFromCodeCoverage]
public static class ValidationSetup
{
    public static IMvcBuilder ConfigureValidation(this IMvcBuilder builder)
    {
        builder.ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors.Select(error => new ErrorDetail
                    {
                        Code = "VALIDATION_ERROR",
                        Field = x.Key,
                        Message = error.ErrorMessage ?? error.Exception?.Message ?? "Unknown validation error"
                    }))
                    .ToList();

                var apiResponse = new ApiDataResponse<object>
                {
                    Success = false,
                    Message = "Validation failed",
                    Errors = errors
                };

                return new BadRequestObjectResult(apiResponse);
            };
        });

        return builder;
    }
}

[ExcludeFromCodeCoverage]
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";

        var response = new ApiDataResponse<object>
        {
            Success = false,
            Message = "An error occurred processing your request",
            Errors = new List<ErrorDetail>
            {
                new()
                {
                    Code = "INTERNAL_ERROR",
                    Message = ex is ApplicationException ? ex.Message : "An unexpected error occurred"
                }
            }
        };

        context.Response.StatusCode = ex switch
        {
            ApplicationException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}
