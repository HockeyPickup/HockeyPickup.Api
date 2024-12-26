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
public class RegularController : ControllerBase
{
    private readonly IRegularService _regularService;

    public RegularController(IRegularService regularService)
    {
        _regularService = regularService;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("duplicate-regular-set")]
    [Description("Duplicates a Regular Set")]
    [Produces(typeof(ApiDataResponse<RegularSetDetailedResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<RegularSetDetailedResponse>>> DuplicateRegularSet([FromBody] DuplicateRegularSetRequest duplicateRegularSetRequest)
    {
        var result = await _regularService.DuplicateRegularSet(duplicateRegularSetRequest);
        var response = ApiDataResponse<RegularSetDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess ? CreatedAtAction(nameof(DuplicateRegularSet), new { id = result.Data.RegularSetId }, response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("update-regular-set")]
    [Description("Updates a Regular Set")]
    [Produces(typeof(ApiDataResponse<RegularSetDetailedResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<RegularSetDetailedResponse>>> UpdateRegularSet([FromBody] UpdateRegularSetRequest updateRegularSetRequest)
    {
        var result = await _regularService.UpdateRegularSet(updateRegularSetRequest);
        var response = ApiDataResponse<RegularSetDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("update-regular-position")]
    [Description("Updates a regular player's position preference")]
    [Produces(typeof(ApiDataResponse<RegularSetDetailedResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<RegularSetDetailedResponse>>> UpdateRegularPosition([FromBody] UpdateRegularPositionRequest request)
    {
        var result = await _regularService.UpdateRegularPosition(request.RegularSetId, request.UserId, request.NewPosition);
        var response = ApiDataResponse<RegularSetDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("update-regular-team")]
    [Description("Updates a regular player's team assignment")]
    [Produces(typeof(ApiDataResponse<RegularSetDetailedResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<RegularSetDetailedResponse>>> UpdateRegularTeam([FromBody] UpdateRegularTeamRequest request)
    {
        var result = await _regularService.UpdateRegularTeam(request.RegularSetId, request.UserId, request.NewTeamAssignment);
        var response = ApiDataResponse<RegularSetDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }
}
