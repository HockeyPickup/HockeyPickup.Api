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

namespace HockeyPickup.Api.Tests.ServicesTests;

// A user whose preference is Goalie and who buys a spot lands on the roster as a TBD skater (D11), at both match sites
public class BuySellServiceGoalieTests
{
    private const int SessionId = 1;
    private const string BuyerId = "goalieBuyer";
    private const string SellerId = "sellerUser";

    private readonly Mock<UserManager<AspNetUser>> _userManager;
    private readonly Mock<ISessionRepository> _mockSessionRepository = new();
    private readonly Mock<IBuySellRepository> _mockBuySellRepository = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly BuySellService _buySellService;

    public BuySellServiceGoalieTests()
    {
        var userStore = new Mock<IUserStore<AspNetUser>>();
        _userManager = new Mock<UserManager<AspNetUser>>(
            userStore.Object,
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<IPasswordHasher<AspNetUser>>(),
            Array.Empty<IUserValidator<AspNetUser>>(),
            Array.Empty<IPasswordValidator<AspNetUser>>(),
            Mock.Of<ILookupNormalizer>(),
            Mock.Of<IdentityErrorDescriber>(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<AspNetUser>>>());

        var configuration = new Mock<IConfiguration>();
        configuration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("testqueue");
        configuration.Setup(x => x["BaseUrl"]).Returns("https://test.com");

        var serviceBus = new Mock<IServiceBus>();
        serviceBus.Setup(x => x.SendAsync(It.IsAny<ServiceBusCommsMessage>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<DateTimeOffset?>()))
            .Returns(Task.CompletedTask);

        _buySellService = new BuySellService(
            _userManager.Object,
            _mockSessionRepository.Object,
            _mockBuySellRepository.Object,
            serviceBus.Object,
            configuration.Object,
            Mock.Of<ILogger<BuySellService>>(),
            Mock.Of<ISubscriptionHandler>(),
            _mockUserRepository.Object,
            Mock.Of<ILotteryRepository>(),
            Mock.Of<ILotteryEligibilityService>());
    }

    private static AspNetUser User(string id, PositionPreference preference) => new()
    {
        Id = id,
        FirstName = "Test",
        LastName = id,
        Email = $"{id}@example.com",
        Active = true,
        UserName = id,
        NotificationPreference = NotificationPreference.All,
        PositionPreference = preference,
        Rating = 1.0m
    };

    private static Session Session()
    {
        var date = TimeZoneUtils.GetCurrentPacificTime().AddDays(4);
        return new Session
        {
            SessionId = SessionId,
            SessionDate = new DateTime(date.Year, date.Month, date.Day, 7, 30, 0),
            BuyDayMinimum = 7,
            Cost = 20.00m,
            CurrentSessionRoster = new List<Data.Entities.CurrentSessionRoster>(),
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };
    }

    private static SessionDetailedResponse SessionWithSeller() => new()
    {
        SessionId = SessionId,
        SessionDate = DateTime.UtcNow.AddDays(1),
        BuyDayMinimum = 6,
        Cost = 20.00m,
        LotteryEnabled = false,
        CreateDateTime = DateTime.UtcNow,
        UpdateDateTime = DateTime.UtcNow,
        CurrentRosters = new List<RosterPlayer>
        {
            new()
            {
                SessionRosterId = 1,
                SessionId = SessionId,
                UserId = SellerId,
                TeamAssignment = TeamAssignment.Dark,
                IsPlaying = true,
                IsRegular = true,
                FirstName = "Test",
                LastName = SellerId,
                Email = "seller@example.com",
                Rating = 1.0m,
                Position = PositionPreference.Forward,
                CurrentPosition = "Forward",
                PlayerStatus = PlayerStatus.Regular,
                PhotoUrl = null!,
                JoinedDateTime = DateTime.UtcNow,
                Preferred = false,
                PreferredPlus = false
            }
        }
    };

    private static UserDetailedResponse UserResponse(string id) => new()
    {
        Id = id,
        UserName = id,
        FirstName = "Test",
        LastName = id,
        Email = $"{id}@example.com",
        Rating = 1.0m,
        Active = true,
        Preferred = false,
        PreferredPlus = false
    };

    private void SetupCommon(AspNetUser buyer, AspNetUser seller, SessionDetailedResponse sessionResponse)
    {
        _userManager.Setup(x => x.FindByIdAsync(BuyerId)).ReturnsAsync(buyer);
        _userManager.Setup(x => x.FindByIdAsync(SellerId)).ReturnsAsync(seller);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(SessionId)).ReturnsAsync(sessionResponse);
        _mockSessionRepository.Setup(x => x.UpdatePlayerStatusAsync(SessionId, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<int?>())).ReturnsAsync(sessionResponse);
        _mockSessionRepository.Setup(x => x.AddOrUpdatePlayerToRosterAsync(SessionId, It.IsAny<string>(), It.IsAny<TeamAssignment>(), It.IsAny<PositionPreference>(), It.IsAny<int?>())).ReturnsAsync(sessionResponse);
        _mockUserRepository.Setup(x => x.GetUserAsync(BuyerId)).ReturnsAsync(UserResponse(BuyerId));
        _mockUserRepository.Setup(x => x.GetUserAsync(SellerId)).ReturnsAsync(UserResponse(SellerId));
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(SessionId, It.IsAny<string>())).ReturnsAsync(new List<BuySell>());
    }

    [Theory]
    [InlineData(PositionPreference.Goalie, PositionPreference.TBD)]
    [InlineData(PositionPreference.Defense, PositionPreference.Defense)]
    public async Task ProcessBuyRequest_MatchedWithSeller_BuyerLandsAsSkater(PositionPreference preference, PositionPreference expectedPosition)
    {
        // Arrange
        var buyer = User(BuyerId, preference);
        var seller = User(SellerId, PositionPreference.Forward);
        var session = Session();
        SetupCommon(buyer, seller, SessionWithSeller());
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(SessionId)).ReturnsAsync(new BuySell
        {
            BuySellId = 7,
            SessionId = SessionId,
            SellerUserId = SellerId,
            TeamAssignment = TeamAssignment.Dark,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Seller = seller
        });
        _mockBuySellRepository.Setup(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>())).ReturnsAsync(new BuySell
        {
            BuySellId = 7,
            SessionId = SessionId,
            SellerUserId = SellerId,
            BuyerUserId = BuyerId,
            TeamAssignment = TeamAssignment.Dark,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Seller = seller,
            Buyer = buyer
        });

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(BuyerId, new BuyRequest { SessionId = SessionId });

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSessionRepository.Verify(x => x.AddOrUpdatePlayerToRosterAsync(SessionId, BuyerId, TeamAssignment.Dark, expectedPosition, 7), Times.Once);
    }

    [Theory]
    [InlineData(PositionPreference.Goalie, PositionPreference.TBD)]
    [InlineData(PositionPreference.Forward, PositionPreference.Forward)]
    public async Task ProcessSellRequest_MatchedWithBuyer_BuyerLandsAsSkater(PositionPreference preference, PositionPreference expectedPosition)
    {
        // Arrange
        var buyer = User(BuyerId, preference);
        var seller = User(SellerId, PositionPreference.Forward);
        var session = Session();
        SetupCommon(buyer, seller, SessionWithSeller());
        _mockBuySellRepository.Setup(x => x.FindMatchingBuyBuySellAsync(SessionId)).ReturnsAsync(new BuySell
        {
            BuySellId = 8,
            SessionId = SessionId,
            BuyerUserId = BuyerId,
            TeamAssignment = TeamAssignment.TBD,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Buyer = buyer
        });
        _mockBuySellRepository.Setup(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>())).ReturnsAsync(new BuySell
        {
            BuySellId = 8,
            SessionId = SessionId,
            BuyerUserId = BuyerId,
            SellerUserId = SellerId,
            TeamAssignment = TeamAssignment.Dark,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Buyer = buyer,
            Seller = seller
        });

        // Act
        var result = await _buySellService.ProcessSellRequestAsync(SellerId, new SellRequest { SessionId = SessionId });

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSessionRepository.Verify(x => x.AddOrUpdatePlayerToRosterAsync(SessionId, BuyerId, TeamAssignment.Dark, expectedPosition, 8), Times.Once);
    }

    [Theory]
    [InlineData(PositionPreference.Goalie, PositionPreference.TBD)]
    [InlineData(PositionPreference.TBD, PositionPreference.TBD)]
    [InlineData(PositionPreference.Forward, PositionPreference.Forward)]
    [InlineData(PositionPreference.Defense, PositionPreference.Defense)]
    public void ToSkaterPosition_MapsGoalieToTbdAndKeepsSkaterPositions(PositionPreference input, PositionPreference expected)
    {
        // Act & Assert
        input.ToSkaterPosition().Should().Be(expected);
    }
}
