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

    [Authorize(Roles = "Admin")]
    [HttpDelete("delete-regular-set/{regularSetId}")]
    [Description("Deletes a Regular Set if it's not in use")]
    [Produces(typeof(ApiResponse))]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse>> DeleteRegularSet(int regularSetId)
    {
        var result = await _regularService.DeleteRegularSet(regularSetId);
        var response = ApiResponse.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("add-regular")]
    [Description("Adds a player to a Regular Set")]
    [Produces(typeof(ApiDataResponse<RegularSetDetailedResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<RegularSetDetailedResponse>>> AddRegular([FromBody] AddRegularRequest request)
    {
        var result = await _regularService.AddRegular(request);
        var response = ApiDataResponse<RegularSetDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess ? CreatedAtAction(nameof(AddRegular), new { id = result.Data.RegularSetId }, response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("delete-regular/{regularSetId}/{userId}")]
    [Description("Removes a player from a Regular Set")]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<RegularSetDetailedResponse>>> DeleteRegular(int regularSetId, string userId)
    {
        var request = new DeleteRegularRequest { RegularSetId = regularSetId, UserId = userId };
        var result = await _regularService.DeleteRegular(request);
        var response = ApiDataResponse<RegularSetDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("new-regular-set")]
    [Description("Creates a new Regular Set")]
    [Produces(typeof(ApiDataResponse<RegularSetDetailedResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiDataResponse<RegularSetDetailedResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<RegularSetDetailedResponse>>> CreateRegularSet([FromBody] CreateRegularSetRequest request)
    {
        var result = await _regularService.CreateRegularSet(request);
        var response = ApiDataResponse<RegularSetDetailedResponse>.FromServiceResult(result);
        return result.IsSuccess
            ? CreatedAtAction(nameof(CreateRegularSet), new { id = result.Data.RegularSetId }, response)
            : BadRequest(response);
    }
}
