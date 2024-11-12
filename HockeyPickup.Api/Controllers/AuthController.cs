using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HockeyPickup.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly IJwtService _jwtService;
    private readonly IUserService _userService;

    public AuthController(IJwtService jwtService, IUserService userService)
    {
        _jwtService = jwtService;
        _userService = userService;
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
}