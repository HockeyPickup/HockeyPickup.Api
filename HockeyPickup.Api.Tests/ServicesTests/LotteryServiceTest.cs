using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HockeyPickup.Api.Tests.ServicesTests;

public class LotteryServiceTests
{
    private readonly Mock<UserManager<AspNetUser>> _userManager;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<ILotteryRepository> _mockLotteryRepository;
    private readonly Mock<IBuySellService> _mockBuySellService;
    private readonly Mock<IServiceBus> _mockServiceBus;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<LotteryService>> _mockLogger;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly ReverseShuffler _shuffler = new();
    private readonly LotteryService _service;

    // Deterministic shuffler: reverses the list so draw order is the reverse of input order.
    private sealed class ReverseShuffler : IRandomShuffler
    {
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = 0, j = list.Count - 1; i < j; i++, j--)
                (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public LotteryServiceTests()
    {
        var userStore = new Mock<IUserStore<AspNetUser>>();
        _userManager = new Mock<UserManager<AspNetUser>>(
            userStore.Object, Mock.Of<IOptions<IdentityOptions>>(), Mock.Of<IPasswordHasher<AspNetUser>>(),
            Array.Empty<IUserValidator<AspNetUser>>(), Array.Empty<IPasswordValidator<AspNetUser>>(),
            Mock.Of<ILookupNormalizer>(), Mock.Of<IdentityErrorDescriber>(), Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<AspNetUser>>>());

        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockLotteryRepository = new Mock<ILotteryRepository>();
        _mockBuySellService = new Mock<IBuySellService>();
        _mockServiceBus = new Mock<IServiceBus>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<LotteryService>>();
        _mockUserRepository = new Mock<IUserRepository>();

        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("comms");
        _mockConfiguration.Setup(x => x["ServiceBusLotteryQueueName"]).Returns("lottery");
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync()).ReturnsAsync(new List<UserDetailedResponse>());

        _service = new LotteryService(_userManager.Object, _mockSessionRepository.Object, _mockLotteryRepository.Object,
            _mockBuySellService.Object, _mockServiceBus.Object, _mockConfiguration.Object, _mockLogger.Object,
            _shuffler, _mockUserRepository.Object);
    }

    private static AspNetUser CreateUser(string id = "user1") => new()
    {
        Id = id,
        FirstName = "First" + id,
        LastName = "Last" + id,
        Email = id + "@example.com",
        Active = true
    };

    private static SessionDetailedResponse CreateSession(bool lotteryEnabled = true, int buyDayMinimum = 5, int windowMinutes = 30)
    {
        // SessionDate one day out with a large buy-day-minimum so the tier draw times are already in the past.
        var sessionDate = TimeZoneUtils.GetCurrentPacificTime().AddDays(1);
        return new SessionDetailedResponse
        {
            SessionId = 1,
            SessionDate = sessionDate,
            BuyDayMinimum = buyDayMinimum,
            LotteryEntryWindowMinutes = windowMinutes,
            LotteryEnabled = lotteryEnabled,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };
    }

    private static SessionLotteryEntrant CreateEntrant(int id, string userId, LotteryClass lotteryClass = LotteryClass.Standard, LotteryEntrantStatus status = LotteryEntrantStatus.Drawing, int? drawOrder = null)
        => new()
        {
            LotteryEntrantId = id,
            SessionId = 1,
            UserId = userId,
            LotteryClass = lotteryClass,
            Status = status,
            DrawOrder = drawOrder,
            User = CreateUser(userId)
        };

    private static BuySellResponse BuyResponse() => new()
    {
        BuySellId = 1,
        SessionId = 1,
        SessionDate = DateTime.UtcNow,
        PaymentSent = false,
        PaymentReceived = false,
        CreateDateTime = DateTime.UtcNow,
        UpdateDateTime = DateTime.UtcNow,
        TeamAssignment = TeamAssignment.TBD,
        Price = 20.00m
    };

    private void SetupBuySuccess() => _mockBuySellService
        .Setup(x => x.ProcessBuyRequestAsync(It.IsAny<string>(), It.IsAny<BuyRequest>(), true))
        .ReturnsAsync(ServiceResult<BuySellResponse>.CreateSuccess(BuyResponse()));

