#pragma warning disable IDE0057 // Use range operator
using System.ComponentModel;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;
using ForgotPasswordRequest = HockeyPickup.Api.Models.Requests.ForgotPasswordRequest;
using LoginRequest = HockeyPickup.Api.Models.Requests.LoginRequest;
using RegisterRequest = HockeyPickup.Api.Models.Requests.RegisterRequest;
using ResetPasswordRequest = HockeyPickup.Api.Models.Requests.ResetPasswordRequest;

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
    [Produces(typeof(LoginResponse))]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request data" });

        var result = await _userService.ValidateCredentialsAsync(request.UserName, request.Password);
        if (!result.IsSuccess)
            return Unauthorized(new { message = result.Message });

        var (user, roles) = result.Data;
        var (token, expiration) = _jwtService.GenerateToken(user.Id, user.UserName, roles);

        return new LoginResponse
        {
            Token = token,
            Expiration = expiration,
            UserBasicResponse = user.ToUserBasicResponse()
        };
    }

    [Authorize]
    [HttpPost("logout")]
    [Description("Invalidates the current JWT token")]
    [Produces(typeof(object))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var token = HttpContext.GetBearerToken();
        if (token == null)
        {
            _logger.LogWarning("No bearer token found during logout attempt");
            return BadRequest(new { message = "No token found" });
        }

        if (await _tokenBlacklist.IsTokenBlacklistedAsync(token))
        {
            return BadRequest(new { message = "Token already invalidated" });
        }

        await _tokenBlacklist.InvalidateTokenAsync(token);

        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("register")]
    [Description("Registers a new user account")]
    [Produces(typeof(RegisterResponse))]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new RegisterResponse
            {
                Success = false,
                Message = "Invalid registration data",
                Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))
            });
        }

        var result = await _userService.RegisterUserAsync(request);

        return result.IsSuccess
            ? Ok(new RegisterResponse
            {
                Success = true,
                Message = result.Message ?? "Registration successful. Please check your email to confirm your account.",
                Errors = Enumerable.Empty<string>()
            })
            : BadRequest(new RegisterResponse
            {
                Success = false,
                Message = "Registration failed",
                Errors = new[] { result.Message }
            });
    }

    [HttpPost("confirm-email")]
    [Description("Confirms user's email address using token from email")]
    [Produces(typeof(ConfirmEmailResponse))]
    [ProducesResponseType(typeof(ConfirmEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ConfirmEmailResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ConfirmEmailResponse
            {
                Success = false,
                Message = "Invalid request data"
            });
        }

        var token = WebUtility.UrlDecode(request.Token);
        var result = await _userService.ConfirmEmailAsync(request.Email, token);

        return result.IsSuccess
            ? Ok(new ConfirmEmailResponse
            {
                Success = true,
                Message = result.Message
            })
            : BadRequest(new ConfirmEmailResponse
            {
                Success = false,
                Message = result.Message
            });
    }

    [Authorize]
    [HttpPost("change-password")]
    [Description("Changes password for authenticated user")]
    [Produces(typeof(object))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid Request Data" });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return NotFound(new { message = "User not found" });

        var result = await _userService.ChangePasswordAsync(userId, request);
        if (!result.IsSuccess)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = "Password changed successfully" });
    }

    [HttpPost("forgot-password")]
    [Description("Initiates password reset process by sending email with reset token")]
    [Produces(typeof(object))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request data" });

        // Call service but ignore failure for security
        await _userService.InitiateForgotPasswordAsync(request.Email, request.FrontendUrl);

        // Always return success to prevent email enumeration
        return Ok(new { message = "If the email exists, a password reset link will be sent" });
    }

    [HttpPost("reset-password")]
    [Description("Resets password using token from email")]
    [Produces(typeof(object))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request data" });

        request.Token = WebUtility.UrlDecode(request.Token);
        var result = await _userService.ResetPasswordAsync(request);

        if (!result.IsSuccess)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = "Password has been reset successfully" });
    }

    [Authorize]
    [HttpPost("save-user")]
    [Description("Updates user profile information")]
    [Produces(typeof(object))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveUser([FromBody] SaveUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request data" });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return NotFound(new { message = "User not found" });

        var result = await _userService.SaveUserAsync(userId, request);
        if (!result.IsSuccess)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = "User saved successfully" });
    }
}
#pragma warning restore IDE0057 // Use range operator
