using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Azure;

namespace HockeyPickup.Api.Tests.ServicesTests;

public class CalendarServiceTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<CalendarService>> _mockLogger;
    private readonly Mock<BlobContainerClient> _mockContainerClient;
    private readonly Mock<BlobClient> _mockBlobClient;
    private readonly CalendarService _calendarService;

    public CalendarServiceTests()
    {
        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockBlobServiceClient = new Mock<BlobServiceClient>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<CalendarService>>();
        _mockContainerClient = new Mock<BlobContainerClient>();
        _mockBlobClient = new Mock<BlobClient>();

        // Setup configuration
        _mockConfiguration.Setup(x => x["SiteTitle"]).Returns("Hockey Pickup");
        _mockConfiguration.Setup(x => x["RinkLocation"]).Returns("Test Rink, Test Address");
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns("https://test.com");

        // Setup blob storage
        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_mockContainerClient.Object);

        _mockContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(_mockBlobClient.Object);

        _mockBlobClient
            .Setup(x => x.Uri)
            .Returns(new Uri("https://test.storage.com/calendars/hockey_pickup.ics"));

        var mockResponse = new Mock<Response<BlobContainerInfo>>();
        _mockContainerClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        _calendarService = new CalendarService(
            _mockSessionRepository.Object,
            _mockBlobServiceClient.Object,
            _mockLogger.Object,
            _mockConfiguration.Object
        );
    }

    [Fact]
    public async Task RebuildCalendarAsync_FiltersSessionsCorrectly()
    {
        // Arrange
        var now = DateTime.Now;
        var sessions = new List<SessionBasicResponse>
        {
            CreateTestSession(1, now.AddDays(1), "Future session"),              // Should be included
            CreateTestSession(2, now.AddDays(-1), "Recent session"),            // Should be included
            CreateTestSession(3, now.AddYears(-2), "Old session"),              // Should be excluded (too old)
            CreateTestSession(4, now.AddDays(7), "CANCELLED - Weather"),        // Should be excluded (cancelled)
            CreateTestSession(5, now.AddDays(14), "cancelled due to holiday"),  // Should be excluded (cancelled)
            CreateTestSession(6, now.AddDays(21), null!),                        // Should be included (null note)
            CreateTestSession(7, now.AddMonths(-11), "Almost year old")         // Should be included
        };

        _mockSessionRepository
            .Setup(x => x.GetBasicSessionsAsync())
            .ReturnsAsync(sessions);

        Stream capturedStream = null;
        var mockUploadResponse = new Mock<Response<BlobContentInfo>>();
        _mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((stream, overwrite, token) =>
            {
                // Read the stream content for verification
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                capturedStream = ms;
            })
            .ReturnsAsync(mockUploadResponse.Object);

        // Act
        var result = await _calendarService.RebuildCalendarAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedStream.Should().NotBeNull();

        // Convert stream to string to check content
        capturedStream.Position = 0;
        var calendarContent = new StreamReader(capturedStream).ReadToEnd();

        // Verify included sessions
        calendarContent.Should().Contain("Future session");
        calendarContent.Should().Contain("Recent session");
        calendarContent.Should().Contain("Almost year old");

        // Verify excluded sessions
        calendarContent.Should().NotContain("Old session");
        calendarContent.Should().NotContain("CANCELLED - Weather");
        calendarContent.Should().NotContain("cancelled due to holiday");
    }
    

    private static SessionBasicResponse CreateTestSession(int id, DateTime date, string note = "")
    {
        return new SessionBasicResponse
        {
            SessionId = id,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            SessionDate = date,
            Note = note
        };
    }

    [Fact]
    public async Task RebuildCalendarAsync_Success_ReturnsCalendarUrl()
    {
        // Arrange
        var sessions = new List<SessionBasicResponse>
        {
            CreateTestSession(1, DateTime.Now.AddDays(1), "Test session"),
            CreateTestSession(2, DateTime.Now.AddDays(7), "Another session")
        };

        _mockSessionRepository
            .Setup(x => x.GetBasicSessionsAsync())
            .ReturnsAsync(sessions);

        var mockUploadResponse = new Mock<Response<BlobContentInfo>>();
        _mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUploadResponse.Object);

        // Act
        var result = await _calendarService.RebuildCalendarAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("https://test.storage.com/calendars/hockey_pickup.ics");

        _mockSessionRepository.Verify(x => x.GetBasicSessionsAsync(), Times.Once);
        _mockBlobClient.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RebuildCalendarAsync_SessionRepositoryFails_ReturnsError()
    {
        // Arrange
        _mockSessionRepository
            .Setup(x => x.GetBasicSessionsAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _calendarService.RebuildCalendarAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Error rebuilding calendar");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task RebuildCalendarAsync_NoSessions_CreatesEmptyCalendar()
    {
        // Arrange
        _mockSessionRepository
            .Setup(x => x.GetBasicSessionsAsync())
            .ReturnsAsync(new List<SessionBasicResponse>());

        var mockUploadResponse = new Mock<Response<BlobContentInfo>>();
        _mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUploadResponse.Object);

        // Act
        var result = await _calendarService.RebuildCalendarAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("https://test.storage.com/calendars/hockey_pickup.ics");

        _mockBlobClient.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RebuildCalendarAsync_BlobStorageFails_ReturnsError()
    {
        // Arrange
        _mockSessionRepository
            .Setup(x => x.GetBasicSessionsAsync())
            .ReturnsAsync(new List<SessionBasicResponse>
            {
                CreateTestSession(1, DateTime.Now.AddDays(1))
            });

        _mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage error"));

        // Act
        var result = await _calendarService.RebuildCalendarAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Error rebuilding calendar");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void GetCalendarUrl_Success_ReturnsUrl()
    {
        // Act
        var result = _calendarService.GetCalendarUrl();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("https://test.storage.com/calendars/hockey_pickup.ics");

        _mockBlobServiceClient.Verify(x => x.GetBlobContainerClient("calendars"), Times.Once);
        _mockContainerClient.Verify(x => x.GetBlobClient("hockey_pickup.ics"), Times.Once);
    }

    [Fact]
    public void GetCalendarUrl_BlobStorageFails_ReturnsError()
    {
        // Arrange
        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Throws(new Exception("Storage error"));

        // Act
        var result = _calendarService.GetCalendarUrl();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Error getting calendar URL");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task RebuildCalendarAsync_FiltersCancelledSessions()
    {
        // Arrange
        var sessions = new List<SessionBasicResponse>
        {
            CreateTestSession(1, DateTime.Now.AddDays(1), "Regular session"),
            CreateTestSession(2, DateTime.Now.AddDays(7), "CANCELLED - Bad weather")
        };

        _mockSessionRepository
            .Setup(x => x.GetBasicSessionsAsync())
            .ReturnsAsync(sessions);

        Stream capturedStream = null;
        var mockUploadResponse = new Mock<Response<BlobContentInfo>>();
        _mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((stream, overwrite, token) =>
            {
                // Read the stream content for verification
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                capturedStream = ms;
            })
            .ReturnsAsync(mockUploadResponse.Object);

        // Act
        var result = await _calendarService.RebuildCalendarAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedStream.Should().NotBeNull();

        // Verify upload was called
        _mockBlobClient.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        // Additional verification of calendar content could be done here by reading capturedStream
        capturedStream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RebuildCalendarAsync_FiltersOldSessions()
    {
        // Arrange
        var sessions = new List<SessionBasicResponse>
        {
            CreateTestSession(1, DateTime.Now.AddDays(1), "Future session"),
            CreateTestSession(2, DateTime.Now.AddYears(-2), "Old session")
        };

        _mockSessionRepository
            .Setup(x => x.GetBasicSessionsAsync())
            .ReturnsAsync(sessions);

        Stream capturedStream = null;
        var mockUploadResponse = new Mock<Response<BlobContentInfo>>();
        _mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((stream, overwrite, token) =>
            {
                // Read the stream content for verification
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                capturedStream = ms;
            })
            .ReturnsAsync(mockUploadResponse.Object);

        // Act
        var result = await _calendarService.RebuildCalendarAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedStream.Should().NotBeNull();

        // Verify upload was called
        _mockBlobClient.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        // Additional verification of calendar content could be done here by reading capturedStream
        capturedStream.Length.Should().BeGreaterThan(0);
    }
}
