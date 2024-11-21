using FluentAssertions;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.GraphQL.Queries;
using HockeyPickup.Api.Models.Responses;
using Moq;

namespace HockeyPickup.Api.Tests.GraphQLTests;

public class SessionQueriesTests
{
    private readonly Mock<ISessionRepository> _mockRepository;
    private readonly SessionQueries _queries;
    private readonly DateTime _testDate = DateTime.UtcNow;

    public SessionQueriesTests()
    {
        _mockRepository = new Mock<ISessionRepository>();
        _queries = new SessionQueries();
    }

    [Fact]
    public async Task GetSessions_CallsRepository()
    {
        // Arrange
        var expectedSessions = new List<SessionBasicResponse>
        {
            new() {
                SessionId = 1,
                Note = "Test Session 1",
                CreateDateTime = _testDate,
                UpdateDateTime = _testDate,
                SessionDate = _testDate.AddDays(7)
            }
        };
        _mockRepository.Setup(r => r.GetBasicSessionsAsync())
            .ReturnsAsync(expectedSessions);

        // Act
        var result = await _queries.GetSessions(_mockRepository.Object);

        // Assert
        result.Should().BeEquivalentTo(expectedSessions);
        _mockRepository.Verify(r => r.GetBasicSessionsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetSession_CallsRepository()
    {
        // Arrange
        var expectedSession = new SessionDetailedResponse
        {
            SessionId = 1,
            Note = "Test Session",
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(7),
            BuySells = new List<BuySellResponse>(),
            ActivityLogs = new List<ActivityLogResponse>()
        };
        _mockRepository.Setup(r => r.GetSessionAsync(1))
            .ReturnsAsync(expectedSession);

        // Act
        var result = await _queries.GetSession(1, _mockRepository.Object);

        // Assert
        result.Should().BeEquivalentTo(expectedSession);
        _mockRepository.Verify(r => r.GetSessionAsync(1), Times.Once);
    }
}
