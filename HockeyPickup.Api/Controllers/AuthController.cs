#pragma warning disable IDE0057 // Use range operator
using System.ComponentModel;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ForgotPasswordRequest = HockeyPickup.Api.Models.Requests.ForgotPasswordRequest;
using LoginRequest = HockeyPickup.Api.Models.Requests.LoginRequest;
using RegisterRequest = HockeyPickup.Api.Models.Requests.RegisterRequest;
using ResetPasswordRequest = HockeyPickup.Api.Models.Requests.ResetPasswordRequest;
using HockeyPickup.Api.Data.Entities;

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
public class AuthController : ControllerBase
{
    private readonly IJwtService _jwtService;
    private readonly IUserService _userService;
    private readonly ITokenBlacklistService _tokenBlacklist;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IJwtService jwtService, IUserService userService, ITokenBlacklistService tokenBlacklist, ILogger<AuthController> logger)
    {
        _jwtService = jwtService;
        _userService = userService;
        _tokenBlacklist = tokenBlacklist;
        _logger = logger;
    }

    [HttpPost("login")]
    [Description("Authenticates user and returns JWT token")]
    [Produces(typeof(ApiDataResponse<LoginResponse>))]
    [ProducesResponseType(typeof(ApiDataResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<LoginResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiDataResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var result = await _userService.ValidateCredentialsAsync(request.UserName, request.Password);
        if (!result.IsSuccess)
        {
            return Unauthorized(result.ToErrorResponse<LoginResponse>("AUTH_ERROR"));
        }

        var (user, roles) = result.Data;
        var (token, expiration) = _jwtService.GenerateToken(user.Id, user.UserName, roles);

        var loginResponse = new LoginResponse
        {
            Token = token,
            Expiration = expiration,
            UserBasicResponse = user.ToUserBasicResponse()
        };

        return Ok(loginResponse.ToApiDataResponse(result));
    }

    [Authorize]
    [HttpPost("logout")]
    [Description("Invalidates the current JWT token")]
    [Produces(typeof(ApiResponse))]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Logout()
    {
        var token = HttpContext.GetBearerToken();
        if (token == null)
        {
            _logger.LogWarning("No bearer token found during logout attempt");
            return BadRequest(new ApiResponse { Message = "No token found", Success = false });
        }

        if (await _tokenBlacklist.IsTokenBlacklistedAsync(token))
        {
            return BadRequest(new ApiResponse { Message = "Token already invalidated", Success = false });
        }

        await _tokenBlacklist.InvalidateTokenAsync(token);

        return Ok(new ApiResponse { Message = "Logged out successfully", Success = true });
    }

    [HttpPost("register")]
    [Description("Registers a new user account")]
    [Produces(typeof(ApiDataResponse<AspNetUser>))]
    [ProducesResponseType(typeof(ApiDataResponse<AspNetUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiDataResponse<AspNetUser>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiDataResponse<AspNetUser>>> Register([FromBody] RegisterRequest request)
    {
        var result = await _userService.RegisterUserAsync(request);
        var response = ApiDataResponse<AspNetUser>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [HttpPost("confirm-email")]
    [Description("Confirms user's email address using token from email")]
    [Produces(typeof(ApiResponse))]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        var result = await _userService.ConfirmEmailAsync(request.Email, request.Token);
        var response = ApiResponse.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize]
    [HttpPost("change-password")]
    [Description("Changes password for authenticated user")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var result = await _userService.ChangePasswordAsync(userId, request);
        var response = ApiResponse.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [HttpPost("forgot-password")]
    [Description("Initiates password reset process by sending email with reset token")]
    [Produces(typeof(ApiResponse))]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        // Call service but ignore failure for security
        var result = await _userService.InitiateForgotPasswordAsync(request.Email, request.FrontendUrl);
        var response = ApiResponse.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [HttpPost("reset-password")]
    [Description("Resets password using token from email")]
    [Produces(typeof(ApiResponse))]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _userService.ResetPasswordAsync(request);
        var response = ApiResponse.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [Authorize]
    [HttpPost("save-user")]
    [Description("Updates user profile information")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> SaveUser([FromBody] SaveUserRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var result = await _userService.SaveUserAsync(userId, request);
        var response = ApiResponse.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }
}
#pragma warning restore IDE0057 // Use range operator
