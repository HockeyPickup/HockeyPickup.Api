using System.ComponentModel;
using HockeyPickup.Api.Helpers;
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
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendarService;
    private readonly ILogger<CalendarController> _logger;

    public CalendarController(ICalendarService calendarService, ILogger<CalendarController> logger)
    {
        _calendarService = calendarService;
        _logger = logger;
    }

    [HttpPost("rebuild")]
    [Authorize(Roles = "Admin")]
    [Description("Regenerates the calendar .ics file")]
    [Produces(typeof(ApiDataResponse<string>))]
    [ProducesResponseType(typeof(ApiDataResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiDataResponse<string>>> RebuildCalendar()
    {
        var result = await _calendarService.RebuildCalendarAsync();
        var response = ApiDataResponse<string>.FromServiceResult(result);
        return result.IsSuccess ? Ok(response) : BadRequest(response);
    }

    [HttpGet]
    [Authorize]
    [Description("Gets the current calendar .ics file URL")]
    [Produces(typeof(ApiDataResponse<string>))]
    [ProducesResponseType(typeof(ApiDataResponse<string>), StatusCodes.Status200OK)]
    public ActionResult<ApiDataResponse<string>> GetCalendarUrl()
    {
        var result = _calendarService.GetCalendarUrl();
        var response = ApiDataResponse<string>.FromServiceResult(result);
        return Ok(response);
    }
}
