using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using RosterPlayer = HockeyPickup.Api.Models.Responses.RosterPlayer;

namespace HockeyPickup.Api.Tests.ServicesTests;

public class BuySellServiceLotteryGateTests
{
    private readonly Mock<UserManager<AspNetUser>> _userManager;
    private readonly Mock<ISessionRepository> _mockSessionRepository = new();
    private readonly Mock<IBuySellRepository> _mockBuySellRepository = new();
    private readonly Mock<IServiceBus> _mockServiceBus = new();
    private readonly Mock<IConfiguration> _mockConfiguration = new();
    private readonly Mock<ILogger<BuySellService>> _mockLogger = new();
    private readonly Mock<ISubscriptionHandler> _mockSubscriptionHandler = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<ILotteryRepository> _mockLotteryRepository = new();
    private readonly Mock<ILotteryEligibilityService> _mockLotteryEligibility = new();
    private readonly BuySellService _service;

    public BuySellServiceLotteryGateTests()
    {
        var userStore = new Mock<IUserStore<AspNetUser>>();
        _userManager = new Mock<UserManager<AspNetUser>>(
            userStore.Object, Mock.Of<IOptions<IdentityOptions>>(), Mock.Of<IPasswordHasher<AspNetUser>>(),
            Array.Empty<IUserValidator<AspNetUser>>(), Array.Empty<IPasswordValidator<AspNetUser>>(),
            Mock.Of<ILookupNormalizer>(), Mock.Of<IdentityErrorDescriber>(), Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<AspNetUser>>>());

        _service = new BuySellService(_userManager.Object, _mockSessionRepository.Object, _mockBuySellRepository.Object,
            _mockServiceBus.Object, _mockConfiguration.Object, _mockLogger.Object, _mockSubscriptionHandler.Object,
            _mockUserRepository.Object, _mockLotteryRepository.Object, _mockLotteryEligibility.Object);
    }

    private static AspNetUser CreateUser(bool active = true) => new()
    {
        Id = "user1",
        FirstName = "Test",
        LastName = "User",
        Email = "test@example.com",
        Active = active
    };

    private static SessionDetailedResponse CreateSession(bool lotteryEnabled = true)
    {
        // Future session whose buy window is already open (so the legacy window check passes on fallthrough).
        var sessionDate = TimeZoneUtils.GetCurrentPacificTime().AddDays(1);
        return new SessionDetailedResponse
        {
            SessionId = 1,
            SessionDate = sessionDate,
            BuyDayMinimum = 5,
            LotteryEntryWindowMinutes = 30,
            LotteryEnabled = lotteryEnabled,
            CurrentRosters = new List<RosterPlayer>(),
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };
    }

