using Azure;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace HockeyPickup.Api.Tests.ServicesTests;

public class CalendarServiceGoalieTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepository = new();
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient = new();
    private readonly Mock<BlobContainerClient> _mockContainerClient = new();
    private readonly Mock<BlobClient> _mockBlobClient = new();
    private readonly CalendarService _calendarService;
    private string? _uploadedCalendar;

    public CalendarServiceGoalieTests()
    {
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(x => x["SiteTitle"]).Returns("Hockey Pickup");
        configuration.Setup(x => x["RinkLocation"]).Returns("Test Rink");
        configuration.Setup(x => x["BaseUrl"]).Returns("https://test.com");

        _mockBlobServiceClient.Setup(x => x.GetBlobContainerClient(It.IsAny<string>())).Returns(_mockContainerClient.Object);
        _mockContainerClient.Setup(x => x.GetBlobClient(It.IsAny<string>())).Returns(_mockBlobClient.Object);
        _mockBlobClient.Setup(x => x.Uri).Returns(new Uri("https://test.storage.com/calendars/hockey_pickup.ics"));

        var rawResponse = default(Response);
        _mockContainerClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContainerInfo(default, default), rawResponse));
        _mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((stream, _, _) => _uploadedCalendar = new StreamReader(stream).ReadToEnd())
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContentInfo(default, default, Array.Empty<byte>(), "", 0), rawResponse));

        _calendarService = new CalendarService(_mockSessionRepository.Object, _mockBlobServiceClient.Object, Mock.Of<ILogger<CalendarService>>(), configuration.Object);
    }

    private static SessionGoalie Goalie(string first, string last, DateTime joined) => new()
    {
        UserId = $"{first}-{last}",
        FirstName = first,
        LastName = last,
        IsPlaying = true,
        JoinedDateTime = joined
    };

    private async Task<string> RebuildAndGetDescriptionAsync(SessionBasicResponse session)
    {
        _mockSessionRepository.Setup(x => x.GetBasicSessionsAsync()).ReturnsAsync(new[] { session });

        var result = await _calendarService.RebuildCalendarAsync();

        result.IsSuccess.Should().BeTrue();
        var calendar = Ical.Net.Calendar.Load(_uploadedCalendar!);
        return calendar!.Events.Single().Description!;
    }

    [Fact]
    public async Task RebuildCalendarAsync_GoaliesOnRosterAndEmptyNote_AppendsGoaliesFromRosterInJoinOrder()
    {
        // Arrange
        var joined = DateTime.UtcNow;
        var session = new SessionBasicResponse
        {
            SessionId = 10,
            CreateDateTime = joined,
            UpdateDateTime = joined,
            SessionDate = DateTime.Now.AddDays(3),
            Note = null,
            Goalies = new List<SessionGoalie> { Goalie("Ken", "Ornstein", joined.AddMinutes(5)), Goalie("Ryan", "Novak", joined) }
        };

        // Act
        var description = await RebuildAndGetDescriptionAsync(session);

        // Assert
        description.Should().Be("https://test.com/session/10\n\nGoalies: Ryan Novak, Ken Ornstein");
    }

    [Fact]
    public async Task RebuildCalendarAsync_GoaliesAndNote_AppendsGoaliesAfterNote()
    {
        // Arrange
        var joined = DateTime.UtcNow;
        var session = new SessionBasicResponse
        {
            SessionId = 11,
            CreateDateTime = joined,
            UpdateDateTime = joined,
            SessionDate = DateTime.Now.AddDays(3),
            Note = "8/10",
            Goalies = new List<SessionGoalie> { Goalie("Ryan", "Novak", joined) }
        };

        // Act
        var description = await RebuildAndGetDescriptionAsync(session);

        // Assert
        description.Should().Be("https://test.com/session/11\n\n8/10\n\nGoalies: Ryan Novak");
    }

    [Fact]
    public async Task RebuildCalendarAsync_NoGoalies_DoesNotAppendGoalies()
    {
        // Arrange
        var session = new SessionBasicResponse
        {
            SessionId = 12,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            SessionDate = DateTime.Now.AddDays(3),
            Note = "8/10"
        };

        // Act
        var description = await RebuildAndGetDescriptionAsync(session);

        // Assert
        description.Should().Be("https://test.com/session/12\n\n8/10");
        description.Should().NotContain("Goalies");
    }
}
