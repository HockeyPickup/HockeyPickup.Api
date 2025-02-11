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

namespace HockeyPickup.Api.Tests.ServicesTests;

/*

Test BuySell roster scenarios:

1. Buyer buys and is matched with a Seller:

1a - Add Buyer to roster as playing

2. Buyer buys (added to queue) and is not matched with a Seller:

2a - Do nothing

3. Seller sells and is matched with a Buyer:

3a - Update Seller to not playing
3b - Add Buyer to roster as playing

4. Seller sells (added to queue) and is not matched with a Buyer:

4a - Update Seller to not playing

5. Buyer removes themselves (cancels buy) from queue

5a - Do nothing

6. Seller removes themselves (cancels sell) from queue

6a - Update Seller to playing

*/
public class BuySellServiceSessionRosterTests
{
    private readonly Mock<UserManager<AspNetUser>> _userManager;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<IBuySellRepository> _mockBuySellRepository;
    private readonly Mock<IServiceBus> _mockServiceBus;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<BuySellService>> _mockLogger;
    private readonly Mock<ISubscriptionHandler> _mockSubscriptionHandler;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly BuySellService _buySellService;

    public BuySellServiceSessionRosterTests()
    {
        // Setup UserManager mock
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

        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockBuySellRepository = new Mock<IBuySellRepository>();
        _mockServiceBus = new Mock<IServiceBus>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<BuySellService>>();
        _mockSubscriptionHandler = new Mock<ISubscriptionHandler>();
        _mockUserRepository = new Mock<IUserRepository>();

        _buySellService = new BuySellService(
            _userManager.Object,
            _mockSessionRepository.Object,
            _mockBuySellRepository.Object,
            _mockServiceBus.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockSubscriptionHandler.Object,
            _mockUserRepository.Object);

        // Common ServiceBus setup
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static AspNetUser CreateTestUser(string userId = "testUser", bool isActive = true)
    {
        return new AspNetUser
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Active = isActive,
            UserName = "testuser",
            NotificationPreference = NotificationPreference.All,
            Preferred = false,
            PreferredPlus = false,
            Rating = 1.0m,
            PhoneNumber = "1234567890"
        };
    }

    private static Session CreateTestSession(int sessionId)
    {
        var currentPacificTime = TimeZoneUtils.GetCurrentPacificTime().AddDays(4);
        currentPacificTime = new DateTime(currentPacificTime.Year, currentPacificTime.Month, currentPacificTime.Day, 7, 30, 0);

        return new Session
        {
            SessionId = sessionId,
            SessionDate = currentPacificTime,
            BuyDayMinimum = 7,
            Cost = 20.00m,
            CurrentSessionRoster = new List<Data.Entities.CurrentSessionRoster>(),
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Note = "Test Session"
        };
    }


    private static SessionDetailedResponse CreateTestSessionResponse(int sessionId = 1, string? userId = null)
    {
        var session = new SessionDetailedResponse
        {
            SessionId = sessionId,
            SessionDate = DateTime.UtcNow.AddDays(1),
            BuyDayMinimum = 6,
            Cost = 20.00m,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Note = "Test Session",
            CurrentRosters = new List<RosterPlayer>()
        };

        if (userId != null)
        {
            session.CurrentRosters.Add(new RosterPlayer
            {
                UserId = userId,
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
                SessionId = sessionId,
                SessionRosterId = 1,
                JoinedDateTime = DateTime.UtcNow,
                CurrentPosition = "Forward",
                LastBuySellId = null,
                Preferred = false,
                PreferredPlus = false
            });
        }

        return session;
    }

    private static UserDetailedResponse CreateTestUserResponse(string userId)
    {
        return new UserDetailedResponse
        {
            Id = userId,
            UserName = "testuser",
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            PhotoUrl = null,
            Rating = 1.0m,
            Active = true,
            Preferred = false,
            PreferredPlus = false
        };
    }

