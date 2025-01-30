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
public class BuySellController : ControllerBase
{
    private readonly IBuySellService _BuySellService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BuySellController(IBuySellService BuySellService, IHttpContextAccessor httpContextAccessor)
    {
        _BuySellService = BuySellService;
        _httpContextAccessor = httpContextAccessor;
    }

    [Authorize]
    [HttpPost("buy")]
    [Description("Submit a buy request for a session spot")]
    [Produces(typeof(ApiDataResponse<BuySellResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<BuySellResponse>>> Buy([FromBody] BuyRequest request)
    {
        var userId = _httpContextAccessor.GetUserId();
        var result = await _BuySellService.ProcessBuyRequestAsync(userId, request);
        var response = ApiDataResponse<BuySellResponse>.FromServiceResult(result);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetBuySell), new { BuySellId = result.Data.BuySellId }, response)
            : BadRequest(response);
    }

    [Authorize]
    [HttpPost("sell")]
    [Description("Submit a sell request for a session spot")]
    [Produces(typeof(ApiDataResponse<BuySellResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<BuySellResponse>>> Sell([FromBody] SellRequest request)
    {
        var userId = _httpContextAccessor.GetUserId();
        var result = await _BuySellService.ProcessSellRequestAsync(userId, request);
        var response = ApiDataResponse<BuySellResponse>.FromServiceResult(result);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetBuySell), new { BuySellId = result.Data.BuySellId }, response)
            : BadRequest(response);
    }

    [Authorize]
    [HttpPost("{BuySellId}/confirm-payment-sent")]
    [Description("Confirm payment sent for a BuySell")]
    [Produces(typeof(ApiDataResponse<BuySellResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<BuySellResponse>>> ConfirmPaymentSent(int buySellId, PaymentMethodType paymentMethodType)
    {
        var userId = _httpContextAccessor.GetUserId();
        var result = await _BuySellService.ConfirmPaymentSentAsync(userId, buySellId, paymentMethodType);
        var response = ApiDataResponse<BuySellResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize]
    [HttpPost("{BuySellId}/confirm-payment-received")]
    [Description("Confirm payment received for a BuySell")]
    [Produces(typeof(ApiDataResponse<BuySellResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<BuySellResponse>>> ConfirmPaymentReceived(int buySellId)
    {
        var userId = _httpContextAccessor.GetUserId();
        var result = await _BuySellService.ConfirmPaymentReceivedAsync(userId, buySellId);
        var response = ApiDataResponse<BuySellResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize]
    [HttpGet("{BuySellId}")]
    [Description("Gets a specific BuySell by ID")]
    [Produces(typeof(ApiDataResponse<BuySellResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiDataResponse<BuySellResponse>>> GetBuySell(int buySellId)
    {
        var result = await _BuySellService.GetBuySellAsync(buySellId);
        var response = ApiDataResponse<BuySellResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : NotFound(response);
    }

    [Authorize]
    [HttpGet("session/{sessionId}")]
    [Description("Gets all BuySells for a specific session")]
    [Produces(typeof(ApiDataResponse<IEnumerable<BuySellResponse>>))]
    [ProducesResponseType(typeof(ApiDataResponse<IEnumerable<BuySellResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<IEnumerable<BuySellResponse>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiDataResponse<IEnumerable<BuySellResponse>>>> GetSessionBuySells(int sessionId)
    {
        var result = await _BuySellService.GetSessionBuySellsAsync(sessionId);
        var response = ApiDataResponse<IEnumerable<BuySellResponse>>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : NotFound(response);
    }

    [Authorize]
    [HttpPost("{BuySellId}/cancel-buy")]
    [Description("Cancels a Buy for a BuySell")]
    [Produces(typeof(ApiDataResponse<BuySellResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<BuySellResponse>>> CancelBuy(int buySellId)
    {
        var userId = _httpContextAccessor.GetUserId();
        var result = await _BuySellService.CancelBuyAsync(userId, buySellId);
        var response = ApiDataResponse<bool>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize]
    [HttpPost("{BuySellId}/cancel-sell")]
    [Description("Cancels a Sell for a BuySell")]
    [Produces(typeof(ApiDataResponse<BuySellResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<BuySellResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<BuySellResponse>>> CancelSell(int buySellId)
    {
        var userId = _httpContextAccessor.GetUserId();
        var result = await _BuySellService.CancelSellAsync(userId, buySellId);
        var response = ApiDataResponse<bool>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }
}
