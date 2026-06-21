using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using Moq;
using Xunit;

namespace HockeyPickup.Api.Tests.ServicesTests;

public partial class SessionServiceTests
{
    private void SetupCreateSessionMocks(bool lotteryEnabled)
    {
        var session = new SessionDetailedResponse
        {
            SessionId = 1,
            SessionDate = DateTime.UtcNow.AddDays(1),
            BuyDayMinimum = 1,
            Cost = 20.00m,
            LotteryEnabled = lotteryEnabled,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };
        _mockSessionRepository.Setup(x => x.CreateSessionAsync(It.IsAny<Session>())).ReturnsAsync(session);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(session);
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync()).ReturnsAsync(new List<UserDetailedResponse>());
        _userManager.Setup(x => x.FindByIdAsync("testUserId")).ReturnsAsync(new AspNetUser { Id = "testUserId", FirstName = "Test", LastName = "User" });
        _configuration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _configuration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("comms");
    }

    [Fact]
    public async Task CreateSession_LotteryEnabled_EnqueuesDraws()
    {
        SetupCreateSessionMocks(lotteryEnabled: true);
        var request = new CreateSessionRequest { SessionDate = DateTime.UtcNow.AddDays(1), RegularSetId = 1, BuyDayMinimum = 1, Cost = 20.00m, LotteryEnabled = true };

        await _sessionService.CreateSession(request);

        _mockLotteryService.Verify(x => x.EnqueueDrawMessagesAsync(It.IsAny<SessionDetailedResponse>()), Times.Once);
    }

    [Fact]
    public async Task CreateSession_LotteryDisabled_DoesNotEnqueue()
    {
        SetupCreateSessionMocks(lotteryEnabled: false);
        var request = new CreateSessionRequest { SessionDate = DateTime.UtcNow.AddDays(1), RegularSetId = 1, BuyDayMinimum = 1, Cost = 20.00m, LotteryEnabled = false };

        await _sessionService.CreateSession(request);

        _mockLotteryService.Verify(x => x.EnqueueDrawMessagesAsync(It.IsAny<SessionDetailedResponse>()), Times.Never);
    }

    private void SetupUpdateSessionMocks(SessionDetailedResponse existing, SessionDetailedResponse updated)
    {
        _mockSessionRepository.Setup(x => x.GetSessionAsync(existing.SessionId)).ReturnsAsync(existing);
        _mockSessionRepository.Setup(x => x.UpdateSessionAsync(It.IsAny<Session>())).ReturnsAsync(updated);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(updated);
    }

    [Fact]
    public async Task UpdateSession_ScheduleChanged_ReEnqueues()
    {
        var date = DateTime.UtcNow.AddDays(5);
        var existing = new SessionDetailedResponse { SessionId = 1, SessionDate = date, BuyDayMinimum = 2, Cost = 20m, LotteryEnabled = true, LotteryEntryWindowMinutes = 30, CreateDateTime = DateTime.UtcNow, UpdateDateTime = DateTime.UtcNow };
        var updated = new SessionDetailedResponse { SessionId = 1, SessionDate = date, BuyDayMinimum = 3, Cost = 20m, LotteryEnabled = true, LotteryEntryWindowMinutes = 30, CreateDateTime = DateTime.UtcNow, UpdateDateTime = DateTime.UtcNow };
        SetupUpdateSessionMocks(existing, updated);
        // BuyDayMinimum changes from 2 -> 3.
        var request = new UpdateSessionRequest { SessionId = 1, SessionDate = date, RegularSetId = 1, BuyDayMinimum = 3, Cost = 20m, LotteryEnabled = true, LotteryEntryWindowMinutes = 30 };

        await _sessionService.UpdateSession(request);

        _mockLotteryService.Verify(x => x.EnqueueDrawMessagesAsync(It.IsAny<SessionDetailedResponse>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSession_NoScheduleChange_DoesNotReEnqueue()
    {
        var date = DateTime.UtcNow.AddDays(5);
        var existing = new SessionDetailedResponse { SessionId = 1, SessionDate = date, BuyDayMinimum = 2, Cost = 20m, LotteryEnabled = true, LotteryEntryWindowMinutes = 30, CreateDateTime = DateTime.UtcNow, UpdateDateTime = DateTime.UtcNow };
        var updated = new SessionDetailedResponse { SessionId = 1, SessionDate = date, BuyDayMinimum = 2, Cost = 25m, LotteryEnabled = true, LotteryEntryWindowMinutes = 30, CreateDateTime = DateTime.UtcNow, UpdateDateTime = DateTime.UtcNow };
        SetupUpdateSessionMocks(existing, updated);
        // Only Cost changes (not a schedule field).
        var request = new UpdateSessionRequest { SessionId = 1, SessionDate = date, RegularSetId = 1, BuyDayMinimum = 2, Cost = 25m, LotteryEnabled = true, LotteryEntryWindowMinutes = 30 };

        await _sessionService.UpdateSession(request);

        _mockLotteryService.Verify(x => x.EnqueueDrawMessagesAsync(It.IsAny<SessionDetailedResponse>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSession_LotteryDisabled_DoesNotReEnqueue()
    {
        var date = DateTime.UtcNow.AddDays(5);
        var existing = new SessionDetailedResponse { SessionId = 1, SessionDate = date, BuyDayMinimum = 2, Cost = 20m, LotteryEnabled = true, LotteryEntryWindowMinutes = 30, CreateDateTime = DateTime.UtcNow, UpdateDateTime = DateTime.UtcNow };
        var updated = new SessionDetailedResponse { SessionId = 1, SessionDate = date, BuyDayMinimum = 2, Cost = 20m, LotteryEnabled = false, LotteryEntryWindowMinutes = 30, CreateDateTime = DateTime.UtcNow, UpdateDateTime = DateTime.UtcNow };
        SetupUpdateSessionMocks(existing, updated);
        // LotteryEnabled flips true -> false (a schedule field changed) but result is disabled, so no enqueue.
        var request = new UpdateSessionRequest { SessionId = 1, SessionDate = date, RegularSetId = 1, BuyDayMinimum = 2, Cost = 20m, LotteryEnabled = false, LotteryEntryWindowMinutes = 30 };

        await _sessionService.UpdateSession(request);

        _mockLotteryService.Verify(x => x.EnqueueDrawMessagesAsync(It.IsAny<SessionDetailedResponse>()), Times.Never);
    }
}