    // Scenario 1: Buyer buys and is matched with a Seller
    [Fact]
    public async Task BuyerMatchedWithSeller_UpdatesRosterCorrectly()
    {
        // Arrange
        var buyerId = "buyerUser";
        var sellerId = "sellerUser";
        var sessionId = 1;
        var buyer = CreateTestUser(buyerId);
        var seller = CreateTestUser(sellerId);
        var session = CreateTestSession(sessionId);
        var buyerResponse = CreateTestUserResponse(buyerId);
        var sellerResponse = CreateTestUserResponse(sellerId);
        var sessionResponse = CreateTestSessionResponse(sessionId, sellerId);

        var existingSell = new BuySell
        {
            BuySellId = 1,
            SessionId = sessionId,
            SellerUserId = sellerId,
            BuyerUserId = null,
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Seller = seller
        };

        var updatedBuySell = new BuySell
        {
            BuySellId = 1,
            SessionId = sessionId,
            SellerUserId = sellerId,
            BuyerUserId = buyerId,
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Seller = seller,
            Buyer = buyer
        };

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == buyerId))).ReturnsAsync(buyer);
        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == sellerId))).ReturnsAsync(seller);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(sessionResponse);
        _mockSessionRepository.Setup(x => x.UpdatePlayerStatusAsync(sessionId, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime>(), It.IsAny<int>())).ReturnsAsync(sessionResponse);
        _mockSessionRepository.Setup(x => x.AddOrUpdatePlayerToRosterAsync(sessionId, It.IsAny<string>(), It.IsAny<TeamAssignment>(), It.IsAny<PositionPreference>(), It.IsAny<int>())).ReturnsAsync(sessionResponse);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(sessionId)).ReturnsAsync(existingSell);
        _mockBuySellRepository.Setup(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>())).ReturnsAsync(updatedBuySell);
        _mockUserRepository.Setup(x => x.GetUserAsync(buyerId)).ReturnsAsync(buyerResponse);
        _mockUserRepository.Setup(x => x.GetUserAsync(sellerId)).ReturnsAsync(sellerResponse);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, buyerId)).ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(buyerId, new BuyRequest { SessionId = sessionId });

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSessionRepository.Verify(x => x.AddOrUpdatePlayerToRosterAsync(
            sessionId,
            buyerId,
            TeamAssignment.Light,
            It.IsAny<PositionPreference>(),
            It.IsAny<int>()), Times.Once);
    }

    // Scenario 2: Buyer buys but no matching seller
    [Fact]
    public async Task BuyerWithNoMatchingSeller_DoesNotUpdateRoster()
    {
        // Arrange
        var buyerId = "buyerUser";
        var buyerId2 = "buyerUser2";
        var sessionId = 1;
        var buyer = CreateTestUser(buyerId);
        var buyer2 = CreateTestUser(buyerId2);
        var session = CreateTestSession(sessionId);
        var buyerResponse = CreateTestUserResponse(buyerId);
        var sessionResponse = CreateTestSessionResponse(sessionId, buyerId);
        var sessionResponse2 = CreateTestSessionResponse(sessionId, buyerId2);
        sessionResponse2.CurrentRosters.Add(new RosterPlayer
        {
            UserId = buyerId2,
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
            SessionId = sessionId,
            SessionRosterId = 1,
            JoinedDateTime = DateTime.UtcNow,
            CurrentPosition = "Forward",
            LastBuySellId = null,
            Preferred = false,
            PreferredPlus = false
        });

        var newBuySell = new BuySell
        {
            BuySellId = 1,
            SessionId = sessionId,
            BuyerUserId = buyerId2,
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Buyer = buyer2
        };

        _userManager.Setup(x => x.FindByIdAsync(buyerId)).ReturnsAsync(buyer);
        _userManager.Setup(x => x.FindByIdAsync(buyerId2)).ReturnsAsync(buyer2);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(sessionResponse);
        _mockSessionRepository.Setup(x => x.UpdatePlayerStatusAsync(sessionId, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime>(), It.IsAny<int>())).ReturnsAsync(sessionResponse2);
        _mockSessionRepository.Setup(x => x.AddOrUpdatePlayerToRosterAsync(sessionId, It.IsAny<string>(), It.IsAny<TeamAssignment>(), It.IsAny<PositionPreference>(), It.IsAny<int>())).ReturnsAsync(sessionResponse2);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(sessionId)).ReturnsAsync((BuySell?) null);
        _mockBuySellRepository.Setup(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>())).ReturnsAsync(newBuySell);
        _mockUserRepository.Setup(x => x.GetUserAsync(buyerId)).ReturnsAsync(buyerResponse);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, buyerId)).ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(buyerId2, new BuyRequest { SessionId = sessionId });

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSessionRepository.Verify(x => x.AddOrUpdatePlayerToRosterAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<TeamAssignment>(),
            It.IsAny<PositionPreference>(),
            It.IsAny<int>()), Times.Never);
    }

    // Scenario 3: Seller sells and is matched with a buyer
    [Fact]
    public async Task SellerMatchedWithBuyer_UpdatesRosterCorrectly()
    {
        // Arrange
        var sellerId = "sellerUser";
        var buyerId = "buyerUser";
        var sessionId = 1;
        var seller = CreateTestUser(sellerId);
        var buyer = CreateTestUser(buyerId);
        var session = CreateTestSession(sessionId);
        var sellerResponse = CreateTestUserResponse(sellerId);
        var buyerResponse = CreateTestUserResponse(buyerId);
        var sessionResponse = CreateTestSessionResponse(sessionId, sellerId);

        var existingBuy = new BuySell
        {
            BuySellId = 1,
            SessionId = sessionId,
            BuyerUserId = buyerId,
            SellerUserId = null,
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Buyer = buyer
        };

        var updatedBuySell = new BuySell
        {
            BuySellId = 1,
            SessionId = sessionId,
            BuyerUserId = buyerId,
            SellerUserId = sellerId,
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Buyer = buyer,
            Seller = seller
        };

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == sellerId))).ReturnsAsync(seller);
        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == buyerId))).ReturnsAsync(buyer);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(sessionResponse);
        _mockSessionRepository.Setup(x => x.UpdatePlayerStatusAsync(sessionId, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime>(), It.IsAny<int>())).ReturnsAsync(sessionResponse);
        _mockSessionRepository.Setup(x => x.AddOrUpdatePlayerToRosterAsync(sessionId, It.IsAny<string>(), It.IsAny<TeamAssignment>(), It.IsAny<PositionPreference>(), It.IsAny<int>())).ReturnsAsync(sessionResponse);
        _mockBuySellRepository.Setup(x => x.FindMatchingBuyBuySellAsync(sessionId)).ReturnsAsync(existingBuy);
        _mockBuySellRepository.Setup(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>())).ReturnsAsync(updatedBuySell);
        _mockUserRepository.Setup(x => x.GetUserAsync(sellerId)).ReturnsAsync(sellerResponse);
        _mockUserRepository.Setup(x => x.GetUserAsync(buyerId)).ReturnsAsync(buyerResponse);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, sellerId)).ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessSellRequestAsync(sellerId, new SellRequest { SessionId = sessionId });

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSessionRepository.Verify(x => x.UpdatePlayerStatusAsync(
            sessionId,
            sellerId,
            false,
            It.IsAny<DateTime>(),
            It.IsAny<int>()), Times.Once);
    }

    // Scenario 4: Seller sells (added to queue) and is not matched with a Buyer
    [Fact]
    public async Task SellerWithNoMatchingBuyer_UpdatesSellerStatus()
    {
        // Arrange
        var sellerId = "sellerUser";
        var sessionId = 1;
        var seller = CreateTestUser(sellerId);
        var session = CreateTestSession(sessionId);
        var sellerResponse = CreateTestUserResponse(sellerId);
        var sessionResponse = CreateTestSessionResponse(sessionId, sellerId);
        var newBuySell = new BuySell
        {
            BuySellId = 1,
            SessionId = sessionId,
            SellerUserId = sellerId,
            BuyerUserId = null,
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Seller = seller
        };

        _userManager.Setup(x => x.FindByIdAsync(sellerId)).ReturnsAsync(seller);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(sessionResponse);
        _mockSessionRepository.Setup(x => x.UpdatePlayerStatusAsync(sessionId, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime>(), It.IsAny<int>())).ReturnsAsync(sessionResponse);
        _mockBuySellRepository.Setup(x => x.FindMatchingBuyBuySellAsync(sessionId)).ReturnsAsync((BuySell?) null);
        _mockBuySellRepository.Setup(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>())).ReturnsAsync(newBuySell);
        _mockUserRepository.Setup(x => x.GetUserAsync(sellerId)).ReturnsAsync(sellerResponse);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, sellerId)).ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessSellRequestAsync(sellerId, new SellRequest { SessionId = sessionId });

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSessionRepository.Verify(x => x.UpdatePlayerStatusAsync(
            sessionId,
            sellerId,
            false,
            It.IsAny<DateTime?>(),
            It.IsAny<int>()),
            Times.Once);
    }

    // Scenario 5: Buyer removes themselves (cancels buy) from queue
    [Fact]
    public async Task BuyerRemovesThemselves_DoesNotUpdateRoster()
    {
        // Arrange
        var buyerId = "buyerUser";
        var sessionId = 1;
        var buyer = CreateTestUser(buyerId);
        var session = CreateTestSession(sessionId);
        var buyerResponse = CreateTestUserResponse(buyerId);
        var sessionResponse = CreateTestSessionResponse(sessionId);
        var existingBuySell = new BuySell
        {
            BuySellId = 1,
            SessionId = sessionId,
            BuyerUserId = buyerId,
            SellerUserId = null,
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Buyer = buyer
        };

        _userManager.Setup(x => x.FindByIdAsync(buyerId)).ReturnsAsync(buyer);
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(sessionId)).ReturnsAsync(existingBuySell);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(sessionResponse);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, buyerId)).ReturnsAsync(new List<BuySell> { existingBuySell });
        _mockBuySellRepository.Setup(x => x.DeleteBuySellAsync(existingBuySell.BuySellId, It.IsAny<string>())).Returns(Task.FromResult(true));
        _mockSessionRepository.Setup(x => x.AddOrUpdatePlayerToRosterAsync(sessionId, It.IsAny<string>(), It.IsAny<TeamAssignment>(), It.IsAny<PositionPreference>(), It.IsAny<int>())).ReturnsAsync(sessionResponse);
        _mockSessionRepository.Setup(x => x.UpdatePlayerStatusAsync(sessionId, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime>(), It.IsAny<int>())).ReturnsAsync(sessionResponse);

        // Act
        var result = await _buySellService.CancelBuyAsync(buyerId, sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSessionRepository.Verify(x => x.AddOrUpdatePlayerToRosterAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<TeamAssignment>(),
            It.IsAny<PositionPreference>(),
            It.IsAny<int>()),
            Times.Never);
        _mockSessionRepository.Verify(x => x.UpdatePlayerStatusAsync(
            sessionId,
            It.IsAny<string>(),
            false,
            It.IsAny<DateTime?>(),
            It.IsAny<int>()),
            Times.Never);
    }

    // Scenario 6: Seller removes themselves (cancels sell) from queue
    [Fact]
    public async Task SellerRemovesThemselves_UpdatesSellerStatus()
    {
        // Arrange
        var sellerId = "sellerUser";
        var sessionId = 1;
        var seller = CreateTestUser(sellerId);
        var session = CreateTestSession(sessionId);
        var sellerResponse = CreateTestUserResponse(sellerId);
        var sessionResponse = CreateTestSessionResponse(sessionId, sellerId);
        var existingBuySell = new BuySell
        {
            BuySellId = 1,
            SessionId = sessionId,
            SellerUserId = sellerId,
            BuyerUserId = null,
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Seller = seller
        };

        _userManager.Setup(x => x.FindByIdAsync(sellerId)).ReturnsAsync(seller);
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(sessionId)).ReturnsAsync(existingBuySell);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(sessionResponse);
        _mockSessionRepository.Setup(x => x.UpdatePlayerStatusAsync(sessionId, It.IsAny<string>(), It.IsAny<bool>(), null, null)).ReturnsAsync(sessionResponse);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, sellerId)).ReturnsAsync(new List<BuySell> { existingBuySell });
        _mockBuySellRepository.Setup(x => x.DeleteBuySellAsync(existingBuySell.BuySellId, It.IsAny<string>())).Returns(Task.FromResult(true));
        _mockUserRepository.Setup(x => x.GetUserAsync(sellerId)).ReturnsAsync(sellerResponse);

        // Act
        var result = await _buySellService.CancelSellAsync(sellerId, sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSessionRepository.Verify(x => x.UpdatePlayerStatusAsync(
            sessionId,
            It.IsAny<string>(),
            true,
            null,
            null),
            Times.Once);
    }

    // Scenario 6a: Seller removes themselves but for some reason isn't on the roster
    [Fact]
    public async Task SellerRemovesThemselvesNotOnRoster()
    {
        // Arrange
        var sellerId = "sellerUser";
        var sessionId = 1;
        var seller = CreateTestUser(sellerId);
        var session = CreateTestSession(sessionId);
        var sellerResponse = CreateTestUserResponse(sellerId);
        var sessionResponse = CreateTestSessionResponse(sessionId, sellerId);
        var existingBuySell = new BuySell
        {
            BuySellId = 1,
            SessionId = sessionId,
            SellerUserId = sellerId,
            BuyerUserId = null,
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Session = session,
            Seller = seller
        };
        sessionResponse.CurrentRosters = null;

        _userManager.Setup(x => x.FindByIdAsync(sellerId)).ReturnsAsync(seller);
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(sessionId)).ReturnsAsync(existingBuySell);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(sessionResponse);
        _mockSessionRepository.Setup(x => x.UpdatePlayerStatusAsync(sessionId, It.IsAny<string>(), It.IsAny<bool>(), null, null)).ReturnsAsync(sessionResponse);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, sellerId)).ReturnsAsync(new List<BuySell> { existingBuySell });
        _mockBuySellRepository.Setup(x => x.DeleteBuySellAsync(existingBuySell.BuySellId, It.IsAny<string>())).Returns(Task.FromResult(true));
        _mockUserRepository.Setup(x => x.GetUserAsync(sellerId)).ReturnsAsync(sellerResponse);

        // Act
        var result = await _buySellService.CancelSellAsync(sellerId, sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSessionRepository.Verify(x => x.UpdatePlayerStatusAsync(
            sessionId,
            It.IsAny<string>(),
            true,
            null,
            null),
            Times.Never);
    }
}
