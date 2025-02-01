using System.ComponentModel;
using System.Security.Claims;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HockeyPickup.Api.Controllers;

[ApiController]
[Authorize]
[Route("[controller]")]
[Consumes("application/json")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status406NotAcceptable)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UsersController> _logger;
    private readonly IUserService _userService;

    public UsersController(IUserRepository userRepository, ILogger<UsersController> logger, IUserService userService)
    {
        _userRepository = userRepository;
        _logger = logger;
        _userService = userService;
    }

    [Authorize]
    [HttpGet]
    [Description("Returns list of users")]
    [Produces(typeof(IEnumerable<UserDetailedResponse>))]
    [ProducesResponseType(typeof(IEnumerable<UserDetailedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<object>>> GetUsers()
    {
        try
        {
            return Ok(await _userRepository.GetDetailedUsersAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new { message = "An error occurred while retrieving users" });
        }
    }

    [Authorize]
    [HttpGet("current")]
    [Description("Returns the user object for the signed in user")]
    [Produces(typeof(UserDetailedResponse))]
    [ProducesResponseType(typeof(UserDetailedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDetailedResponse>> GetUser()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return NotFound(new { message = "User not found" });

            return Ok(await _userRepository.GetUserAsync(userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user");
            return StatusCode(500, new { message = "An error occurred while retrieving user" });
        }
    }

    [Authorize]
    [HttpGet("{userId}")]
    [Description("Returns a specific user by their Id")]
    [Produces(typeof(UserDetailedResponse))]
    [ProducesResponseType(typeof(UserDetailedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDetailedResponse>> GetUserById(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest(new { message = "User Id cannot be empty" });

            var user = await _userRepository.GetUserAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user with Id: {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving user" });
        }
    }

    [Authorize]
    [HttpPost("{userId}/payment-methods")]
    [Description("Adds a new payment method for a user")]
    [Produces(typeof(ApiDataResponse<UserPaymentMethodResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<UserPaymentMethodResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiDataResponse<UserPaymentMethodResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<UserPaymentMethodResponse>>> AddPaymentMethod(string userId, [FromBody] UserPaymentMethodRequest request)
    {
        var result = await _userService.AddUserPaymentMethodAsync(userId, request);
        var response = ApiDataResponse<UserPaymentMethodResponse>.FromServiceResult(result);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPaymentMethod), new { userId, paymentMethodId = result.Data.UserPaymentMethodId }, response)
            : BadRequest(response);
    }

    [Authorize]
    [HttpPut("{userId}/payment-methods/{paymentMethodId}")]
    [Description("Updates an existing payment method")]
    [Produces(typeof(ApiDataResponse<UserPaymentMethodResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<UserPaymentMethodResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<UserPaymentMethodResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<UserPaymentMethodResponse>>> UpdatePaymentMethod(string userId, int paymentMethodId, [FromBody] UserPaymentMethodRequest request)
    {
        var result = await _userService.UpdateUserPaymentMethodAsync(userId, paymentMethodId, request);
        var response = ApiDataResponse<UserPaymentMethodResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize]
    [HttpGet("{userId}/payment-methods")]
    [Description("Gets all payment methods for a user")]
    [Produces(typeof(ApiDataResponse<IEnumerable<UserPaymentMethodResponse>>))]
    [ProducesResponseType(typeof(ApiDataResponse<IEnumerable<UserPaymentMethodResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<IEnumerable<UserPaymentMethodResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<IEnumerable<UserPaymentMethodResponse>>>> GetPaymentMethods(string userId)
    {
        var result = await _userService.GetUserPaymentMethodsAsync(userId);
        var response = ApiDataResponse<IEnumerable<UserPaymentMethodResponse>>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize]
    [HttpGet("{userId}/payment-methods/{paymentMethodId}")]
    [Description("Gets a specific payment method")]
    [Produces(typeof(ApiDataResponse<UserPaymentMethodResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<UserPaymentMethodResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<UserPaymentMethodResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<UserPaymentMethodResponse>>> GetPaymentMethod(string userId, int paymentMethodId)
    {
        var result = await _userService.GetUserPaymentMethodAsync(userId, paymentMethodId);
        var response = ApiDataResponse<UserPaymentMethodResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize]
    [HttpDelete("{userId}/payment-methods/{paymentMethodId}")]
    [Description("Deletes a specific payment method")]
    [Produces(typeof(ApiDataResponse<UserPaymentMethodResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<UserPaymentMethodResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<UserPaymentMethodResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<UserPaymentMethodResponse>>> DeletePaymentMethod(string userId, int paymentMethodId)
    {
        var result = await _userService.DeleteUserPaymentMethodAsync(userId, paymentMethodId);
        var response = ApiDataResponse<UserPaymentMethodResponse>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }
}
