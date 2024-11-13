#pragma warning disable IDE0057 // Use range operator
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HockeyPickup.Api.Controllers;

[ApiController]
[Route("[controller]")]
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
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _userService.ValidateCredentialsAsync(request.Username, request.Password);
            if (result == null)
                return Unauthorized(new { message = "Invalid login attempt." });

            var (user, roles) = result.Value;
            var (token, expiration) = _jwtService.GenerateToken(user.Id, user.Username, roles);

            return new LoginResponse
            {
                Token = token,
                Expiration = expiration
            };
        }
        catch (InvalidOperationException ex) when (ex.Message == "Email not confirmed")
        {
            return BadRequest(new { message = "You must confirm your registration by email." });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

        var (success, errors) = await _userService.RegisterUserAsync(request);

        if (!success)
        {
            return BadRequest(new RegisterResponse
            {
                Success = false,
                Message = "Registration failed",
                Errors = errors
            });
        }

        return Ok(new RegisterResponse
        {
            Success = true,
            Message = errors.Any() ? "Registration successful but there were some warnings" : "Registration successful. Please check your email to confirm your account.",
            Errors = errors
        });
    }
}

#pragma warning restore IDE0057 // Use range operator