    private void SetupCommonHappyPath(SessionDetailedResponse session, AspNetUser buyer, bool isAdmin = false)
    {
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _userManager.Setup(x => x.FindByIdAsync("user1")).ReturnsAsync(buyer);
        _userManager.Setup(x => x.IsInRoleAsync(buyer, "Admin")).ReturnsAsync(isAdmin);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(1, "user1")).ReturnsAsync(new List<BuySell>());
    }

    private void SetupEligibility(BuyActionState state, LotteryClass? lotteryClass = null, TimeSpan? timeUntilDraw = null, bool allowDirectBuy = false)
        => _mockLotteryEligibility.Setup(x => x.Resolve(It.IsAny<SessionDetailedResponse>(), It.IsAny<AspNetUser>(), It.IsAny<SessionLotteryEntrant?>(), It.IsAny<DateTime>()))
            .Returns(new LotteryEligibility { State = state, ChosenClass = lotteryClass, TimeUntilDraw = timeUntilDraw, Reason = "reason", AllowDirectBuy = allowDirectBuy });

    [Fact]
    public async Task CanBuy_GateEnterLottery_ReturnsEnterLotteryState()
    {
        var buyer = CreateUser();
        SetupCommonHappyPath(CreateSession(), buyer);
        _mockLotteryRepository.Setup(x => x.GetEntrantAsync(1, "user1")).ReturnsAsync((SessionLotteryEntrant?) null);
        SetupEligibility(BuyActionState.EnterLottery, LotteryClass.Preferred, TimeSpan.FromMinutes(5));

        var result = await _service.CanBuyAsync("user1", 1);

        result.Data!.IsAllowed.Should().BeFalse();
        result.Data.BuyActionState.Should().Be(BuyActionState.EnterLottery);
        result.Data.LotteryClass.Should().Be(LotteryClass.Preferred);
        result.Data.TimeUntilDraw.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task CanBuy_GateInLottery_ReturnsInLotteryState()
    {
        var buyer = CreateUser();
        SetupCommonHappyPath(CreateSession(), buyer);
        _mockLotteryRepository.Setup(x => x.GetEntrantAsync(1, "user1")).ReturnsAsync(new SessionLotteryEntrant { SessionId = 1, UserId = "user1", LotteryClass = LotteryClass.Standard, Status = LotteryEntrantStatus.Entered });
        SetupEligibility(BuyActionState.InLottery, LotteryClass.Standard, TimeSpan.FromMinutes(3));

        var result = await _service.CanBuyAsync("user1", 1);

        result.Data!.BuyActionState.Should().Be(BuyActionState.InLottery);
        result.Data.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task CanBuy_GateWindowNotOpen_ReturnsWindowNotOpen()
    {
        var buyer = CreateUser();
        SetupCommonHappyPath(CreateSession(), buyer);
        _mockLotteryRepository.Setup(x => x.GetEntrantAsync(1, "user1")).ReturnsAsync((SessionLotteryEntrant?) null);
        SetupEligibility(BuyActionState.WindowNotOpen, timeUntilDraw: TimeSpan.FromHours(2));

        var result = await _service.CanBuyAsync("user1", 1);

        result.Data!.BuyActionState.Should().Be(BuyActionState.WindowNotOpen);
        result.Data.TimeUntilAllowed.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task CanBuy_GateBuyNow_FallsThroughToLegacyAllowed()
    {
        var buyer = CreateUser();
        SetupCommonHappyPath(CreateSession(), buyer);
        _mockLotteryRepository.Setup(x => x.GetEntrantAsync(1, "user1")).ReturnsAsync((SessionLotteryEntrant?) null);
        SetupEligibility(BuyActionState.BuyNow, allowDirectBuy: true);

        var result = await _service.CanBuyAsync("user1", 1);

        result.Data!.IsAllowed.Should().BeTrue();
        result.Data.BuyActionState.Should().Be(BuyActionState.BuyNow);
    }

    [Fact]
    public async Task CanBuy_LotteryDisabled_SkipsGate_LegacyBehavior()
    {
        var buyer = CreateUser();
        SetupCommonHappyPath(CreateSession(lotteryEnabled: false), buyer);

        var result = await _service.CanBuyAsync("user1", 1);

        result.Data!.IsAllowed.Should().BeTrue();
        result.Data.BuyActionState.Should().Be(BuyActionState.BuyNow);
        _mockLotteryEligibility.Verify(x => x.Resolve(It.IsAny<SessionDetailedResponse>(), It.IsAny<AspNetUser>(), It.IsAny<SessionLotteryEntrant?>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task CanBuy_BypassLotteryGate_SkipsGateOnly()
    {
        var buyer = CreateUser();
        SetupCommonHappyPath(CreateSession(), buyer);

        var result = await _service.CanBuyAsync("user1", 1, bypassLotteryGate: true);

        result.Data!.IsAllowed.Should().BeTrue();
        result.Data.BuyActionState.Should().Be(BuyActionState.BuyNow);
        _mockLotteryEligibility.Verify(x => x.Resolve(It.IsAny<SessionDetailedResponse>(), It.IsAny<AspNetUser>(), It.IsAny<SessionLotteryEntrant?>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task CanBuy_BypassLotteryGate_StillEnforcesRosterCheck()
    {
        var buyer = CreateUser();
        var session = CreateSession();
        session.CurrentRosters.Add(new RosterPlayer
        {
            UserId = "user1",
            TeamAssignment = TeamAssignment.Light,
            IsPlaying = true,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Rating = 1.0m,
            Position = PositionPreference.Forward,
            PlayerStatus = PlayerStatus.Regular,
            PhotoUrl = null!,
            IsRegular = true,
            SessionId = 1,
            SessionRosterId = 1,
            JoinedDateTime = DateTime.UtcNow,
            CurrentPosition = "Forward",
            Preferred = false,
            PreferredPlus = false
        });
        SetupCommonHappyPath(session, buyer);

        var result = await _service.CanBuyAsync("user1", 1, bypassLotteryGate: true);

        result.Data!.IsAllowed.Should().BeFalse();
        result.Data.BuyActionState.Should().Be(BuyActionState.NotEligible);
        result.Data.Reason.Should().Contain("already on the roster");
    }

    [Fact]
    public async Task CanBuy_Admin_BypassesLotteryGateEntirely()
    {
        var buyer = CreateUser();
        SetupCommonHappyPath(CreateSession(), buyer, isAdmin: true);

        var result = await _service.CanBuyAsync("user1", 1);

        result.Data!.IsAllowed.Should().BeTrue();
        result.Data.BuyActionState.Should().Be(BuyActionState.BuyNow);
        _mockLotteryEligibility.Verify(x => x.Resolve(It.IsAny<SessionDetailedResponse>(), It.IsAny<AspNetUser>(), It.IsAny<SessionLotteryEntrant?>(), It.IsAny<DateTime>()), Times.Never);
    }
}
