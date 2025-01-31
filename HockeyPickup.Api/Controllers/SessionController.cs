using System.ComponentModel;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HockeyPickup.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Consumes("application/json")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status406NotAcceptable)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public class SessionController : ControllerBase
{
    private readonly ISessionService _sessionService;

    public SessionController(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("create-session")]
    [Description("Creates a new session")]
    [Produces(typeof(ApiDataResponse<SessionDetailedResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<SessionDetailedResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiDataResponse<SessionDetailedResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<SessionDetailedResponse>>> CreateSession([FromBody] CreateSessionRequest createSessionRequest)
    {
        var result = await _sessionService.CreateSession(createSessionRequest);
        var response = ApiDataResponse<SessionDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess ? CreatedAtAction(nameof(CreateSession), new { id = result.Data.SessionId }, response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("update-session")]
    [Description("Updates an existing session")]
    [Produces(typeof(ApiDataResponse<SessionDetailedResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<SessionDetailedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<SessionDetailedResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<SessionDetailedResponse>>> UpdateSession([FromBody] UpdateSessionRequest updateSessionRequest)
    {
        var result = await _sessionService.UpdateSession(updateSessionRequest);
        var response = ApiDataResponse<SessionDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("update-roster-position")]
    [Description("Updates a roster player position")]
    [Produces(typeof(ApiDataResponse<SessionDetailedResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<SessionDetailedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<SessionDetailedResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<SessionDetailedResponse>>> UpdateRosterPosition([FromBody] UpdateRosterPositionRequest updateRosterPositionRequest)
    {
        var result = await _sessionService.UpdateRosterPosition(updateRosterPositionRequest.SessionId, updateRosterPositionRequest.UserId, (PositionPreference) updateRosterPositionRequest.NewPosition);
        var response = ApiDataResponse<SessionDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("update-roster-team")]
    [Description("Updates a roster player team")]
    [Produces(typeof(ApiDataResponse<SessionDetailedResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<SessionDetailedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<SessionDetailedResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<SessionDetailedResponse>>> UpdateRosterTeam([FromBody] UpdateRosterTeamRequest updateRosterTeamRequest)
    {
        var result = await _sessionService.UpdateRosterTeam(updateRosterTeamRequest.SessionId, updateRosterTeamRequest.UserId, (TeamAssignment) updateRosterTeamRequest.NewTeamAssignment);
        var response = ApiDataResponse<SessionDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("delete-roster-player/{sessionId}/{userId}")]
    [Description("Removes a player from the Session Roster")]
    [Produces(typeof(ApiDataResponse<SessionDetailedResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<SessionDetailedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<SessionDetailedResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<SessionDetailedResponse>>> DeleteRosterPlayer(int sessionId, string userId)
    {
        var result = await _sessionService.DeleteRosterPlayer(sessionId, userId);
        var response = ApiDataResponse<SessionDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("delete-session/{sessionId}")]
    [Description("Deletes an existing session")]
    [Produces(typeof(ApiDataResponse<bool>))]
    [ProducesResponseType(typeof(ApiDataResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<bool>>> DeleteSession(int sessionId)
    {
        var result = await _sessionService.DeleteSessionAsync(sessionId);
        var response = ApiDataResponse<bool>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }
}
