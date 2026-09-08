using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RosterPlayer = HockeyPickup.Api.Models.Responses.RosterPlayer;

namespace HockeyPickup.Api.Tests.ServicesTests;

// An Admin impersonating a player carries their Admin buy bypass onto that player, so the commissioner can seat
// someone who asked well in advance - before any buy window opens and regardless of the lottery. The bypass is
// scoped to buying: every other denial (inactive, past session, already rostered, duplicate Buy/Sell) still applies.
public class BuySellServiceImpersonationTests
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

    private const string BuyerId = "buyer1";
    private const string AdminId = "admin1";

    public BuySellServiceImpersonationTests()
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

    private static AspNetUser CreateBuyer(bool active = true) => new()
    {
        Id = BuyerId,
        FirstName = "Buy",
        LastName = "Er",
        Email = "buyer@example.com",
        Active = active
    };

    private static AspNetUser CreateAdmin() => new()
    {
        Id = AdminId,
        FirstName = "Comm",
        LastName = "Issioner",
        Email = "admin@example.com",
        Active = true
    };

    // Session 10 days out with a 1-day buy minimum: no tier's buy window has opened, which is exactly the
    // "player emailed way in advance" case this bypass exists for.
    private static SessionDetailedResponse CreateSessionWithClosedBuyWindow(bool lotteryEnabled = false)
    {
        return new SessionDetailedResponse
        {
            SessionId = 1,
            SessionDate = TimeZoneUtils.GetCurrentPacificTime().AddDays(10),
            BuyDayMinimum = 1,
            Cost = 20.00m,
            LotteryEntryWindowMinutes = 30,
            LotteryEnabled = lotteryEnabled,
            CurrentRosters = new List<RosterPlayer>(),
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };
    }

    private static List<RosterPlayer> CreateRoster(string userId)
    {
        return new List<RosterPlayer>
        {
            new RosterPlayer
            {
                UserId = userId,
                TeamAssignment = TeamAssignment.Light,
                IsPlaying = true,
                FirstName = "Buy",
                LastName = "Er",
                Email = "buyer@example.com",
                Rating = 1.0m,
                Position = PositionPreference.Forward,
                PlayerStatus = PlayerStatus.Regular,
                PhotoUrl = null!,
                IsRegular = true,
                SessionId = 1,
                SessionRosterId = 1,
                JoinedDateTime = DateTime.UtcNow,
                CurrentPosition = "Forward",
                LastBuySellId = null,
                Preferred = false,
                PreferredPlus = false
            }
        };
    }

    private void SetupBuyer(SessionDetailedResponse session, AspNetUser buyer, List<BuySell>? existingBuySells = null)
    {
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _userManager.Setup(x => x.FindByIdAsync(BuyerId)).ReturnsAsync(buyer);
        _userManager.Setup(x => x.IsInRoleAsync(buyer, "Admin")).ReturnsAsync(false);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(1, BuyerId)).ReturnsAsync(existingBuySells ?? new List<BuySell>());
    }

    private void SetupImpersonator(bool isAdmin = true)
    {
        var admin = CreateAdmin();
        _userManager.Setup(x => x.FindByIdAsync(AdminId)).ReturnsAsync(admin);
        _userManager.Setup(x => x.IsInRoleAsync(admin, "Admin")).ReturnsAsync(isAdmin);
    }

    [Fact]
    public async Task CanBuyAsync_ImpersonatingAdmin_BuyWindowClosed_AllowsBuyNow()
    {
        // Arrange
        SetupBuyer(CreateSessionWithClosedBuyWindow(), CreateBuyer());
        SetupImpersonator();

        // Act
        var result = await _service.CanBuyAsync(BuyerId, 1, impersonatingAdminId: AdminId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.IsAllowed.Should().BeTrue();
        result.Data.BuyActionState.Should().Be(BuyActionState.BuyNow);
        result.Data.Reason.Should().Be("Admins can buy spots regardless of time window while impersonating");
    }

    [Fact]
    public async Task CanBuyAsync_ImpersonatingAdmin_LotterySession_AllowsBuyNow_WithoutConsultingLottery()
    {
        // Arrange
        SetupBuyer(CreateSessionWithClosedBuyWindow(lotteryEnabled: true), CreateBuyer());
        SetupImpersonator();

        // Act
        var result = await _service.CanBuyAsync(BuyerId, 1, impersonatingAdminId: AdminId);

        // Assert
        result.Data.IsAllowed.Should().BeTrue();
        result.Data.BuyActionState.Should().Be(BuyActionState.BuyNow);
        // The bypass short-circuits ahead of the lottery gate, exactly as the plain Admin path does.
        _mockLotteryRepository.Verify(x => x.GetEntrantAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _mockLotteryEligibility.Verify(x => x.Resolve(It.IsAny<SessionDetailedResponse>(), It.IsAny<AspNetUser>(),
            It.IsAny<SessionLotteryEntrant?>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task CanBuyAsync_ImpersonatorNoLongerAdmin_FallsThroughToWindowCheck()
    {
        // Arrange - revoking the Admin role must also revoke any impersonation token still in flight
        SetupBuyer(CreateSessionWithClosedBuyWindow(), CreateBuyer());
        SetupImpersonator(isAdmin: false);

        // Act
        var result = await _service.CanBuyAsync(BuyerId, 1, impersonatingAdminId: AdminId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.BuyActionState.Should().Be(BuyActionState.WindowNotOpen);
        result.Data.Reason.Should().Be("Standard buy window is not open yet");
    }

    [Fact]
    public async Task CanBuyAsync_ImpersonatorNotFound_FallsThroughToWindowCheck()
    {
        // Arrange
        SetupBuyer(CreateSessionWithClosedBuyWindow(), CreateBuyer());
        _userManager.Setup(x => x.FindByIdAsync(AdminId)).ReturnsAsync((AspNetUser?) null);

        // Act
        var result = await _service.CanBuyAsync(BuyerId, 1, impersonatingAdminId: AdminId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.BuyActionState.Should().Be(BuyActionState.WindowNotOpen);
        result.Data.Reason.Should().Be("Standard buy window is not open yet");
    }

    [Fact]
    public async Task CanBuyAsync_NotImpersonating_BuyWindowClosed_Denied()
    {
        // Arrange
        SetupBuyer(CreateSessionWithClosedBuyWindow(), CreateBuyer());

        // Act
        var result = await _service.CanBuyAsync(BuyerId, 1);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.BuyActionState.Should().Be(BuyActionState.WindowNotOpen);
    }

    [Fact]
    public async Task CanBuyAsync_ImpersonatingAdmin_InactiveUser_StillDenied()
    {
        // Arrange
        SetupBuyer(CreateSessionWithClosedBuyWindow(), CreateBuyer(active: false));
        SetupImpersonator();

        // Act
        var result = await _service.CanBuyAsync(BuyerId, 1, impersonatingAdminId: AdminId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("User is not active");
    }

    [Fact]
    public async Task CanBuyAsync_ImpersonatingAdmin_AlreadyOnRoster_StillDenied()
    {
        // Arrange
        var session = CreateSessionWithClosedBuyWindow();
        session.CurrentRosters = CreateRoster(BuyerId);
        SetupBuyer(session, CreateBuyer());
        SetupImpersonator();

        // Act
        var result = await _service.CanBuyAsync(BuyerId, 1, impersonatingAdminId: AdminId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("You are already on the roster for this session");
    }

    [Fact]
    public async Task CanBuyAsync_ImpersonatingAdmin_AlreadyHasActiveBuy_StillDenied()
    {
        // Arrange
        var existing = new List<BuySell>
        {
            new BuySell { BuySellId = 1, SessionId = 1, BuyerUserId = BuyerId, SellerUserId = null, CreateByUserId = BuyerId, UpdateByUserId = BuyerId }
        };
        SetupBuyer(CreateSessionWithClosedBuyWindow(), CreateBuyer(), existing);
        SetupImpersonator();

        // Act
        var result = await _service.CanBuyAsync(BuyerId, 1, impersonatingAdminId: AdminId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("You already have an active Buy for this session");
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_ImpersonatingAdmin_BuyWindowClosed_AddsToBuyQueue()
    {
        // Arrange
        var request = new BuyRequest { SessionId = 1, Note = "Emailed months ago" };
        SetupBuyer(CreateSessionWithClosedBuyWindow(lotteryEnabled: true), CreateBuyer());
        SetupImpersonator();

        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(1)).ReturnsAsync((BuySell?) null);
        _mockBuySellRepository.Setup(x => x.GetQueuePositionAsync(It.IsAny<int>())).ReturnsAsync(1);
        _mockBuySellRepository.Setup(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()))
            .ReturnsAsync(new BuySell
            {
                BuySellId = 1,
                SessionId = 1,
                BuyerUserId = BuyerId,
                Price = 20.00m,
                TeamAssignment = TeamAssignment.TBD,
                CreateByUserId = BuyerId,
                UpdateByUserId = BuyerId,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow,
                Buyer = CreateBuyer(),
                Session = new Session
                {
                    SessionId = 1,
                    SessionDate = TimeZoneUtils.GetCurrentPacificTime().AddDays(10),
                    BuyDayMinimum = 1,
                    Cost = 20.00m,
                    CurrentSessionRoster = new List<Data.Entities.CurrentSessionRoster>(),
                    CreateDateTime = DateTime.UtcNow,
                    UpdateDateTime = DateTime.UtcNow,
                    Note = "Test Session"
                }
            });
        _mockUserRepository.Setup(x => x.GetUserAsync(BuyerId)).ReturnsAsync(new UserDetailedResponse
        {
            Id = BuyerId,
            UserName = "buyer",
            FirstName = "Buy",
            LastName = "Er",
            Email = "buyer@example.com",
            PhotoUrl = null,
            Rating = 1.0m,
            Active = true,
            Preferred = false,
            PreferredPlus = false
        });
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(It.IsAny<ServiceBusCommsMessage>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ProcessBuyRequestAsync(BuyerId, request, impersonatingAdminId: AdminId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.BuyerUserId.Should().Be(BuyerId);
        _mockBuySellRepository.Verify(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_NotImpersonating_BuyWindowClosed_Fails()
    {
        // Arrange
        SetupBuyer(CreateSessionWithClosedBuyWindow(), CreateBuyer());

        // Act
        var result = await _service.ProcessBuyRequestAsync(BuyerId, new BuyRequest { SessionId = 1 });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Standard buy window is not open yet");
        _mockBuySellRepository.Verify(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()), Times.Never);
    }
}
