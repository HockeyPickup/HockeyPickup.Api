using HockeyPickup.Api.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using FluentAssertions;

namespace HockeyPickup.Api.Tests.HelperTests;

public class ValidationSetupTests
{
    [Fact]
    public Task InvalidModelState_SetsFieldInErrorDetails()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers()
            .ConfigureValidation();

        var app = builder.Build();

        // Creating a test request with a model that will fail validation
        var context = new DefaultHttpContext();
        context.Request.Path = "/test";
        context.Request.Method = "POST";

        // Create a controller context with invalid model state
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("email", "Invalid email format");  // This field name should appear in the response

        var actionContext = new ActionContext(
            context,
            new RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor(),
            modelState);

        // Act
        var factory = app.Services
            .GetRequiredService<IOptions<ApiBehaviorOptions>>()
            .Value
            .InvalidModelStateResponseFactory;

        var result = factory(actionContext) as BadRequestObjectResult;

        // Assert
        result.Should().NotBeNull();
        var response = result!.Value as ApiDataResponse<object>;
        response.Should().NotBeNull();
        response!.Errors.Should().ContainSingle();
        response.Errors[0].Field.Should().Be("email");
        response.Errors[0].Code.Should().Be("VALIDATION_ERROR");
        response.Errors[0].Message.Should().Be("Invalid email format");
        return Task.CompletedTask;
    }
}
