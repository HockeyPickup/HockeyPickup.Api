using System.ComponentModel;
using System.Security.Claims;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Models.Responses;
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

    public UsersController(IUserRepository userRepository, ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
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
    [Description("Returns a specific user by their ID")]
    [Produces(typeof(UserDetailedResponse))]
    [ProducesResponseType(typeof(UserDetailedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDetailedResponse>> GetUserById(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest(new { message = "User ID cannot be empty" });

            var user = await _userRepository.GetUserAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user with ID: {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving user" });
        }
    }
}
