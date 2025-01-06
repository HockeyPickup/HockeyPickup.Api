using Azure.Storage.Blobs;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Calendar = Ical.Net.Calendar;

namespace HockeyPickup.Api.Services;

public interface ICalendarService
{
    Task<ServiceResult<string>> RebuildCalendarAsync();
    ServiceResult<string> GetCalendarUrl();
}

public class CalendarService : ICalendarService
{
    private const string CALENDAR_CONTAINER = "calendars";
    private const string CALENDAR_FILENAME = "hockey_pickup.ics";
    private const string TIMEZONE = "America/Los_Angeles";

    private readonly ISessionRepository _sessionRepository;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(ISessionRepository sessionRepository, BlobServiceClient blobServiceClient, ILogger<CalendarService> logger, IConfiguration configuration)
    {
        _sessionRepository = sessionRepository;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
        _configuration = configuration;
    }

    public ServiceResult<string> GetCalendarUrl()
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(CALENDAR_CONTAINER);
            var blobClient = containerClient.GetBlobClient(CALENDAR_FILENAME);
            return ServiceResult<string>.CreateSuccess(blobClient.Uri.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting calendar URL");
            return ServiceResult<string>.CreateFailure("Error getting calendar URL");
        }
    }

    public async Task<ServiceResult<string>> RebuildCalendarAsync()
    {
        try
        {
            var siteTitle = _configuration["SiteTitle"];
            var rinkLocation = _configuration["RinkLocation"];
            var baseUrl = _configuration["BaseUrl"];

            var sessions = await _sessionRepository.GetBasicSessionsAsync();

            var calendar = new Calendar();
            var now = DateTime.Now;

            calendar.AddProperty("DESCRIPTION", $"{siteTitle} Calendar");
            calendar.AddProperty("X-WR-CALNAME", $"{siteTitle} Calendar");

            // Only include the sessions in the future and within the last year
            var recentSessions = sessions.Where(s => s.SessionDate.Year >= now.AddYears(-1).Year && !(s.Note?.Contains("cancelled", StringComparison.OrdinalIgnoreCase) ?? false)).OrderBy(s => s.SessionDate);

            foreach (var session in recentSessions)
            {
                var url = $"{baseUrl.TrimEnd('/')}/session/{session.SessionId}";
                var uri = new Uri(url);

                var iCalEvent = new CalendarEvent
                {
                    Summary = siteTitle,
                    Description = string.Format("{0}{1}",
                        url,
                        !string.IsNullOrEmpty(session.Note) ?
                            Environment.NewLine + Environment.NewLine + session.Note :
                            string.Empty),
                    DtStart = new CalDateTime(session.SessionDate, TIMEZONE),
                    DtEnd = new CalDateTime(session.SessionDate.AddHours(1), TIMEZONE),
                    Url = uri,
                    Location = rinkLocation
                };

                calendar.Events.Add(iCalEvent);
            }

            var serializer = new CalendarSerializer();
            var calendarContent = serializer.SerializeToString(calendar);

            var containerClient = _blobServiceClient.GetBlobContainerClient(CALENDAR_CONTAINER);

            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync();

            // Upload the calendar file
            var blobClient = containerClient.GetBlobClient(CALENDAR_FILENAME);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(calendarContent));
            await blobClient.UploadAsync(stream, overwrite: true);

            return ServiceResult<string>.CreateSuccess(blobClient.Uri.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding calendar");
            return ServiceResult<string>.CreateFailure("Error rebuilding calendar");
        }
    }
}
