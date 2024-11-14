using System.ComponentModel;
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

    [HttpGet]
    [Description("Returns list of users - basic info for regular users, detailed for admins")]
    [Produces(typeof(IEnumerable<UserBasicResponse>))]
    [ProducesResponseType(typeof(IEnumerable<UserBasicResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<UserDetailedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<object>>> GetUsers()
    {
        try
        {
            // Check if user is admin
            if (User.IsInRole("Admin"))
            {
                return Ok(await _userRepository.GetDetailedUsersAsync());
            }
            return Ok(await _userRepository.GetBasicUsersAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new { message = "An error occurred while retrieving users" });
        }
    }
}