    private static UserDetailedResponse User(string id, bool active, NotificationPreference pref, string email) => new()
    {
        Id = id,
        Email = email,
        Active = active,
        NotificationPreference = pref,
        FirstName = "First" + id,
        LastName = "Last" + id,
        UserName = id,
        Rating = 1.0m,
        Preferred = false,
        PreferredPlus = false
    };

    // ---------- HandleDrawMessageAsync ----------

    [Fact]
    public async Task HandleDraw_SessionNull_NoOp()
    {
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync((SessionDetailedResponse) null!);

        var result = await _service.HandleDrawMessageAsync(new LotteryDrawMessage { SessionId = 1, LotteryClass = LotteryClass.Standard, ExpectedDrawDateTimePacific = DateTime.Now });

        result.Should().Be(LotteryDrawOutcome.NoOp);
        _mockLotteryRepository.Verify(x => x.ClaimForDrawingAsync(It.IsAny<int>(), It.IsAny<LotteryClass>()), Times.Never);
    }

    [Fact]
    public async Task HandleDraw_LotteryDisabled_NoOp()
    {
        var session = CreateSession(lotteryEnabled: false);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);

        var result = await _service.HandleDrawMessageAsync(new LotteryDrawMessage { SessionId = 1, LotteryClass = LotteryClass.Standard, ExpectedDrawDateTimePacific = session.LotteryDrawStandard });

