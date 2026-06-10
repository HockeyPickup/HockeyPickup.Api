using System.ComponentModel;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HockeyPickup.Api.Controllers;

[ApiController]
[Route("lottery")]
[Consumes("application/json")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status406NotAcceptable)]
public class LotteryController : ControllerBase
{
    private readonly ILotteryService _lotteryService;

    public LotteryController(ILotteryService lotteryService)
    {
        _lotteryService = lotteryService;
    }

    [ServiceKeyAuthorize]
    [HttpPost("execute-due")]
    [Description("Safety-net sweep that runs any due or stuck lottery draws. Idempotent and safe to invoke at any time.")]
    [Produces(typeof(ApiDataResponse<int>))]
    [ProducesResponseType(typeof(ApiDataResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<int>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<int>>> ExecuteDue()
    {
        var result = await _lotteryService.ExecuteDueAsync();
        var response = ApiDataResponse<int>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }
}
