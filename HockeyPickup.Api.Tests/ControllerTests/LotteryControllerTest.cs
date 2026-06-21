using FluentAssertions;
using HockeyPickup.Api.Controllers;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly string? _originalKey = Environment.GetEnvironmentVariable(ServiceKeyAuthorizeAttribute.ConfigKeyName);

    // headerValue: the X-Service-Key header to send (null = no header).
    // configKey: when set, attaches an IConfiguration via RequestServices holding LotteryServiceApiKey (the prod path);
    //            when null, RequestServices is left unset so the attribute falls back to the environment variable.
    private static AuthorizationFilterContext CreateContext(string? headerValue, string? configKey = null)
    {
        var httpContext = new DefaultHttpContext();
        if (headerValue != null)
            httpContext.Request.Headers[ServiceKeyAuthorizeAttribute.HeaderName] = headerValue;

        if (configKey != null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [ServiceKeyAuthorizeAttribute.ConfigKeyName] = configKey })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            httpContext.RequestServices = services.BuildServiceProvider();
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public void OnAuthorization_KeyNotConfigured_Unauthorized()
    {
        Environment.SetEnvironmentVariable(ServiceKeyAuthorizeAttribute.ConfigKeyName, null);
        var context = CreateContext("anything");

        new ServiceKeyAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void OnAuthorization_HeaderMissing_Unauthorized()
    {
        Environment.SetEnvironmentVariable(ServiceKeyAuthorizeAttribute.ConfigKeyName, "secret");
        var context = CreateContext(null);

        new ServiceKeyAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void OnAuthorization_HeaderMismatch_Unauthorized()
    {
        Environment.SetEnvironmentVariable(ServiceKeyAuthorizeAttribute.ConfigKeyName, "secret");
        var context = CreateContext("wrong");

        new ServiceKeyAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void OnAuthorization_HeaderMatchesEnvVar_Authorized()
    {
        Environment.SetEnvironmentVariable(ServiceKeyAuthorizeAttribute.ConfigKeyName, "secret");
        var context = CreateContext("secret");

        new ServiceKeyAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void OnAuthorization_HeaderMatchesConfiguration_Authorized()
    {
        // No environment variable - the key resolves from IConfiguration (the production/local-appsettings path).
        Environment.SetEnvironmentVariable(ServiceKeyAuthorizeAttribute.ConfigKeyName, null);
        var context = CreateContext("configsecret", configKey: "configsecret");

        new ServiceKeyAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeNull();
    }

    public void Dispose() => Environment.SetEnvironmentVariable(ServiceKeyAuthorizeAttribute.ConfigKeyName, _originalKey);
}
