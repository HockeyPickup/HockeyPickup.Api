#pragma warning disable IDE0045 // Convert to conditional expression
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
using RosterPlayer = HockeyPickup.Api.Models.Responses.RosterPlayer;

namespace HockeyPickup.Api.Tests.ServicesTests;

public class BuySellServiceTests
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

    public BuySellServiceTests()
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

    private static SessionDetailedResponse CreateTestSessionDetailedResponse(int sessionId = 1)
    {
        var currentPacificTime = TimeZoneUtils.GetCurrentPacificTime().AddDays(4);
        currentPacificTime = new DateTime(currentPacificTime.Year, currentPacificTime.Month, currentPacificTime.Day, 7, 30, 0);

        return new SessionDetailedResponse
        {
            SessionId = sessionId,
            SessionDate = currentPacificTime,
            BuyDayMinimum = 6,
            Cost = 20.00m,
            CurrentRosters = new List<RosterPlayer>(),
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            Note = "Test Session"
        };
    }

    private static Session CreateTestSession(int sessionId = 1)
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

    private static BuySell CreateTestBuySell(
        int buySellId = 1,
        string? buyerId = null,
        string? sellerId = null,
        bool paymentSent = false,
        bool paymentReceived = false,
        PaymentMethodType? paymentMethod = null,
        bool includeNavigationProperties = true)
    {
        var buySell = new BuySell
        {
            BuySellId = buySellId,
            SessionId = 1,
            BuyerUserId = buyerId,
            SellerUserId = sellerId,
            Price = 20.00m,
            PaymentSent = paymentSent,
            PaymentReceived = paymentReceived,
            PaymentMethod = paymentMethod,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            TeamAssignment = TeamAssignment.TBD,
            CreateByUserId = buyerId ?? sellerId ?? "system",
            UpdateByUserId = buyerId ?? sellerId ?? "system",
            SellerNote = "",
            BuyerNote = "",
            SellerNoteFlagged = false,
            BuyerNoteFlagged = false
        };

        if (includeNavigationProperties)
        {
            buySell.Buyer = buyerId != null ? CreateTestUser(buyerId) : null;
            buySell.Seller = sellerId != null ? CreateTestUser(sellerId) : null;
            buySell.Session = CreateTestSession();
        }

        return buySell;
    }

    private static List<RosterPlayer> CreateFullRosterPlayer(string userId)
    {
        return new List<RosterPlayer> 
        { 
            new RosterPlayer 
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

    [Fact]
    public async Task ProcessBuyRequestAsync_Success_WithNoExistingSell()
    {
        // Arrange
        var userId = "testUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var userResponse = CreateTestUserResponse(userId);

        // Setup session repository
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(session);

        // Setup user manager
        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin"))
            .ReturnsAsync(false);

        // Setup BuySell repository
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(request.SessionId))
            .ReturnsAsync((BuySell?) null);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());
        _mockBuySellRepository.Setup(x => x.GetQueuePositionAsync(It.IsAny<int>()))
            .ReturnsAsync(1);

        // Setup user repository
        _mockUserRepository.Setup(x => x.GetUserAsync(userId))
            .ReturnsAsync(userResponse);

        // Setup ServiceBus
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"])
            .Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"])
            .Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockBuySell = CreateTestBuySell(buyerId: userId);
        _mockBuySellRepository.Setup(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()))
            .ReturnsAsync(mockBuySell);

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.BuyerUserId.Should().Be(userId);
        _mockBuySellRepository.Verify(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_Fails_WhenSessionInPast()
    {
        // Arrange
        var userId = "testUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        session.SessionDate = TimeZoneUtils.GetCurrentPacificTime().AddDays(-1); // Past session

        _userManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Cannot buy for a session that has already started");
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_Fails_WhenUserNotActive()
    {
        // Arrange
        var userId = "testUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId, isActive: false);
        var session = CreateTestSessionDetailedResponse();

        _userManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("User is not active");
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_Success_WithExistingSell()
    {
        // Arrange
        var userId = "testUser";
        var sellerId = "sellerUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId);
        var seller = CreateTestUser(sellerId);
        var session = CreateTestSessionDetailedResponse();
        var userResponse = CreateTestUserResponse(userId);
        var sellerResponse = CreateTestUserResponse(sellerId);

        // Add seller to session roster
        session.CurrentRosters = new List<RosterPlayer>
    {
        new RosterPlayer
        {
        UserId = sellerId,
        TeamAssignment = TeamAssignment.Light,
        IsPlaying = true,
        FirstName = "Test",
        LastName = "Seller",
        Email = "seller@test.com",
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

        var existingSell = CreateTestBuySell(sellerId: sellerId);
        var updatedBuySell = CreateTestBuySell(buyerId: userId, sellerId: sellerId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == sellerId))).ReturnsAsync(seller);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(request.SessionId)).ReturnsAsync(existingSell);
        _mockBuySellRepository.Setup(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>())).ReturnsAsync(updatedBuySell);
        _mockUserRepository.Setup(x => x.GetUserAsync(userId)).ReturnsAsync(userResponse);
        _mockUserRepository.Setup(x => x.GetUserAsync(sellerId)).ReturnsAsync(sellerResponse);
        _mockBuySellRepository.Setup(x => x.GetQueuePositionAsync(It.IsAny<int>())).ReturnsAsync(1);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Setup ServiceBus
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.BuyerUserId.Should().Be(userId);
        result.Data.SellerUserId.Should().Be(sellerId);
        _mockBuySellRepository.Verify(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()), Times.Once);
    }
    [Fact]
    public async Task ProcessSellRequestAsync_Success_WithNoExistingBuy()
    {
        // Arrange
        var userId = "testUser";
        var request = new SellRequest { SessionId = 1, Note = "Test sell" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var userResponse = CreateTestUserResponse(userId);

        // Add seller to session roster as active player
        session.CurrentRosters = new List<RosterPlayer>
    {
        new RosterPlayer
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
            SessionId = 1,
            SessionRosterId = 1,
            JoinedDateTime = DateTime.UtcNow,
            CurrentPosition = "Forward",
            LastBuySellId = null,
            Preferred = false,
            PreferredPlus = false
        }
    };

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingBuyBuySellAsync(request.SessionId)).ReturnsAsync((BuySell?) null);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());
        _mockUserRepository.Setup(x => x.GetUserAsync(userId)).ReturnsAsync(userResponse);

        var mockBuySell = CreateTestBuySell(sellerId: userId);
        _mockBuySellRepository.Setup(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()))
            .ReturnsAsync(mockBuySell);

        // Setup ServiceBus
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _buySellService.ProcessSellRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.SellerUserId.Should().Be(userId);
        _mockBuySellRepository.Verify(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessSellRequestAsync_Fails_WhenUserNotOnRoster()
    {
        // Arrange
        var userId = "testUser";
        var request = new SellRequest { SessionId = 1, Note = "Test sell" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();

        // Empty roster - user not on it
        session.CurrentRosters = new List<RosterPlayer>();

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessSellRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("You must be on the roster to sell your spot");
    }

    [Fact]
    public async Task ProcessSellRequestAsync_Fails_WhenUserAlreadyHasActiveSell()
    {
        // Arrange
        var userId = "testUser";
        var request = new SellRequest { SessionId = 1, Note = "Test sell" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();

        // Add user to roster
        session.CurrentRosters = new List<RosterPlayer>
    {
        new RosterPlayer
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
            SessionId = 1,
            SessionRosterId = 1,
            JoinedDateTime = DateTime.UtcNow,
            CurrentPosition = "Forward",
            LastBuySellId = null,
            Preferred = false,
            PreferredPlus = false
        }
    };

        // Setup existing active sell
        var existingSell = CreateTestBuySell(sellerId: userId);
        var existingSells = new List<BuySell> { existingSell };

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(existingSells);

        // Act
        var result = await _buySellService.ProcessSellRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("You already have an active Sell for this session");
    }

    [Fact]
    public async Task ProcessSellRequestAsync_Success_WithExistingBuy()
    {
        // Arrange
        var sellerId = "testUser";
        var buyerId = "buyerUser";
        var request = new SellRequest { SessionId = 1, Note = "Test sell" };
        var seller = CreateTestUser(sellerId);
        var buyer = CreateTestUser(buyerId);
        var session = CreateTestSessionDetailedResponse();
        var sellerResponse = CreateTestUserResponse(sellerId);
        var buyerResponse = CreateTestUserResponse(buyerId);

        // Add seller to session roster
        session.CurrentRosters = new List<RosterPlayer>
    {
        new RosterPlayer
        {
            UserId = sellerId,
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
            LastBuySellId = null,
            Preferred = false,
            PreferredPlus = false
        }
    };

        var existingBuy = CreateTestBuySell(buyerId: buyerId);
        var updatedBuySell = CreateTestBuySell(buyerId: buyerId, sellerId: sellerId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == sellerId))).ReturnsAsync(seller);
        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == buyerId))).ReturnsAsync(buyer);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingBuyBuySellAsync(request.SessionId)).ReturnsAsync(existingBuy);
        _mockBuySellRepository.Setup(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>())).ReturnsAsync(updatedBuySell);
        _mockUserRepository.Setup(x => x.GetUserAsync(sellerId)).ReturnsAsync(sellerResponse);
        _mockUserRepository.Setup(x => x.GetUserAsync(buyerId)).ReturnsAsync(buyerResponse);
        _mockBuySellRepository.Setup(x => x.GetQueuePositionAsync(It.IsAny<int>())).ReturnsAsync(1);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, sellerId))
            .ReturnsAsync(new List<BuySell>());

        // Setup ServiceBus
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _buySellService.ProcessSellRequestAsync(sellerId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.BuyerUserId.Should().Be(buyerId);
        result.Data.SellerUserId.Should().Be(sellerId);
        _mockBuySellRepository.Verify(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()), Times.Once);
    }


    [Fact]
    public async Task ConfirmPaymentSentAsync_Success()
    {
        // Arrange
        var userId = "buyerUser";
        var sellerId = "sellerUser";
        var buySellId = 1;
        var paymentMethod = PaymentMethodType.Venmo;

        var buyer = CreateTestUser(userId);
        var seller = CreateTestUser(sellerId);
        var buySell = CreateTestBuySell(buySellId, userId, sellerId);
        var updatedBuySell = CreateTestBuySell(buySellId, userId, sellerId, paymentSent: true, paymentMethod: paymentMethod);
        var session = CreateTestSessionDetailedResponse();
        var buyerResponse = CreateTestUserResponse(userId);
        var sellerResponse = CreateTestUserResponse(sellerId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(buyer);
        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == sellerId))).ReturnsAsync(seller);
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockBuySellRepository.Setup(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>())).ReturnsAsync(updatedBuySell);
        _mockUserRepository.Setup(x => x.GetUserAsync(userId)).ReturnsAsync(buyerResponse);
        _mockUserRepository.Setup(x => x.GetUserAsync(sellerId)).ReturnsAsync(sellerResponse);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(buySell.SessionId)).ReturnsAsync(session);

        // Setup ServiceBus
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _buySellService.ConfirmPaymentSentAsync(userId, buySellId, paymentMethod);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.PaymentSent.Should().BeTrue();
        result.Data.PaymentMethod.Should().Be(paymentMethod);
        _mockBuySellRepository.Verify(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmPaymentReceivedAsync_Success()
    {
        // Arrange
        var buyerId = "buyerUser";
        var sellerId = "sellerUser";
        var buySellId = 1;
        var paymentMethod = PaymentMethodType.Venmo;

        var buyer = CreateTestUser(buyerId);
        var seller = CreateTestUser(sellerId);
        var buySell = CreateTestBuySell(buySellId, buyerId, sellerId, paymentSent: true, paymentMethod: paymentMethod);
        var updatedBuySell = CreateTestBuySell(buySellId, buyerId, sellerId, paymentSent: true, paymentReceived: true, paymentMethod: paymentMethod);
        var session = CreateTestSessionDetailedResponse();
        var buyerResponse = CreateTestUserResponse(buyerId);
        var sellerResponse = CreateTestUserResponse(sellerId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == buyerId))).ReturnsAsync(buyer);
        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == sellerId))).ReturnsAsync(seller);
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockBuySellRepository.Setup(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>())).ReturnsAsync(updatedBuySell);
        _mockUserRepository.Setup(x => x.GetUserAsync(buyerId)).ReturnsAsync(buyerResponse);
        _mockUserRepository.Setup(x => x.GetUserAsync(sellerId)).ReturnsAsync(sellerResponse);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(buySell.SessionId)).ReturnsAsync(session);

        // Setup ServiceBus
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _buySellService.ConfirmPaymentReceivedAsync(sellerId, buySellId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.PaymentReceived.Should().BeTrue();
        result.Data.PaymentSent.Should().BeTrue();
        result.Data.PaymentMethod.Should().Be(paymentMethod);
        _mockBuySellRepository.Verify(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UnconfirmPaymentSentAsync_Success()
    {
        // Arrange
        var buyerId = "buyerUser";
        var sellerId = "sellerUser";
        var buySellId = 1;
        var paymentMethod = PaymentMethodType.Venmo;

        var buyer = CreateTestUser(buyerId);
        var seller = CreateTestUser(sellerId);
        var buySell = CreateTestBuySell(buySellId, buyerId, sellerId, paymentSent: true, paymentMethod: paymentMethod);
        var updatedBuySell = CreateTestBuySell(buySellId, buyerId, sellerId, paymentSent: false, paymentMethod: paymentMethod);
        var session = CreateTestSessionDetailedResponse();

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == buyerId))).ReturnsAsync(buyer);
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockBuySellRepository.Setup(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>())).ReturnsAsync(updatedBuySell);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(buySell.SessionId)).ReturnsAsync(session);

        // Act
        var result = await _buySellService.UnconfirmPaymentSentAsync(buyerId, buySellId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.PaymentSent.Should().BeFalse();
        _mockBuySellRepository.Verify(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UnconfirmPaymentReceivedAsync_Success()
    {
        // Arrange
        var buyerId = "buyerUser";
        var sellerId = "sellerUser";
        var buySellId = 1;
        var paymentMethod = PaymentMethodType.Venmo;

        var seller = CreateTestUser(sellerId);
        var buySell = CreateTestBuySell(buySellId, buyerId, sellerId, paymentSent: true, paymentReceived: true, paymentMethod: paymentMethod);
        var updatedBuySell = CreateTestBuySell(buySellId, buyerId, sellerId, paymentSent: true, paymentReceived: false, paymentMethod: paymentMethod);
        var session = CreateTestSessionDetailedResponse();

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == sellerId))).ReturnsAsync(seller);
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockBuySellRepository.Setup(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>())).ReturnsAsync(updatedBuySell);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(buySell.SessionId)).ReturnsAsync(session);

        // Act
        var result = await _buySellService.UnconfirmPaymentReceivedAsync(sellerId, buySellId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.PaymentReceived.Should().BeFalse();
        _mockBuySellRepository.Verify(x => x.UpdateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CancelBuyAsync_Success()
    {
        // Arrange
        var buyerId = "buyerUser";
        var buySellId = 1;

        var buyer = CreateTestUser(buyerId);
        var buySell = CreateTestBuySell(buySellId, buyerId: buyerId);
        var session = CreateTestSessionDetailedResponse();

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == buyerId))).ReturnsAsync(buyer);
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockBuySellRepository.Setup(x => x.DeleteBuySellAsync(buySellId, It.IsAny<string>())).ReturnsAsync(true);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(buySell.SessionId)).ReturnsAsync(session);

        // Setup ServiceBus
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _buySellService.CancelBuyAsync(buyerId, buySellId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        _mockBuySellRepository.Verify(x => x.DeleteBuySellAsync(buySellId, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CancelSellAsync_Success()
    {
        // Arrange
        var sellerId = "sellerUser";
        var buySellId = 1;

        var seller = CreateTestUser(sellerId);
        var buySell = CreateTestBuySell(buySellId, sellerId: sellerId);
        var session = CreateTestSessionDetailedResponse();

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == sellerId))).ReturnsAsync(seller);
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockBuySellRepository.Setup(x => x.DeleteBuySellAsync(buySellId, It.IsAny<string>())).ReturnsAsync(true);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(buySell.SessionId)).ReturnsAsync(session);

        // Setup ServiceBus
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _buySellService.CancelSellAsync(sellerId, buySellId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        _mockBuySellRepository.Verify(x => x.DeleteBuySellAsync(buySellId, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GetBuySellAsync_Success()
    {
        // Arrange
        var buyerId = "buyerUser";
        var sellerId = "sellerUser";
        var buySellId = 1;
        var paymentMethod = PaymentMethodType.Venmo;

        var buyer = CreateTestUser(buyerId);
        var seller = CreateTestUser(sellerId);
        var buySell = CreateTestBuySell(buySellId, buyerId, sellerId, paymentMethod: paymentMethod);
        var buyerResponse = CreateTestUserResponse(buyerId);
        var sellerResponse = CreateTestUserResponse(sellerId);

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockUserRepository.Setup(x => x.GetUserAsync(buyerId)).ReturnsAsync(buyerResponse);
        _mockUserRepository.Setup(x => x.GetUserAsync(sellerId)).ReturnsAsync(sellerResponse);
        _mockBuySellRepository.Setup(x => x.GetQueuePositionAsync(buySellId)).ReturnsAsync(1);

        // Act
        var result = await _buySellService.GetBuySellAsync(buySellId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.BuySellId.Should().Be(buySellId);
        result.Data.BuyerUserId.Should().Be(buyerId);
        result.Data.SellerUserId.Should().Be(sellerId);
        result.Data.QueuePosition.Should().Be(1);
    }

    [Fact]
    public async Task GetSessionBuySellsAsync_Success()
    {
        // Arrange
        var sessionId = 1;
        var buyerId = "buyerUser";
        var sellerId = "sellerUser";
        var buySellId = 1;

        var buyer = CreateTestUser(buyerId);
        var seller = CreateTestUser(sellerId);
        var buySell = CreateTestBuySell(buySellId, buyerId, sellerId);
        var buyerResponse = CreateTestUserResponse(buyerId);
        var sellerResponse = CreateTestUserResponse(sellerId);

        var buySells = new List<BuySell> { buySell };

        _mockBuySellRepository.Setup(x => x.GetSessionBuySellsAsync(sessionId)).ReturnsAsync(buySells);
        _mockUserRepository.Setup(x => x.GetUserAsync(buyerId)).ReturnsAsync(buyerResponse);
        _mockUserRepository.Setup(x => x.GetUserAsync(sellerId)).ReturnsAsync(sellerResponse);
        _mockBuySellRepository.Setup(x => x.GetQueuePositionAsync(buySellId)).ReturnsAsync(1);

        // Act
        var result = await _buySellService.GetSessionBuySellsAsync(sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(1);
        result.Data.First().BuySellId.Should().Be(buySellId);
        result.Data.First().BuyerUserId.Should().Be(buyerId);
        result.Data.First().SellerUserId.Should().Be(sellerId);
        result.Data.First().QueuePosition.Should().Be(1);
    }

    [Fact]
    public async Task GetUserBuySellsAsync_Success()
    {
        // Arrange
        var userId = "testUser";
        var buySellId = 1;

        var user = CreateTestUser(userId);
        var buySell = CreateTestBuySell(buySellId, buyerId: userId);
        var userResponse = CreateTestUserResponse(userId);

        var buySells = new List<BuySell> { buySell };

        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(userId)).ReturnsAsync(buySells);
        _mockUserRepository.Setup(x => x.GetUserAsync(userId)).ReturnsAsync(userResponse);
        _mockBuySellRepository.Setup(x => x.GetQueuePositionAsync(buySellId)).ReturnsAsync(1);

        // Act
        var result = await _buySellService.GetUserBuySellsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(1);
        result.Data.First().BuySellId.Should().Be(buySellId);
        result.Data.First().BuyerUserId.Should().Be(userId);
        result.Data.First().QueuePosition.Should().Be(1);
    }

    [Fact]
    public async Task GetBuySellAsync_ReturnsFailure_WhenNotFound()
    {
        // Arrange
        var buySellId = 1;
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync((BuySell?) null);

        // Act
        var result = await _buySellService.GetBuySellAsync(buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("BuySell not found");
    }

    [Fact]
    public async Task GetSessionBuySellsAsync_ReturnsEmptyList_WhenNoneFound()
    {
        // Arrange
        var sessionId = 1;
        _mockBuySellRepository.Setup(x => x.GetSessionBuySellsAsync(sessionId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.GetSessionBuySellsAsync(sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserBuySellsAsync_ReturnsEmptyList_WhenNoneFound()
    {
        // Arrange
        var userId = "testUser";
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.GetUserBuySellsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task CanBuyAsync_ReturnsFalse_WhenSessionNotFound()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync((SessionDetailedResponse)null!);

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("Session not found");
    }

    [Fact]
    public async Task CanBuyAsync_ReturnsFalse_WhenUserNotFound()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var session = CreateTestSessionDetailedResponse(sessionId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync((AspNetUser?) null);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("User not found");
    }

    [Fact]
    public async Task CanBuyAsync_ReturnsFalse_WhenUserAlreadyBuyingSession()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var existingBuySell = CreateTestBuySell(1, buyerId: userId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId)).ReturnsAsync(new List<BuySell> { existingBuySell });

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Contain("You already have an active Buy for this session");
    }

    [Fact]
    public async Task CanBuyAsync_ReturnsFalse_WhenUserAlreadySellingSession()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var existingBuySell = CreateTestBuySell(1, sellerId: userId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId)).ReturnsAsync(new List<BuySell> { existingBuySell });

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Contain("You have an active Sell for this session");
    }

    [Fact]
    public async Task CanBuyAsync_ReturnsFalse_WhenUserAlreadyInSession()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = new List<RosterPlayer> 
        { 
            new RosterPlayer 
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
                SessionId = 1,
                SessionRosterId = 1,
                JoinedDateTime = DateTime.UtcNow,
                CurrentPosition = "Forward",
                LastBuySellId = null,
                Preferred = false,
                PreferredPlus = false
            } 
        };        
        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId)).ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Contain("already on the");
    }

    [Fact]
    public async Task CancelBuyAsync_ReturnsFalse_WhenBuySellNotFound()
    {
        // Arrange
        var buyerId = "buyerUser";
        var buySellId = 1;
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync((BuySell?)null);

        // Act
        var result = await _buySellService.CancelBuyAsync(buyerId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("BuySell not found");
    }

    [Fact]
    public async Task CancelBuyAsync_ReturnsFalse_WhenSessionNotFound()
    {
        // Arrange
        var buyerId = "buyerUser";
        var buySellId = 1;
        var buySell = CreateTestBuySell(buySellId, buyerId: buyerId);

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(buySell.SessionId))!.ReturnsAsync((SessionDetailedResponse?)null);

        // Act
        var result = await _buySellService.CancelBuyAsync(buyerId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Session not found");
    }

    [Fact]
    public async Task CancelBuyAsync_ReturnsFalse_WhenNotAuthorized()
    {
        // Arrange
        var buyerId = "buyerUser";
        var wrongUserId = "wrongUser";
        var buySellId = 1;
        var buySell = CreateTestBuySell(buySellId, buyerId: buyerId);
        var session = CreateTestSessionDetailedResponse();

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(buySell.SessionId)).ReturnsAsync(session);

        // Act
        var result = await _buySellService.CancelBuyAsync(wrongUserId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Not authorized to cancel this BuySell");
    }

    [Fact]
    public async Task CancelBuyAsync_ReturnsFalse_WhenAlreadyBought()
    {
        // Arrange
        var buyerId = "buyerUser";
        var buySellId = 1;
        var buySell = CreateTestBuySell(buySellId, buyerId: buyerId, sellerId: "sellerUser");
        var session = CreateTestSessionDetailedResponse();

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(buySell.SessionId)).ReturnsAsync(session);

        // Act
        var result = await _buySellService.CancelBuyAsync(buyerId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Cannot cancel spot that is already bought");
    }

    [Fact]
    public async Task CancelSellAsync_ReturnsFalse_WhenBuySellNotFound()
    {
        // Arrange
        var sellerId = "sellerUser";
        var buySellId = 1;
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync((BuySell?)null);

        // Act
        var result = await _buySellService.CancelSellAsync(sellerId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("BuySell not found");
    }

    [Fact]
    public async Task CancelSellAsync_ReturnsFalse_WhenSessionNotFound()
    {
        // Arrange
        var sellerId = "sellerUser";
        var buySellId = 1;
        var buySell = CreateTestBuySell(buySellId, sellerId: sellerId);

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(buySell.SessionId))!.ReturnsAsync((SessionDetailedResponse?)null);

        // Act
        var result = await _buySellService.CancelSellAsync(sellerId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Session not found");
    }

    [Fact]
    public async Task CancelSellAsync_ReturnsFalse_WhenNotAuthorized()
    {
        // Arrange
        var sellerId = "sellerUser";
        var wrongUserId = "wrongUser";
        var buySellId = 1;
        var buySell = CreateTestBuySell(buySellId, sellerId: sellerId);
        var session = CreateTestSessionDetailedResponse();

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(buySell.SessionId)).ReturnsAsync(session);

        // Act
        var result = await _buySellService.CancelSellAsync(wrongUserId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Not authorized to cancel this BuySell");
    }

    [Fact]
    public async Task CancelSellAsync_ReturnsFalse_WhenAlreadySold()
    {
        // Arrange
        var sellerId = "sellerUser";
        var buySellId = 1;
        var buySell = CreateTestBuySell(buySellId, sellerId: sellerId, buyerId: "buyerUser");
        var session = CreateTestSessionDetailedResponse();

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId)).ReturnsAsync(buySell);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(buySell.SessionId)).ReturnsAsync(session);

        // Act
        var result = await _buySellService.CancelSellAsync(sellerId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Cannot cancel spot that is already sold");
    }

    [Fact]
    public async Task CanBuyAsync_ReturnsTrue_WhenUserIsAdmin()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(true);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId)).ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanBuyAsync_ReturnsTrue_WhenUserIsPreferredPlus()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        user.PreferredPlus = true;
        var session = CreateTestSessionDetailedResponse();

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId)).ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanBuyAsync_ReturnsTrue_WhenUserIsPreferred()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        user.Preferred = true;
        var session = CreateTestSessionDetailedResponse();

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId)).ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanBuyAsync_ReturnsFalse_WhenOutsideBuyWindow()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        session.SessionDate = TimeZoneUtils.GetCurrentPacificTime().AddDays(3); // Session in 3 days
        session.BuyDayMinimum = 1; // Can only buy 1 day before session

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId)).ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Contain("buy window");
    }

    [Fact]
    public async Task CanSellAsync_ReturnsTrue_WhenUserIsAdmin()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = new List<RosterPlayer> 
        {
            new RosterPlayer
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
                SessionId = 1,
                SessionRosterId = 1,
                JoinedDateTime = DateTime.UtcNow,
                CurrentPosition = "Forward",
                LastBuySellId = null,
                Preferred = false,
                PreferredPlus = false
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(true);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId)).ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanSellAsync_ReturnsTrue_WhenUserIsPreferredPlus()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        user.PreferredPlus = true;
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = new List<RosterPlayer> 
        { 
            new RosterPlayer 
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
                SessionId = 1,
                SessionRosterId = 1,
                JoinedDateTime = DateTime.UtcNow,
                CurrentPosition = "Forward",
                LastBuySellId = null,
                Preferred = false,
                PreferredPlus = false
            } 
        };

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId)).ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanSellAsync_ReturnsTrue_WhenUserIsPreferred()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        user.Preferred = true;
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = new List<RosterPlayer> 
        { 
            new RosterPlayer 
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
                SessionId = 1,
                SessionRosterId = 1,
                JoinedDateTime = DateTime.UtcNow,
                CurrentPosition = "Forward",
                LastBuySellId = null,
                Preferred = false,
                PreferredPlus = false
            } 
        };

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId)).ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanSellAsync_ReturnsFalse_WhenUserNotOnRoster()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = new List<RosterPlayer>(); // Empty roster

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("You must be on the roster to sell your spot");
    }

    [Fact]
    public async Task CanSellAsync_ReturnsFalse_WhenUserAlreadySelling()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = CreateFullRosterPlayer(userId);
        var existingSell = CreateTestBuySell(1, sellerId: userId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId))
            .ReturnsAsync(new List<BuySell> { existingSell });

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("You already have an active Sell for this session");
    }

    [Fact]
    public async Task CanSellAsync_ReturnsFalse_WhenUserAlreadyBuying()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = CreateFullRosterPlayer(userId);
        var existingBuy = CreateTestBuySell(1, buyerId: userId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId))
            .ReturnsAsync(new List<BuySell> { existingBuy });

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("You have an active Buy for this session");
    }

    [Fact]
    public async Task CanSellAsync_ReturnsFalse_WhenSessionInPast()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        session.SessionDate = TimeZoneUtils.GetCurrentPacificTime().AddDays(-1); // Session in past
        session.CurrentRosters = CreateFullRosterPlayer(userId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("Cannot sell for a session that has already started");
    }    

    [Fact]
    public async Task CanSellAsync_ReturnsFalse_WhenSessionNotFound()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))!.ReturnsAsync((SessionDetailedResponse?)null);

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("Session not found");
    }

    [Fact]
    public async Task CanSellAsync_ReturnsFalse_WhenUserNotFound()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var session = CreateTestSessionDetailedResponse();

        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync((AspNetUser?)null);

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("User not found");
    }

    [Fact]
    public async Task CanSellAsync_ReturnsFalse_WhenUserNotActive()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId, isActive: false);  // Set user as inactive
        var session = CreateTestSessionDetailedResponse();

        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("User is not active");
    }

    [Fact]
    public async Task CanSellAsync_HandlesException()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error checking sell eligibility for user");
    }

    [Fact]
    public async Task CanBuyAsync_HandlesException()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error checking buy eligibility for user");
    }

    [Fact]
    public async Task CancelBuyAsync_HandlesException()
    {
        // Arrange
        var userId = "testUser";
        var buySellId = 1;
        
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _buySellService.CancelBuyAsync(userId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error cancelling Buy BuySell");
    }

    [Fact]
    public async Task CancelSellAsync_HandlesException()
    {
        // Arrange
        var userId = "testUser";
        var buySellId = 1;
        
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _buySellService.CancelSellAsync(userId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error cancelling Sell BuySell");
    }

    [Fact]
    public async Task ConfirmPaymentSentAsync_HandlesException()
    {
        // Arrange
        var userId = "testUser";
        var buySellId = 1;
        
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _buySellService.ConfirmPaymentSentAsync(userId, buySellId, PaymentMethodType.Unknown);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error confirming payment sent");
    }

    [Fact]
    public async Task ConfirmPaymentReceivedAsync_HandlesException()
    {
        // Arrange
        var userId = "testUser";
        var buySellId = 1;
        
        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _buySellService.ConfirmPaymentReceivedAsync(userId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error confirming payment received");
    }

    [Fact]
    public async Task UnconfirmPaymentSentAsync_HandlesException()
    {
        // Arrange
        var userId = "testUser";
        var buySellId = 1;

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _buySellService.UnconfirmPaymentSentAsync(userId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error unconfirming payment sent");
    }

    [Fact]
    public async Task UnconfirmPaymentReceivedAsync_HandlesException()
    {
        // Arrange
        var userId = "testUser";
        var buySellId = 1;

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _buySellService.UnconfirmPaymentReceivedAsync(userId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error unconfirming payment received");
    }

    [Fact]
    public async Task GetBuySellAsync_HandlesException()
    {
        // Arrange
        var buySellId = 1;

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _buySellService.GetBuySellAsync(buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error getting BuySell");
    }

    [Fact]
    public async Task GetSessionBuySellsAsync_HandlesException()
    {
        // Arrange
        var sessionId = 1;

        _mockBuySellRepository.Setup(x => x.GetSessionBuySellsAsync(sessionId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _buySellService.GetSessionBuySellsAsync(sessionId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error getting BuySells for Session");
    }

    [Fact]
    public async Task GetUserBuySellsAsync_HandlesException()
    {
        // Arrange
        var userId = "testUser";

        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(userId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _buySellService.GetUserBuySellsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error getting BuySells for User");
    }

    [Fact]
    public async Task UnconfirmPaymentSentAsync_ReturnsFalse_WhenBuySellNotFound()
    {
        // Arrange
        var userId = "testUser";
        var buySellId = 1;

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ReturnsAsync((BuySell?) null);

        // Act
        var result = await _buySellService.UnconfirmPaymentSentAsync(userId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("BuySell not found");
    }

    [Fact]
    public async Task UnconfirmPaymentSentAsync_ReturnsFalse_WhenNotAuthorized()
    {
        // Arrange
        var userId = "testUser";
        var wrongUserId = "wrongUser";
        var buySellId = 1;
        var buySell = CreateTestBuySell(buySellId, buyerId: userId);

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ReturnsAsync(buySell);

        // Act
        var result = await _buySellService.UnconfirmPaymentSentAsync(wrongUserId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Not authorized to unconfirm payment sent for this BuySell");
    }

    [Fact]
    public async Task UnconfirmPaymentReceivedAsync_ReturnsFalse_WhenBuySellNotFound()
    {
        // Arrange
        var userId = "testUser";
        var buySellId = 1;

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ReturnsAsync((BuySell?) null);

        // Act
        var result = await _buySellService.UnconfirmPaymentReceivedAsync(userId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("BuySell not found");
    }

    [Fact]
    public async Task UnconfirmPaymentReceivedAsync_ReturnsFalse_WhenNotAuthorized()
    {
        // Arrange
        var userId = "testUser";
        var wrongUserId = "wrongUser";
        var buySellId = 1;
        var buySell = CreateTestBuySell(buySellId, sellerId: userId);

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ReturnsAsync(buySell);

        // Act
        var result = await _buySellService.UnconfirmPaymentReceivedAsync(wrongUserId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Not authorized to unconfirm payment received for this BuySell");
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_HandlesSessionRepositoryException()
    {
        // Arrange
        var userId = "testUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();

        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ThrowsAsync(new Exception("Database error"));

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error checking buy eligibility for user");
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_HandlesBuySellRepositoryException()
    {
        // Arrange
        var userId = "testUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(request.SessionId))
            .ThrowsAsync(new Exception("Database error"));
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error processing buy request for session");
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_HandlesCreateBuySellException()
    {
        // Arrange
        var userId = "testUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var userResponse = CreateTestUserResponse(userId);

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(request.SessionId))
            .ReturnsAsync((BuySell?) null);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());
        _mockUserRepository.Setup(x => x.GetUserAsync(userId))
            .ReturnsAsync(userResponse);
        _mockBuySellRepository.Setup(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Database error"));

        // Setup ServiceBus
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"])
            .Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"])
            .Returns("https://test.com");

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error processing buy request for session");
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_HandlesSellerUserNotFoundError()
    {
        // Arrange
        var userId = "testUser";
        var sellerId = "sellerUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var userResponse = CreateTestUserResponse(userId);

        // Add seller to session roster
        session.CurrentRosters = new List<RosterPlayer>
        {
            new RosterPlayer
            {
                UserId = sellerId,
                TeamAssignment = TeamAssignment.Light,
                IsPlaying = true,
                FirstName = "Test",
                LastName = "Seller",
                Email = "seller@test.com",
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

        var existingSell = CreateTestBuySell(sellerId: sellerId);

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync(sellerId))
            .ReturnsAsync((AspNetUser?) null); // Seller not found
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(request.SessionId))
            .ReturnsAsync(existingSell);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Seller not found");
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_HandlesSellerNotInRosterError()
    {
        // Arrange
        var userId = "testUser";
        var sellerId = "sellerUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId);
        var seller = CreateTestUser(sellerId);
        var session = CreateTestSessionDetailedResponse();

        // Empty roster - seller not in it
        session.CurrentRosters = new List<RosterPlayer>();

        var existingSell = CreateTestBuySell(sellerId: sellerId);

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManager.Setup(x => x.FindByIdAsync(sellerId))
            .ReturnsAsync(seller);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(request.SessionId))
            .ReturnsAsync(existingSell);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Seller not found in session roster");
    }

    [Fact]
    public async Task ProcessSellRequestAsync_HandlesCanSellAsyncError()
    {
        // Arrange
        var userId = "testUser";
        var request = new SellRequest { SessionId = 1, Note = "Test sell" };

        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _buySellService.ProcessSellRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error checking sell eligibility for user");
    }

    [Fact]
    public async Task ProcessSellRequestAsync_HandlesRosterError()
    {
        // Arrange
        var userId = "testUser";
        var request = new SellRequest { SessionId = 1, Note = "Test sell" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var emptyRoster = new List<RosterPlayer>();
        session.CurrentRosters = emptyRoster;

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessSellRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("You must be on the roster to sell your spot");
    }

    [Fact]
    public async Task ProcessSellRequestAsync_HandlesBuySellRepositoryException()
    {
        // Arrange
        var userId = "testUser";
        var request = new SellRequest { SessionId = 1, Note = "Test sell" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = CreateFullRosterPlayer(userId);

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());
        _mockBuySellRepository.Setup(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Database error"));

        // Setup ServiceBus
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"])
            .Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"])
            .Returns("https://test.com");

        // Act
        var result = await _buySellService.ProcessSellRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error processing sell request for session");
    }

    [Theory]
    [InlineData("test@example.com", true, NotificationPreference.All)]
    [InlineData("", true, NotificationPreference.All)]
    [InlineData("test@example.com", false, NotificationPreference.All)]
    [InlineData("test@example.com", true, NotificationPreference.None)]
    public async Task SendBuySellServiceBusCommsMessage_FiltersEmailsCorrectly(string email, bool isActive, NotificationPreference preference)
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var userResponse = new UserDetailedResponse
        {
            Id = "testId",
            UserName = "testUser",
            Email = email,
            FirstName = "Test",
            LastName = "User",
            Rating = 1.0m,
            Active = isActive,
            Preferred = false,
            PreferredPlus = false,
            NotificationPreference = preference
        };

        var buyer = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();

        // Setup all required mocks
        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(buyer);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId))
            .ReturnsAsync(new List<BuySell>());
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(sessionId))
            .ReturnsAsync((BuySell?) null);
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync())
            .ReturnsAsync(new List<UserDetailedResponse> { userResponse });
        _mockUserRepository.Setup(x => x.GetUserAsync(userId))
            .ReturnsAsync(userResponse);

        // Setup ServiceBus and capture the message for verification
        ServiceBusCommsMessage? capturedMessage = null;
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"])
            .Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"])
            .Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Callback<ServiceBusCommsMessage, string, string, string, CancellationToken?>((msg, _, _, _, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var buySell = CreateTestBuySell(1, userId);
        _mockBuySellRepository.Setup(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()))
            .ReturnsAsync(buySell);

        // Act
        await _buySellService.ProcessBuyRequestAsync(userId, new BuyRequest { SessionId = sessionId });

        // Assert
        _mockServiceBus.Verify(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        capturedMessage.Should().NotBeNull();
        if (string.IsNullOrEmpty(email) || !isActive || preference != NotificationPreference.All)
        {
            capturedMessage!.NotificationEmails.Should().NotContain(email);
        }
        else
        {
            capturedMessage!.NotificationEmails.Should().Contain(email);
        }
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_HandleServiceBusException()
    {
        // Arrange
        var userId = "testUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var userResponse = CreateTestUserResponse(userId);

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(request.SessionId))
            .ReturnsAsync((BuySell?) null);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());
        _mockUserRepository.Setup(x => x.GetUserAsync(userId))
            .ReturnsAsync(userResponse);

        var mockBuySell = CreateTestBuySell(buyerId: userId);
        _mockBuySellRepository.Setup(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()))
            .ReturnsAsync(mockBuySell);

        // Setup ServiceBus to throw exception
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"])
            .Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"])
            .Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service Bus error"));

        // Act 
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error processing buy request");
        _mockServiceBus.Verify(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessSellRequestAsync_WithNoCurrentRosters_HandlesGracefully()
    {
        // Arrange
        var userId = "testUser";
        var request = new SellRequest { SessionId = 1, Note = "Test sell" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var existingSell = CreateTestBuySell(sellerId: userId);
        session.CurrentRosters = new List<RosterPlayer>(); // Empty list instead of null

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessSellRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("You must be on the roster to sell your spot");
    }

    [Fact]
    public async Task ProcessSellRequestAsync_WithMissingRoster_HandlesGracefully()
    {
        // Arrange
        var userId = "testUser";
        var request = new SellRequest { SessionId = 1, Note = "Test sell" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = null; // Test null roster handling

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessSellRequestAsync(userId, request);

        // Assert  
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Seller not found in session roster");
    }

    [Fact]
    public async Task SendBuySellServiceBusCommsMessageAsync_HandlesNullEmailLists()
    {
        // Arrange
        var userId = "testUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var userResponse = CreateTestUserResponse(userId);
        var emptyUserList = new List<UserDetailedResponse>(); // Empty list instead of null

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(request.SessionId))
            .ReturnsAsync((BuySell?) null);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());
        _mockUserRepository.Setup(x => x.GetUserAsync(userId))
            .ReturnsAsync(userResponse);
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync())
            .ReturnsAsync(emptyUserList); // Return empty list instead of null

        var mockBuySell = CreateTestBuySell(buyerId: userId);
        _mockBuySellRepository.Setup(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()))
            .ReturnsAsync(mockBuySell);

        // Setup ServiceBus
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"])
            .Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"])
            .Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act 
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockServiceBus.Verify(x => x.SendAsync(
            It.Is<ServiceBusCommsMessage>(m => m.NotificationEmails != null && !m.NotificationEmails.Any()),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnconfirmPaymentSentAsync_ReturnsFalse_WhenPaymentAlreadyReceived()
    {
        // Arrange
        var buyerId = "testUser";
        var sellerId = "sellerUser";
        var buySellId = 1;
        var paymentMethod = PaymentMethodType.Venmo;

        var buyer = CreateTestUser(buyerId);
        var buySell = CreateTestBuySell(buySellId, buyerId, sellerId,
            paymentSent: true,
            paymentReceived: true, // Payment already received
            paymentMethod: paymentMethod);

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ReturnsAsync(buySell);

        // Act
        var result = await _buySellService.UnconfirmPaymentSentAsync(buyerId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error unconfirming payment sent for BuySell");
    }

    [Fact]
    public async Task SendBuySellServiceBusCommsMessageAsync_WithMessageDataAdditions()
    {
        // Arrange
        var userId = "testUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var userResponse = CreateTestUserResponse(userId);

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(request.SessionId))
            .ReturnsAsync((BuySell?) null);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());
        _mockUserRepository.Setup(x => x.GetUserAsync(userId))
            .ReturnsAsync(userResponse);
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync())
            .ReturnsAsync(new List<UserDetailedResponse> { userResponse });

        var mockBuySell = CreateTestBuySell(buyerId: userId);
        _mockBuySellRepository.Setup(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()))
            .ReturnsAsync(mockBuySell);

        // Capture the message data additions
        ServiceBusCommsMessage? capturedMessage = null;
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"])
            .Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"])
            .Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Callback<ServiceBusCommsMessage, string, string, string, CancellationToken?>((msg, _, _, _, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedMessage.Should().NotBeNull();
        capturedMessage!.MessageData.Should().ContainKey("SessionDate");
        capturedMessage.MessageData.Should().ContainKey("SessionUrl");
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_WhenCurrentRostersIsNull_HandlesGracefully()
    {
        // Arrange
        var userId = "testUser";
        var sellerId = "sellerUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var buyer = CreateTestUser(userId);
        var seller = CreateTestUser(sellerId);
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = null; // Explicitly set to null
        var existingSell = CreateTestBuySell(sellerId: sellerId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(buyer);
        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == sellerId))).ReturnsAsync(seller);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(request.SessionId)).ReturnsAsync(existingSell);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Seller not found in session roster");
    }

    [Fact]
    public async Task ProcessBuyRequestAsync_WhenSellerRosterIsNull_HandlesGracefully()
    {
        // Arrange
        var userId = "testUser";
        var sellerId = "sellerUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var buyer = CreateTestUser(userId);
        var seller = CreateTestUser(sellerId);
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = new List<RosterPlayer>(); // Empty list
        var existingSell = CreateTestBuySell(sellerId: sellerId);

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(buyer);
        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == sellerId))).ReturnsAsync(seller);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(request.SessionId)).ReturnsAsync(existingSell);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Seller not found in session roster");
    }

    [Fact]
    public async Task CanBuyAsync_WhenCurrentRostersIsNull_HandlesGracefully()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = null; // Explicitly set to null

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanBuyAsync_WhenHasActiveSellerTransaction_ReturnsFalse()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var activeSell = CreateTestBuySell(sellerId: userId); // Create active sell transaction

        _userManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id == userId))).ReturnsAsync(user);
        _userManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId))
            .ReturnsAsync(new List<BuySell> { activeSell });

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.IsAllowed.Should().BeFalse();
        result.Data.Reason.Should().Be("You have an active Sell for this session");
    }

    [Theory]
    [InlineData(null)]
    //[InlineData(new object[] { new Dictionary<string, string>() })]
    //[InlineData(new object[] { new Dictionary<string, string> { { "TestKey", "TestValue" } } })]
    public async Task ProcessBuyRequestAsync_HandlesMessageDataAdditionsCorrectly(Dictionary<string, string>? messageDataAdditions)
    {
        // Arrange
        var userId = "testUser";
        var request = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        var userResponse = CreateTestUserResponse(userId);

        _userManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.FindMatchingSellBuySellAsync(request.SessionId))
            .ReturnsAsync((BuySell?) null);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(request.SessionId, userId))
            .ReturnsAsync(new List<BuySell>());
        _mockUserRepository.Setup(x => x.GetUserAsync(userId)).ReturnsAsync(userResponse);
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync())
            .ReturnsAsync(new List<UserDetailedResponse> { userResponse });

        ServiceBusCommsMessage? capturedMessage = null;
        _mockConfiguration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("testqueue");
        _mockConfiguration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Callback<ServiceBusCommsMessage, string, string, string, CancellationToken?>((msg, _, _, _, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var mockBuySell = CreateTestBuySell(buyerId: userId);
        _mockBuySellRepository.Setup(x => x.CreateBuySellAsync(It.IsAny<BuySell>(), It.IsAny<string>()))
            .ReturnsAsync(mockBuySell);

        // Act
        var result = await _buySellService.ProcessBuyRequestAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedMessage.Should().NotBeNull();
        capturedMessage!.MessageData.Should().ContainKey("SessionDate");
        capturedMessage.MessageData.Should().ContainKey("SessionUrl");

        if (messageDataAdditions != null)
        {
            foreach (var kvp in messageDataAdditions)
            {
                if (capturedMessage.MessageData.ContainsKey(kvp.Key))
                {
                    capturedMessage.MessageData[kvp.Key].Should().Be(kvp.Value);
                }
            }
        }
    }

    [Fact]
    public async Task ConfirmPaymentSentAsync_ReturnsFalse_WhenBuySellNotFound()
    {
        // Arrange
        var userId = "testUser";
        var buySellId = 1;

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ReturnsAsync((BuySell?)null);

        // Act
        var result = await _buySellService.ConfirmPaymentSentAsync(userId, buySellId, PaymentMethodType.Unknown);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("BuySell not found");
    }

    [Fact]
    public async Task ConfirmPaymentReceivedAsync_ReturnsFalse_WhenBuySellNotFound()
    {
        // Arrange
        var userId = "testUser";
        var buySellId = 1;

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ReturnsAsync((BuySell?)null);

        // Act
        var result = await _buySellService.ConfirmPaymentReceivedAsync(userId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("BuySell not found");
    }

    [Fact]
    public async Task ConfirmPaymentSentAsync_ReturnsFalse_WhenNotAuthorized()
    {
        // Arrange
        var userId = "testUser";
        var wrongUserId = "wrongUser";
        var buySellId = 1;
        var buySell = CreateTestBuySell(buySellId, buyerId: userId); // Note: wrongUserId is not the buyer

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ReturnsAsync(buySell);

        // Act
        var result = await _buySellService.ConfirmPaymentSentAsync(wrongUserId, buySellId, PaymentMethodType.Unknown);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Not authorized to confirm payment sent for this BuySell");
    }

    [Fact]
    public async Task ConfirmPaymentReceivedAsync_ReturnsFalse_WhenNotAuthorized()
    {
        // Arrange
        var userId = "testUser";
        var wrongUserId = "wrongUser";
        var buySellId = 1;
        var buySell = CreateTestBuySell(buySellId, sellerId: userId); // Note: wrongUserId is not the seller

        _mockBuySellRepository.Setup(x => x.GetBuySellAsync(buySellId))
            .ReturnsAsync(buySell);

        // Act
        var result = await _buySellService.ConfirmPaymentReceivedAsync(wrongUserId, buySellId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Not authorized to confirm payment received for this BuySell");
    }

    [Theory]
    [InlineData(true, null, true)]    // User is seller, no buyer - active sell
    [InlineData(true, "buyer123", false)]  // User is seller but has buyer - not active sell
    [InlineData(false, null, false)]   // User is not seller - not active sell
    public async Task CanBuyAsync_ChecksActiveSellConditions(bool isUserSeller, string? buyerId, bool expectActiveSell)
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();

        var buySell = CreateTestBuySell(
            sellerId: isUserSeller ? userId : "otherSeller",
            buyerId: buyerId
        );

        _userManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId))
            .ReturnsAsync(new List<BuySell> { buySell });

        // Act
        var result = await _buySellService.CanBuyAsync(userId, sessionId);

        // Assert
        if (expectActiveSell)
        {
            result.Data.IsAllowed.Should().BeFalse();
            result.Data.Reason.Should().Be("You have an active Sell for this session");
        }
        else
        {
            result.Data.IsAllowed.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(true, "buyer123", false)]  // User is seller but has buyer - not active sell
    [InlineData(false, null, false)]   // User is not seller - not active sell
    public async Task CanSellAsync_ChecksActiveSellConditions(bool isUserSeller, string? buyerId, bool expectActiveSell)
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();
        session.CurrentRosters = CreateFullRosterPlayer(userId); // Ensure user is on roster and playing

        var buySell = CreateTestBuySell(
            sellerId: isUserSeller ? userId : "otherSeller",
            buyerId: buyerId
        );

        _userManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId))
            .ReturnsAsync(new List<BuySell> { buySell });

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        if (expectActiveSell)
        {
            result.Data.IsAllowed.Should().BeFalse();
            result.Data.Reason.Should().Be("You already have a buy/sell request for this session");
        }
        else
        {
            result.Data.IsAllowed.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("otherUser", false)]              // Wrong user
    [InlineData("correctUser", true)]             // Correct user and playing
    public async Task CanSellAsync_ChecksRosterConditions(object? rosterData, bool expectedAllowed)
    {
        // Arrange
        var userId = (rosterData as string) == "correctUser" ? "correctUser" : "testUser";
        var sessionId = 1;
        var user = CreateTestUser(userId);
        var session = CreateTestSessionDetailedResponse();

        // Setup rosters based on test data
        if (rosterData == null)
        {
            session.CurrentRosters = null;
        }
        else if (rosterData is RosterPlayer[])
        {
            session.CurrentRosters = new List<RosterPlayer>();
        }
        else
        {
            session.CurrentRosters = new List<RosterPlayer>
            {
                new RosterPlayer
                {
                    UserId = rosterData as string ?? "otherUser",
                    IsPlaying = true,
                    TeamAssignment = TeamAssignment.Light,
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
                    LastBuySellId = null,
                    Preferred = false,
                    PreferredPlus = false
                }
            };
        }

        _userManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId)).ReturnsAsync(session);
        _mockBuySellRepository.Setup(x => x.GetUserBuySellsAsync(sessionId, userId))
            .ReturnsAsync(new List<BuySell>());

        // Act
        var result = await _buySellService.CanSellAsync(userId, sessionId);

        // Assert
        result.Data.IsAllowed.Should().Be(expectedAllowed);
        if (!expectedAllowed)
        {
            result.Data.Reason.Should().Be("You must be on the roster to sell your spot");
        }
    }
}
#pragma warning restore IDE0045 // Convert to conditional expression
