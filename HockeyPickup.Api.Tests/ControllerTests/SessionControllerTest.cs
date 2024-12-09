using HockeyPickup.Api.Controllers;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace HockeyPickup.Api.Tests.Controllers;

public class SessionControllerTests
{
    private readonly Mock<ISessionService> _sessionService;
    private readonly Mock<ILogger<AuthController>> _logger;
    private readonly SessionController _controller;

    public SessionControllerTests()
    {
        _sessionService = new Mock<ISessionService>();
        _logger = new Mock<ILogger<AuthController>>();
        _controller = new SessionController(_logger.Object, _sessionService.Object);
    }

    private static SessionDetailedResponse CreateTestSession()
    {
        return new SessionDetailedResponse
        {
            SessionId = 1,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            SessionDate = DateTime.UtcNow.Date,
            Note = string.Empty,
            CurrentRosters = new List<RosterPlayer>()
        };
    }

    [Fact]
    public async Task UpdateRosterPosition_Success_ReturnsOkResult()
    {
        // Arrange
        var request = new UpdateRosterPositionRequest
        {
            SessionId = 1,
            UserId = "testUser",
            NewPosition = 1
        };
        var serviceResponse = ServiceResult<SessionDetailedResponse>.CreateSuccess(
            CreateTestSession(), "Success");

        _sessionService.Setup(x => x.UpdateRosterPosition(
            request.SessionId, request.UserId, request.NewPosition))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRosterPosition(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<SessionDetailedResponse>>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task UpdateRosterPosition_Failure_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateRosterPositionRequest
        {
            SessionId = 1,
            UserId = "testUser",
            NewPosition = 1
        };
        var serviceResponse = ServiceResult<SessionDetailedResponse>.CreateFailure("Error message");

        _sessionService.Setup(x => x.UpdateRosterPosition(
            request.SessionId, request.UserId, request.NewPosition))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRosterPosition(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<SessionDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Error message", response.Message);
    }

    [Fact]
    public async Task UpdateTeamAssignment_Success_ReturnsOkResult()
    {
        // Arrange
        var request = new UpdateRosterTeamRequest
        {
            SessionId = 1,
            UserId = "testUser",
            NewTeamAssignment = 1
        };
        var serviceResponse = ServiceResult<SessionDetailedResponse>.CreateSuccess(
            CreateTestSession(), "Success");

        _sessionService.Setup(x => x.UpdateRosterTeam(
            request.SessionId, request.UserId, request.NewTeamAssignment))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRosterTeam(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<SessionDetailedResponse>>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task UpdateTeamAssignment_Failure_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateRosterTeamRequest
        {
            SessionId = 1,
            UserId = "testUser",
            NewTeamAssignment = 1
        };
        var serviceResponse = ServiceResult<SessionDetailedResponse>.CreateFailure("Error message");

        _sessionService.Setup(x => x.UpdateRosterTeam(
            request.SessionId, request.UserId, request.NewTeamAssignment))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRosterTeam(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<SessionDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Error message", response.Message);
    }
}
