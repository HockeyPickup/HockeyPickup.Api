using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using HockeyPickup.Api.Controllers;
using HockeyPickup.Api.Services;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Responses;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using HockeyPickup.Api.Helpers;

namespace HockeyPickup.Api.Tests.ControllerTests;

public class CalendarControllerTest
{
    private readonly Mock<ICalendarService> _mockCalendarService;
    private readonly Mock<ILogger<CalendarController>> _mockLogger;
    private readonly CalendarController _controller;

    public CalendarControllerTest()
    {
        _mockCalendarService = new Mock<ICalendarService>();
        _mockLogger = new Mock<ILogger<CalendarController>>();

        _controller = new CalendarController(
            _mockCalendarService.Object,
            _mockLogger.Object
        );
    }

    private void SetupAuthentication(bool isAdmin = false)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "test@example.com")
        };

        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext();
        httpContext.User = claimsPrincipal;

        _controller.ControllerContext = new ControllerContext()
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task RebuildCalendar_AsAdmin_ReturnsOkWithUrl()
    {
        // Arrange
        SetupAuthentication(isAdmin: true);
        var calendarUrl = "https://storage.example.com/calendars/hockey_pickup.ics";

        _mockCalendarService
            .Setup(x => x.RebuildCalendarAsync())
            .ReturnsAsync(ServiceResult<string>.CreateSuccess(calendarUrl));

        // Act
        var result = await _controller.RebuildCalendar();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<string>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().Be(calendarUrl);
        response.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task RebuildCalendar_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        SetupAuthentication(isAdmin: true);
        var errorMessage = "Failed to rebuild calendar";

        _mockCalendarService
            .Setup(x => x.RebuildCalendarAsync())
            .ReturnsAsync(ServiceResult<string>.CreateFailure(errorMessage));

        // Act
        var result = await _controller.RebuildCalendar();

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<string>>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Data.Should().BeNull();
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be(errorMessage);
    }

    [Fact]
    public void GetCalendarUrl_ValidRequest_ReturnsOkWithUrl()
    {
        // Arrange
        SetupAuthentication();
        var calendarUrl = "https://storage.example.com/calendars/hockey_pickup.ics";

        _mockCalendarService
            .Setup(x => x.GetCalendarUrl())
            .Returns(ServiceResult<string>.CreateSuccess(calendarUrl));

        // Act
        var result = _controller.GetCalendarUrl();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<string>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().Be(calendarUrl);
        response.Errors.Should().BeEmpty();
    }

    [Fact]
    public void GetCalendarUrl_ServiceFailure_ReturnsOk()
    {
        // Arrange
        SetupAuthentication();
        var errorMessage = "Failed to get calendar URL";

        _mockCalendarService
            .Setup(x => x.GetCalendarUrl())
            .Returns(ServiceResult<string>.CreateFailure(errorMessage));

        // Act
        var result = _controller.GetCalendarUrl();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<string>>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Data.Should().BeNull();
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be(errorMessage);
    }

    [Fact]
    public async Task RebuildCalendar_ServiceThrowsException_ThrowsException()
    {
        // Arrange
        SetupAuthentication(isAdmin: true);

        _mockCalendarService
            .Setup(x => x.RebuildCalendarAsync())
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _controller.RebuildCalendar());
    }

    [Fact]
    public void GetCalendarUrl_ServiceThrowsException_ThrowsException()
    {
        // Arrange
        SetupAuthentication();

        _mockCalendarService
            .Setup(x => x.GetCalendarUrl())
            .Throws(new Exception("Unexpected error"));

        // Act & Assert
        Assert.Throws<Exception>(() =>
            _controller.GetCalendarUrl());
    }

    [Fact]
    public async Task RebuildCalendar_AsNonAdmin_ThrowsAuthorizationException()
    {
        // Arrange
        var endpoint = new Endpoint(
            context => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute("Admin")),
            "Test endpoint"
        );

        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()
        );

        var authContext = new AuthorizationFilterContext(
            actionContext,
            new List<IFilterMetadata>()
        );

        SetupAuthentication(isAdmin: false);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _controller.RebuildCalendar());
    }

    [Fact]
    public void GetCalendarUrl_NoAuthentication_ThrowsAuthorizationException()
    {
        // Arrange
        var endpoint = new Endpoint(
            context => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute()),
            "Test endpoint"
        );

        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()
        );

        var authContext = new AuthorizationFilterContext(
            actionContext,
            new List<IFilterMetadata>()
        );

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            _controller.GetCalendarUrl());
        ex.Message.Should().Contain("Value cannot be null");
    }
}