        result.Should().Be(LotteryDrawOutcome.NoOp);
    }

    [Fact]
    public async Task HandleDraw_DrawTimeMismatch_NoOp()
    {
        var session = CreateSession();
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);

        var result = await _service.HandleDrawMessageAsync(new LotteryDrawMessage { SessionId = 1, LotteryClass = LotteryClass.Standard, ExpectedDrawDateTimePacific = session.LotteryDrawStandard.AddTicks(1) });

        result.Should().Be(LotteryDrawOutcome.NoOp);
    }

    [Fact]
    public async Task HandleDraw_BeforeDraw_Reschedule()
    {
        // Far-future session so the draw time is in the future.
        var sessionDate = TimeZoneUtils.GetCurrentPacificTime().AddDays(20);
        var session = new SessionDetailedResponse
        {
            SessionId = 1,
            SessionDate = sessionDate,
            BuyDayMinimum = 1,
            LotteryEntryWindowMinutes = 30,
            LotteryEnabled = true,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);

        var result = await _service.HandleDrawMessageAsync(new LotteryDrawMessage { SessionId = 1, LotteryClass = LotteryClass.Standard, ExpectedDrawDateTimePacific = session.LotteryDrawStandard });

        result.Should().Be(LotteryDrawOutcome.Reschedule);
    }

    [Fact]
    public async Task HandleDraw_ClaimZero_NoOp()
    {
        var session = CreateSession();
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockLotteryRepository.Setup(x => x.ClaimForDrawingAsync(1, LotteryClass.Standard)).ReturnsAsync(0);

        var result = await _service.HandleDrawMessageAsync(new LotteryDrawMessage { SessionId = 1, LotteryClass = LotteryClass.Standard, ExpectedDrawDateTimePacific = session.LotteryDrawStandard });

        result.Should().Be(LotteryDrawOutcome.NoOp);
        _mockLotteryRepository.Verify(x => x.PersistDrawOrderAsync(It.IsAny<int>(), It.IsAny<LotteryClass>(), It.IsAny<IReadOnlyList<(int, int)>>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task HandleDraw_HappyPath_ShufflesPersistsBeforeBuy_AndPublishes()
    {
        var session = CreateSession();
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockLotteryRepository.Setup(x => x.ClaimForDrawingAsync(1, LotteryClass.Standard)).ReturnsAsync(2);
        var entrants = new List<SessionLotteryEntrant> { CreateEntrant(10, "userA"), CreateEntrant(20, "userB") };
        _mockLotteryRepository.Setup(x => x.GetEntrantsAsync(1, LotteryClass.Standard, LotteryEntrantStatus.Drawing)).ReturnsAsync(entrants);

        var callOrder = new List<string>();
        IReadOnlyList<(int, int)>? capturedOrder = null;
        _mockLotteryRepository.Setup(x => x.PersistDrawOrderAsync(1, LotteryClass.Standard, It.IsAny<IReadOnlyList<(int, int)>>(), It.IsAny<DateTime>()))
            .Callback<int, LotteryClass, IReadOnlyList<(int, int)>, DateTime>((_, _, ordered, _) => { callOrder.Add("persist"); capturedOrder = ordered; })
            .Returns(Task.CompletedTask);
        _mockBuySellService.Setup(x => x.ProcessBuyRequestAsync(It.IsAny<string>(), It.IsAny<BuyRequest>(), true))
            .Callback<string, BuyRequest, bool>((userId, _, _) => callOrder.Add("buy:" + userId))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateSuccess(BuyResponse()));

        string? activity = null;
        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, It.IsAny<string>()))
            .Callback<int, string>((_, a) => activity = a).ReturnsAsync(session);

        var result = await _service.HandleDrawMessageAsync(new LotteryDrawMessage { SessionId = 1, LotteryClass = LotteryClass.Standard, ExpectedDrawDateTimePacific = session.LotteryDrawStandard });

        result.Should().Be(LotteryDrawOutcome.Completed);
        // Persist happens before any buy.
        callOrder[0].Should().Be("persist");
        // Reverse shuffle => userB drawn first, userA second.
        callOrder.Should().Equal("persist", "buy:userB", "buy:userA");
        capturedOrder.Should().Equal((20, 1), (10, 2));
        _mockLotteryRepository.Verify(x => x.MarkDrawnAsync(20), Times.Once);
        _mockLotteryRepository.Verify(x => x.MarkDrawnAsync(10), Times.Once);
        activity.Should().Be("Lottery Draw Results (Standard): FirstuserB LastuserB, FirstuserA LastuserA");
        _mockServiceBus.Verify(x => x.SendAsync(It.IsAny<ServiceBusCommsMessage>(), "LotteryDrawCompleted", It.IsAny<string>(), "comms", It.IsAny<CancellationToken>(), It.IsAny<DateTimeOffset?>()), Times.Once);
    }

    [Fact]
    public async Task HandleDraw_DrawCompleted_NotifiesEntrantsAndAllAlertSubscribers()
    {
        var session = CreateSession();
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockLotteryRepository.Setup(x => x.ClaimForDrawingAsync(1, LotteryClass.Standard)).ReturnsAsync(1);
        _mockLotteryRepository.Setup(x => x.GetEntrantsAsync(1, LotteryClass.Standard, LotteryEntrantStatus.Drawing))
            .ReturnsAsync(new List<SessionLotteryEntrant> { CreateEntrant(10, "userA") }); // email userA@example.com
        _mockLotteryRepository.Setup(x => x.PersistDrawOrderAsync(It.IsAny<int>(), It.IsAny<LotteryClass>(), It.IsAny<IReadOnlyList<(int, int)>>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, It.IsAny<string>())).ReturnsAsync(session);
        SetupBuySuccess();
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync()).ReturnsAsync(new List<UserDetailedResponse>
        {
            User("sub", active: true, NotificationPreference.All, "alerts@example.com"),    // included
            User("userA", active: true, NotificationPreference.All, "userA@example.com"),   // duplicate of entrant -> deduped
            User("off", active: true, NotificationPreference.OnlyMyBuySell, "off@example.com"), // wrong preference
            User("inactive", active: false, NotificationPreference.All, "inactive@example.com"), // inactive
            User("noemail", active: true, NotificationPreference.All, ""),                  // empty email filtered
        });

        ICollection<string>? recipients = null;
        _mockServiceBus.Setup(x => x.SendAsync(It.IsAny<ServiceBusCommsMessage>(), "LotteryDrawCompleted", It.IsAny<string>(), "comms", It.IsAny<CancellationToken>(), It.IsAny<DateTimeOffset?>()))
            .Callback<ServiceBusCommsMessage, string, string, string, CancellationToken, DateTimeOffset?>((m, _, _, _, _, _) => recipients = m.NotificationEmails)
            .Returns(Task.CompletedTask);

        var result = await _service.HandleDrawMessageAsync(new LotteryDrawMessage { SessionId = 1, LotteryClass = LotteryClass.Standard, ExpectedDrawDateTimePacific = session.LotteryDrawStandard });

        result.Should().Be(LotteryDrawOutcome.Completed);
        recipients.Should().BeEquivalentTo(new[] { "userA@example.com", "alerts@example.com" });
    }

    [Fact]
    public async Task HandleDraw_OneEntrantFails_RemainderProcessed()
    {
        var session = CreateSession();
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockLotteryRepository.Setup(x => x.ClaimForDrawingAsync(1, LotteryClass.Standard)).ReturnsAsync(2);
        var entrants = new List<SessionLotteryEntrant> { CreateEntrant(10, "userA"), CreateEntrant(20, "userB") };
        _mockLotteryRepository.Setup(x => x.GetEntrantsAsync(1, LotteryClass.Standard, LotteryEntrantStatus.Drawing)).ReturnsAsync(entrants);
        _mockLotteryRepository.Setup(x => x.PersistDrawOrderAsync(It.IsAny<int>(), It.IsAny<LotteryClass>(), It.IsAny<IReadOnlyList<(int, int)>>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, It.IsAny<string>())).ReturnsAsync(session);

        // userB (drawn first) fails; userA succeeds.
        _mockBuySellService.Setup(x => x.ProcessBuyRequestAsync("userB", It.IsAny<BuyRequest>(), true))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateFailure("already rostered"));
        _mockBuySellService.Setup(x => x.ProcessBuyRequestAsync("userA", It.IsAny<BuyRequest>(), true))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateSuccess(BuyResponse()));

        var result = await _service.HandleDrawMessageAsync(new LotteryDrawMessage { SessionId = 1, LotteryClass = LotteryClass.Standard, ExpectedDrawDateTimePacific = session.LotteryDrawStandard });

        result.Should().Be(LotteryDrawOutcome.Completed);
        _mockLotteryRepository.Verify(x => x.MarkFailedAsync(20, "already rostered"), Times.Once);
        _mockLotteryRepository.Verify(x => x.MarkDrawnAsync(10), Times.Once);
    }

    [Fact]
    public async Task HandleDraw_EntrantsWithNullUserOrEmptyEmail_AndNullBaseUrl_StillCompletes()
    {
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns((string?) null);
        var session = CreateSession();
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockLotteryRepository.Setup(x => x.ClaimForDrawingAsync(1, LotteryClass.Standard)).ReturnsAsync(2);
        var entrants = new List<SessionLotteryEntrant>
        {
            new() { LotteryEntrantId = 10, SessionId = 1, UserId = "userA", LotteryClass = LotteryClass.Standard, Status = LotteryEntrantStatus.Drawing, User = null },
            new() { LotteryEntrantId = 20, SessionId = 1, UserId = "userB", LotteryClass = LotteryClass.Standard, Status = LotteryEntrantStatus.Drawing, User = new AspNetUser { Id = "userB", FirstName = "Bob", LastName = "B", Email = "" } }
        };
        _mockLotteryRepository.Setup(x => x.GetEntrantsAsync(1, LotteryClass.Standard, LotteryEntrantStatus.Drawing)).ReturnsAsync(entrants);
        _mockLotteryRepository.Setup(x => x.PersistDrawOrderAsync(It.IsAny<int>(), It.IsAny<LotteryClass>(), It.IsAny<IReadOnlyList<(int, int)>>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, It.IsAny<string>())).ReturnsAsync(session);
        SetupBuySuccess();

        var result = await _service.HandleDrawMessageAsync(new LotteryDrawMessage { SessionId = 1, LotteryClass = LotteryClass.Standard, ExpectedDrawDateTimePacific = session.LotteryDrawStandard });

        result.Should().Be(LotteryDrawOutcome.Completed);
        // No entrant emails collected (one null user, one empty email).
        _mockServiceBus.Verify(x => x.SendAsync(It.Is<ServiceBusCommsMessage>(m => m.NotificationEmails!.Count == 0), "LotteryDrawCompleted", It.IsAny<string>(), "comms", It.IsAny<CancellationToken>(), It.IsAny<DateTimeOffset?>()), Times.Once);
    }

    [Fact]
    public async Task HandleDraw_Exception_NoOp()
    {
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _service.HandleDrawMessageAsync(new LotteryDrawMessage { SessionId = 1, LotteryClass = LotteryClass.Standard, ExpectedDrawDateTimePacific = DateTime.Now });

        result.Should().Be(LotteryDrawOutcome.NoOp);
    }

    // ---------- EnterAsync ----------

    [Fact]
    public async Task Enter_EligibleToEnter_CreatesEntrantAndPublishes()
    {
        var session = CreateSession();
        _mockBuySellService.Setup(x => x.CanBuyAsync("user1", 1, false)).ReturnsAsync(
            ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
            {
                IsAllowed = false,
                Reason = "enter",
                BuyActionState = BuyActionState.EnterLottery,
                LotteryClass = LotteryClass.Preferred,
                TimeUntilDraw = TimeSpan.FromMinutes(10)
            }));
        _userManager.Setup(x => x.FindByIdAsync("user1")).ReturnsAsync(CreateUser());
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockLotteryRepository.Setup(x => x.CreateOrReactivateEntrantAsync(1, "user1", LotteryClass.Preferred, 1.0m, It.IsAny<string>()))
            .ReturnsAsync(CreateEntrant(1, "user1", LotteryClass.Preferred, LotteryEntrantStatus.Entered));

        var result = await _service.EnterAsync("user1", 1);

        result.IsSuccess.Should().BeTrue();
        result.Data!.BuyActionState.Should().Be(BuyActionState.InLottery);
        result.Data.LotteryClass.Should().Be(LotteryClass.Preferred);
        _mockLotteryRepository.Verify(x => x.CreateOrReactivateEntrantAsync(1, "user1", LotteryClass.Preferred, 1.0m, "Firstuser1 Lastuser1 entered the Preferred lottery"), Times.Once);
        _mockServiceBus.Verify(x => x.SendAsync(It.IsAny<ServiceBusCommsMessage>(), "LotteryEntered", It.IsAny<string>(), "comms", It.IsAny<CancellationToken>(), It.IsAny<DateTimeOffset?>()), Times.Once);
    }

    [Fact]
    public async Task Enter_FiltersNotificationEmails_AndHandlesNullBaseUrl()
    {
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns((string?) null);
        // Exercises every branch of the notification-email predicate + the empty-email filter.
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync()).ReturnsAsync(new List<UserDetailedResponse>
        {
            User("a", active: true, NotificationPreference.All, "a@x.com"),   // included
            User("b", active: true, NotificationPreference.All, ""),          // matches predicate, empty email filtered out
            User("c", active: false, NotificationPreference.All, "c@x.com"),  // inactive
            User("d", active: true, NotificationPreference.OnlyMyBuySell, "d@x.com") // wrong preference
        });

        var session = CreateSession();
        _mockBuySellService.Setup(x => x.CanBuyAsync("user1", 1, false)).ReturnsAsync(
            ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse { IsAllowed = false, Reason = "enter", BuyActionState = BuyActionState.EnterLottery, LotteryClass = LotteryClass.Standard, TimeUntilDraw = TimeSpan.FromMinutes(5) }));
        // Buyer with null first/last name exercises the null-coalescing branches in the message builder.
        _userManager.Setup(x => x.FindByIdAsync("user1")).ReturnsAsync(new AspNetUser { Id = "user1", FirstName = null, LastName = null, Email = "user1@example.com", Active = true });
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockLotteryRepository.Setup(x => x.CreateOrReactivateEntrantAsync(1, "user1", LotteryClass.Standard, 1.0m, It.IsAny<string>()))
            .ReturnsAsync(CreateEntrant(1, "user1"));

        var result = await _service.EnterAsync("user1", 1);

        result.IsSuccess.Should().BeTrue();
        _mockServiceBus.Verify(x => x.SendAsync(It.Is<ServiceBusCommsMessage>(m => m.NotificationEmails!.Count == 1 && m.NotificationEmails.Contains("a@x.com")), "LotteryEntered", It.IsAny<string>(), "comms", It.IsAny<CancellationToken>(), It.IsAny<DateTimeOffset?>()), Times.Once);
    }

    [Fact]
    public async Task Enter_NotEnterLotteryState_Fails()
    {
        _mockBuySellService.Setup(x => x.CanBuyAsync("user1", 1, false)).ReturnsAsync(
            ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse { IsAllowed = false, Reason = "You are entered in the Preferred lottery", BuyActionState = BuyActionState.InLottery }));

        var result = await _service.EnterAsync("user1", 1);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("You are entered in the Preferred lottery");
        _mockLotteryRepository.Verify(x => x.CreateOrReactivateEntrantAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<LotteryClass>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Enter_CanBuyFailure_Fails()
    {
        _mockBuySellService.Setup(x => x.CanBuyAsync("user1", 1, false)).ReturnsAsync(ServiceResult<BuySellStatusResponse>.CreateFailure("db error"));

        var result = await _service.EnterAsync("user1", 1);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("db error");
    }

    [Fact]
    public async Task Enter_UserNotFound_Fails()
    {
        _mockBuySellService.Setup(x => x.CanBuyAsync("user1", 1, false)).ReturnsAsync(
            ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse { IsAllowed = false, Reason = "enter", BuyActionState = BuyActionState.EnterLottery, LotteryClass = LotteryClass.Standard }));
        _userManager.Setup(x => x.FindByIdAsync("user1")).ReturnsAsync((AspNetUser?) null);

        var result = await _service.EnterAsync("user1", 1);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task Enter_Exception_Fails()
    {
        _mockBuySellService.Setup(x => x.CanBuyAsync("user1", 1, false)).ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _service.EnterAsync("user1", 1);

        result.IsSuccess.Should().BeFalse();
    }

    // ---------- WithdrawAsync ----------

    [Fact]
    public async Task Withdraw_Entered_Succeeds()
    {
        _userManager.Setup(x => x.FindByIdAsync("user1")).ReturnsAsync(CreateUser());
        _mockLotteryRepository.Setup(x => x.GetEntrantAsync(1, "user1")).ReturnsAsync(CreateEntrant(1, "user1", LotteryClass.Standard, LotteryEntrantStatus.Entered));
        _mockLotteryRepository.Setup(x => x.WithdrawEntrantAsync(1, "user1", It.IsAny<string>())).ReturnsAsync(CreateEntrant(1, "user1", LotteryClass.Standard, LotteryEntrantStatus.Withdrawn));

        var result = await _service.WithdrawAsync("user1", 1);

        result.IsSuccess.Should().BeTrue();
        _mockLotteryRepository.Verify(x => x.WithdrawEntrantAsync(1, "user1", "Firstuser1 Lastuser1 withdrew from the Standard lottery"), Times.Once);
    }

    [Fact]
    public async Task Withdraw_UserNotFound_Fails()
    {
        _userManager.Setup(x => x.FindByIdAsync("user1")).ReturnsAsync((AspNetUser?) null);

        var result = await _service.WithdrawAsync("user1", 1);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task Withdraw_NoEntry_Fails()
    {
        _userManager.Setup(x => x.FindByIdAsync("user1")).ReturnsAsync(CreateUser());
        _mockLotteryRepository.Setup(x => x.GetEntrantAsync(1, "user1")).ReturnsAsync((SessionLotteryEntrant?) null);

        var result = await _service.WithdrawAsync("user1", 1);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Withdraw_NotEnteredStatus_Fails()
    {
        _userManager.Setup(x => x.FindByIdAsync("user1")).ReturnsAsync(CreateUser());
        _mockLotteryRepository.Setup(x => x.GetEntrantAsync(1, "user1")).ReturnsAsync(CreateEntrant(1, "user1", LotteryClass.Standard, LotteryEntrantStatus.Drawing));

        var result = await _service.WithdrawAsync("user1", 1);

        result.IsSuccess.Should().BeFalse();
        _mockLotteryRepository.Verify(x => x.WithdrawEntrantAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Withdraw_RaceLostEntry_Fails()
    {
        _userManager.Setup(x => x.FindByIdAsync("user1")).ReturnsAsync(CreateUser());
        _mockLotteryRepository.Setup(x => x.GetEntrantAsync(1, "user1")).ReturnsAsync(CreateEntrant(1, "user1", LotteryClass.Standard, LotteryEntrantStatus.Entered));
        _mockLotteryRepository.Setup(x => x.WithdrawEntrantAsync(1, "user1", It.IsAny<string>())).ReturnsAsync((SessionLotteryEntrant?) null);

        var result = await _service.WithdrawAsync("user1", 1);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Withdraw_Exception_Fails()
    {
        _userManager.Setup(x => x.FindByIdAsync("user1")).ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _service.WithdrawAsync("user1", 1);

        result.IsSuccess.Should().BeFalse();
    }

    // ---------- ExecuteDueAsync ----------

    [Fact]
    public async Task ExecuteDue_Empty_ReturnsZero()
    {
        _mockLotteryRepository.Setup(x => x.GetDueUndrawnTiersAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<(int, LotteryClass)>());
        _mockLotteryRepository.Setup(x => x.GetStuckDrawingAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<SessionLotteryEntrant>());

        var result = await _service.ExecuteDueAsync();

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteDue_DueTier_RunsDraw()
    {
        var session = CreateSession();
        _mockLotteryRepository.Setup(x => x.GetDueUndrawnTiersAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<(int, LotteryClass)> { (1, LotteryClass.Standard) });
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockLotteryRepository.Setup(x => x.ClaimForDrawingAsync(1, LotteryClass.Standard)).ReturnsAsync(1);
        _mockLotteryRepository.Setup(x => x.GetEntrantsAsync(1, LotteryClass.Standard, LotteryEntrantStatus.Drawing)).ReturnsAsync(new List<SessionLotteryEntrant> { CreateEntrant(10, "userA") });
        _mockLotteryRepository.Setup(x => x.PersistDrawOrderAsync(It.IsAny<int>(), It.IsAny<LotteryClass>(), It.IsAny<IReadOnlyList<(int, int)>>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, It.IsAny<string>())).ReturnsAsync(session);
        _mockLotteryRepository.Setup(x => x.GetStuckDrawingAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<SessionLotteryEntrant>());
        SetupBuySuccess();

        var result = await _service.ExecuteDueAsync();

        result.Data.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteDue_DueTier_SessionMissing_Skipped()
    {
        _mockLotteryRepository.Setup(x => x.GetDueUndrawnTiersAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<(int, LotteryClass)> { (1, LotteryClass.Standard) });
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync((SessionDetailedResponse) null!);
        _mockLotteryRepository.Setup(x => x.GetStuckDrawingAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<SessionLotteryEntrant>());

        var result = await _service.ExecuteDueAsync();

        result.Data.Should().Be(0);
        _mockLotteryRepository.Verify(x => x.ClaimForDrawingAsync(It.IsAny<int>(), It.IsAny<LotteryClass>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteDue_StuckWithDrawOrder_ResumesInOrder()
    {
        var session = CreateSession();
        _mockLotteryRepository.Setup(x => x.GetDueUndrawnTiersAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<(int, LotteryClass)>());
        _mockLotteryRepository.Setup(x => x.GetStuckDrawingAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<SessionLotteryEntrant> { CreateEntrant(10, "userA", drawOrder: 1) });
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockLotteryRepository.Setup(x => x.GetDrawingOrderedAsync(1, LotteryClass.Standard)).ReturnsAsync(new List<SessionLotteryEntrant> { CreateEntrant(10, "userA", drawOrder: 1) });
        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, It.IsAny<string>())).ReturnsAsync(session);
        SetupBuySuccess();

        var result = await _service.ExecuteDueAsync();

        result.Data.Should().Be(1);
        // Resume path must NOT re-persist the draw order.
        _mockLotteryRepository.Verify(x => x.PersistDrawOrderAsync(It.IsAny<int>(), It.IsAny<LotteryClass>(), It.IsAny<IReadOnlyList<(int, int)>>(), It.IsAny<DateTime>()), Times.Never);
        _mockLotteryRepository.Verify(x => x.MarkDrawnAsync(10), Times.Once);
    }

    [Fact]
    public async Task ExecuteDue_StuckWithoutDrawOrder_Reclaims()
    {
        var session = CreateSession();
        _mockLotteryRepository.Setup(x => x.GetDueUndrawnTiersAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<(int, LotteryClass)>());
        _mockLotteryRepository.Setup(x => x.GetStuckDrawingAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<SessionLotteryEntrant> { CreateEntrant(10, "userA", drawOrder: null) });
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockLotteryRepository.Setup(x => x.GetDrawingOrderedAsync(1, LotteryClass.Standard)).ReturnsAsync(new List<SessionLotteryEntrant> { CreateEntrant(10, "userA", drawOrder: null) });
        _mockLotteryRepository.Setup(x => x.PersistDrawOrderAsync(It.IsAny<int>(), It.IsAny<LotteryClass>(), It.IsAny<IReadOnlyList<(int, int)>>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, It.IsAny<string>())).ReturnsAsync(session);
        SetupBuySuccess();

        var result = await _service.ExecuteDueAsync();

        result.Data.Should().Be(1);
        _mockLotteryRepository.Verify(x => x.PersistDrawOrderAsync(It.IsAny<int>(), It.IsAny<LotteryClass>(), It.IsAny<IReadOnlyList<(int, int)>>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteDue_StuckOrderedEmpty_Skipped()
    {
        var session = CreateSession();
        _mockLotteryRepository.Setup(x => x.GetDueUndrawnTiersAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<(int, LotteryClass)>());
        _mockLotteryRepository.Setup(x => x.GetStuckDrawingAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<SessionLotteryEntrant> { CreateEntrant(10, "userA", drawOrder: 1) });
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockLotteryRepository.Setup(x => x.GetDrawingOrderedAsync(1, LotteryClass.Standard)).ReturnsAsync(new List<SessionLotteryEntrant>());

        var result = await _service.ExecuteDueAsync();

        result.Data.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteDue_StuckSessionMissing_Skipped()
    {
        _mockLotteryRepository.Setup(x => x.GetDueUndrawnTiersAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<(int, LotteryClass)>());
        _mockLotteryRepository.Setup(x => x.GetStuckDrawingAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<SessionLotteryEntrant> { CreateEntrant(10, "userA", drawOrder: 1) });
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync((SessionDetailedResponse) null!);

        var result = await _service.ExecuteDueAsync();

        result.Data.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteDue_Exception_Fails()
    {
        _mockLotteryRepository.Setup(x => x.GetDueUndrawnTiersAsync(It.IsAny<DateTime>())).ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _service.ExecuteDueAsync();

        result.IsSuccess.Should().BeFalse();
    }

    // ---------- EnqueueDrawMessagesAsync ----------

    [Fact]
    public async Task EnqueueDrawMessages_SendsThreeScheduledMessages()
    {
        // Use a session date that crosses into PST (non-DST) so Pacific->UTC offset is -8h.
        var session = new SessionDetailedResponse
        {
            SessionId = 7,
            SessionDate = new DateTime(2026, 2, 25, 7, 30, 0),
            BuyDayMinimum = 6,
            LotteryEntryWindowMinutes = 30,
            LotteryEnabled = true,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };

        var sent = new List<(LotteryClass Class, DateTimeOffset? Scheduled)>();
        _mockServiceBus.Setup(x => x.SendAsync(It.IsAny<LotteryDrawMessage>(), "LotteryDraw", It.IsAny<string>(), "lottery", It.IsAny<CancellationToken>(), It.IsAny<DateTimeOffset?>()))
            .Callback<LotteryDrawMessage, string, string, string, CancellationToken, DateTimeOffset?>((m, _, _, _, _, scheduled) => sent.Add((m.LotteryClass, scheduled)))
            .Returns(Task.CompletedTask);

        await _service.EnqueueDrawMessagesAsync(session);

        sent.Should().HaveCount(3);
        sent.Select(s => s.Class).Should().BeEquivalentTo(new[] { LotteryClass.PreferredPlus, LotteryClass.Preferred, LotteryClass.Standard });
        // PreferredPlus draw 02/18 9:55 Pacific (PST, UTC-8) -> 17:55 UTC.
        var pp = sent.First(s => s.Class == LotteryClass.PreferredPlus);
        pp.Scheduled.Should().Be(new DateTimeOffset(2026, 2, 18, 17, 55, 0, TimeSpan.Zero));
    }
}
