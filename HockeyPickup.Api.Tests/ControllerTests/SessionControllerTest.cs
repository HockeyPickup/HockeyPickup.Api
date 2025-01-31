using HockeyPickup.Api.Controllers;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace HockeyPickup.Api.Tests.ControllerTests;

public class SessionControllerTests
{
    private readonly Mock<ISessionService> _sessionService;
    private readonly Mock<ILogger<AuthController>> _logger;
    private readonly SessionController _controller;

    public SessionControllerTests()
    {
        _sessionService = new Mock<ISessionService>();
        _logger = new Mock<ILogger<AuthController>>();
        _controller = new SessionController(_sessionService.Object);
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
            CurrentRosters = new List<Models.Responses.RosterPlayer>()
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
            NewPosition = (PositionPreference) 1
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
            NewPosition = (PositionPreference) 1
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
            NewTeamAssignment = (TeamAssignment) 1
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
            NewTeamAssignment = (TeamAssignment) 1
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

    [Fact]
    public async Task CreateSession_Success_ReturnsCreatedResponse()
    {
        // Arrange
        var request = new CreateSessionRequest
        {
            SessionDate = DateTime.UtcNow.AddDays(1),
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m,
            Note = "Test session"
        };

        var serviceResponse = new SessionDetailedResponse
        {
            SessionId = 1,
            SessionDate = request.SessionDate,
            RegularSetId = request.RegularSetId,
            BuyDayMinimum = request.BuyDayMinimum,
            Cost = request.Cost,
            Note = request.Note,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };

        _sessionService.Setup(x => x.CreateSession(It.IsAny<CreateSessionRequest>()))
            .ReturnsAsync(ServiceResult<SessionDetailedResponse>.CreateSuccess(serviceResponse));

        // Act
        var result = await _controller.CreateSession(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<SessionDetailedResponse>>(createdResult.Value);
        Assert.True(response.Success);
        Assert.Equal(serviceResponse.SessionId, response.Data.SessionId);
    }

    [Fact]
    public async Task CreateSession_Failure_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateSessionRequest
        {
            SessionDate = DateTime.UtcNow.AddDays(1),
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m,
            Note = "Test session"
        };

        _sessionService.Setup(x => x.CreateSession(It.IsAny<CreateSessionRequest>()))
            .ReturnsAsync(ServiceResult<SessionDetailedResponse>.CreateFailure("Test error"));

        // Act
        var result = await _controller.CreateSession(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<SessionDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Test error", response.Message);
    }

    [Fact]
    public async Task UpdateSession_Success_ReturnsOkResponse()
    {
        // Arrange
        var request = new UpdateSessionRequest
        {
            SessionId = 1,
            SessionDate = DateTime.UtcNow.AddDays(1),
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m,
            Note = "Updated session"
        };

        var serviceResponse = new SessionDetailedResponse
        {
            SessionId = request.SessionId,
            SessionDate = request.SessionDate,
            RegularSetId = request.RegularSetId,
            BuyDayMinimum = request.BuyDayMinimum,
            Cost = request.Cost,
            Note = request.Note,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };

        _sessionService.Setup(x => x.UpdateSession(It.IsAny<UpdateSessionRequest>()))
            .ReturnsAsync(ServiceResult<SessionDetailedResponse>.CreateSuccess(serviceResponse));

        // Act
        var result = await _controller.UpdateSession(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<SessionDetailedResponse>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(serviceResponse.SessionId, response.Data.SessionId);
    }

    [Fact]
    public async Task UpdateSession_Failure_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateSessionRequest
        {
            SessionId = 1,
            SessionDate = DateTime.UtcNow.AddDays(1),
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m,
            Note = "Updated session"
        };

        _sessionService.Setup(x => x.UpdateSession(It.IsAny<UpdateSessionRequest>()))
            .ReturnsAsync(ServiceResult<SessionDetailedResponse>.CreateFailure("Test error"));

        // Act
        var result = await _controller.UpdateSession(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<SessionDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Test error", response.Message);
    }

    [Fact]
    public async Task DeleteSession_Success_ReturnsOkResult()
    {
        // Arrange
        var sessionId = 1;
        _sessionService.Setup(s => s.DeleteSessionAsync(sessionId))
            .ReturnsAsync(ServiceResult<bool>.CreateSuccess(true, "Session deleted successfully"));

        // Act
        var result = await _controller.DeleteSession(sessionId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<bool>>(okResult.Value);
        Assert.True(response.Success);
        Assert.True(response.Data);
    }

    [Fact]
    public async Task DeleteSession_NotFound_ReturnsBadRequest()
    {
        // Arrange
        var sessionId = 1;
        _sessionService.Setup(s => s.DeleteSessionAsync(sessionId))
            .ReturnsAsync(ServiceResult<bool>.CreateFailure("Session not found"));

        // Act
        var result = await _controller.DeleteSession(sessionId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<bool>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Session not found", response.Message);
    }

    [Fact]
    public async Task DeleteSession_Error_ReturnsBadRequest()
    {
        // Arrange
        var sessionId = 1;
        _sessionService.Setup(s => s.DeleteSessionAsync(sessionId))
            .ReturnsAsync(ServiceResult<bool>.CreateFailure("An error occurred"));

        // Act
        var result = await _controller.DeleteSession(sessionId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<bool>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("An error occurred", response.Message);
    }
}
