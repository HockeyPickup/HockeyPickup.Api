using System.ComponentModel;
using System.Security.Claims;
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
public class ImpersonationController : ControllerBase
{
    private readonly IImpersonationService _impersonationService;

    public ImpersonationController(IImpersonationService impersonationService)
    {
        _impersonationService = impersonationService;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("impersonate")]
    [Description("Start impersonating another user")]
    [Produces(typeof(ApiDataResponse<ImpersonationResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<ImpersonationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<ImpersonationResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiDataResponse<ImpersonationResponse>>> ImpersonateUser([FromBody] ImpersonationRequest request)
    {
        var result = await _impersonationService.ImpersonateUserAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, request.TargetUserId);
        var response = ApiDataResponse<ImpersonationResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize]
    [HttpPost("revert")]
    [Description("Revert an active impersonation session")]
    [Produces(typeof(ApiDataResponse<RevertImpersonationResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<RevertImpersonationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<RevertImpersonationResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<RevertImpersonationResponse>>> RevertImpersonation()
    {
        var result = await _impersonationService.RevertImpersonationAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, User);
        var response = ApiDataResponse<RevertImpersonationResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize]
    [HttpGet("status")]
    [Description("Get current impersonation status")]
    [Produces(typeof(ApiDataResponse<ImpersonationStatusResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<ImpersonationStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiDataResponse<ImpersonationStatusResponse>>> GetStatus()
    {
        var result = await _impersonationService.GetStatusAsync(User);
        return Ok(ApiDataResponse<ImpersonationStatusResponse>.FromServiceResult(result));
    }
}
