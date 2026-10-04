using FluentAssertions;
using HockeyPickup.Api.Controllers;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Moq;

namespace HockeyPickup.Api.Tests.ControllerTests;

public class SessionControllerAddRosterPlayerTests
{
    private readonly Mock<ISessionService> _sessionService = new();
    private readonly SessionController _controller;

    public SessionControllerAddRosterPlayerTests()
    {
        _controller = new SessionController(_sessionService.Object);
    }

    private static AddRosterPlayerRequest Request() => new()
    {
        SessionId = 5,
        UserId = "goalieUser",
        Position = PositionPreference.Goalie,
        TeamAssignment = TeamAssignment.TBD
    };

    [Fact]
    public async Task AddRosterPlayer_Success_ReturnsCreatedAtAction()
    {
        // Arrange
        var request = Request();
        var session = new SessionDetailedResponse
        {
            SessionId = 5,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            SessionDate = DateTime.UtcNow.Date
        };
        _sessionService.Setup(x => x.AddRosterPlayer(request))
            .ReturnsAsync(ServiceResult<SessionDetailedResponse>.CreateSuccess(session, "Ryan Novak added to roster as Goalie"));

        // Act
        var result = await _controller.AddRosterPlayer(request);

        // Assert
        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(SessionController.AddRosterPlayer));
        created.RouteValues!["id"].Should().Be(5);
        var response = created.Value.Should().BeOfType<ApiDataResponse<SessionDetailedResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Ryan Novak added to roster as Goalie");
        response.Data.Should().BeSameAs(session);
    }

    [Fact]
    public async Task AddRosterPlayer_Failure_ReturnsBadRequest()
    {
        // Arrange
        var request = Request();
        _sessionService.Setup(x => x.AddRosterPlayer(request))
            .ReturnsAsync(ServiceResult<SessionDetailedResponse>.CreateFailure("Ryan Novak is already on the roster for this session"));

        // Act
        var result = await _controller.AddRosterPlayer(request);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiDataResponse<SessionDetailedResponse>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Ryan Novak is already on the roster for this session");
    }
}
