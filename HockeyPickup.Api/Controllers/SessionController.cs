using System.ComponentModel;
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
    private readonly ILogger<AuthController> _logger;

    public SessionController(ILogger<AuthController> logger, ISessionService sessionService)
    {
        _logger = logger;
        _sessionService = sessionService;
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
        var result = await _sessionService.UpdateRosterPosition(updateRosterPositionRequest.SessionId, updateRosterPositionRequest.UserId, updateRosterPositionRequest.NewPosition);
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
        var result = await _sessionService.UpdateRosterTeam(updateRosterTeamRequest.SessionId, updateRosterTeamRequest.UserId, updateRosterTeamRequest.NewTeamAssignment);
        var response = ApiDataResponse<SessionDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }
}
