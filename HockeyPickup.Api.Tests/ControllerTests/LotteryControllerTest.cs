using FluentAssertions;
using HockeyPickup.Api.Controllers;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace HockeyPickup.Api.Tests.ControllerTests;

public class LotteryControllerTest
{
    private readonly Mock<ILotteryService> _mockLotteryService = new();
    private readonly LotteryController _controller;

    public LotteryControllerTest()
    {
        _controller = new LotteryController(_mockLotteryService.Object);
    }

    [Fact]
    public async Task ExecuteDue_Success_ReturnsOk()
    {
        _mockLotteryService.Setup(s => s.ExecuteDueAsync()).ReturnsAsync(ServiceResult<int>.CreateSuccess(3));

        var result = await _controller.ExecuteDue();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<int>>().Subject;
        response.Data.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteDue_Failure_ReturnsBadRequest()
    {
        _mockLotteryService.Setup(s => s.ExecuteDueAsync()).ReturnsAsync(ServiceResult<int>.CreateFailure("error"));

        var result = await _controller.ExecuteDue();

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}

public class ServiceKeyAuthorizeAttributeTest : IDisposable
{
    private readonly string? _originalKey = Environment.GetEnvironmentVariable(ServiceKeyAuthorizeAttribute.EnvVarName);

    private static AuthorizationFilterContext CreateContext(string? headerValue)
    {
        var httpContext = new DefaultHttpContext();
        if (headerValue != null)
            httpContext.Request.Headers[ServiceKeyAuthorizeAttribute.HeaderName] = headerValue;

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public void OnAuthorization_EnvVarNotSet_Unauthorized()
    {
        Environment.SetEnvironmentVariable(ServiceKeyAuthorizeAttribute.EnvVarName, null);
        var context = CreateContext("anything");

        new ServiceKeyAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void OnAuthorization_HeaderMissing_Unauthorized()
    {
        Environment.SetEnvironmentVariable(ServiceKeyAuthorizeAttribute.EnvVarName, "secret");
        var context = CreateContext(null);

        new ServiceKeyAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void OnAuthorization_HeaderMismatch_Unauthorized()
    {
        Environment.SetEnvironmentVariable(ServiceKeyAuthorizeAttribute.EnvVarName, "secret");
        var context = CreateContext("wrong");

        new ServiceKeyAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void OnAuthorization_HeaderMatches_Authorized()
    {
        Environment.SetEnvironmentVariable(ServiceKeyAuthorizeAttribute.EnvVarName, "secret");
        var context = CreateContext("secret");

        new ServiceKeyAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeNull();
    }

    public void Dispose() => Environment.SetEnvironmentVariable(ServiceKeyAuthorizeAttribute.EnvVarName, _originalKey);
}
