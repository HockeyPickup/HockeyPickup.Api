#pragma warning disable IDE0031 // Use null propagation
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using Microsoft.Extensions.Logging;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Models.Responses;
using RosterPlayer = HockeyPickup.Api.Models.Responses.RosterPlayer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace HockeyPickup.Api.Tests.DataRepositoryTests;

// Test-specific DbContext
public class BuySellTestHockeyPickupContext : HockeyPickupContext
{
    public BuySellTestHockeyPickupContext(DbContextOptions<HockeyPickupContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure AspNetUser
        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.ToTable("AspNetUsers");
            entity.HasKey(e => e.Id);

            // Basic properties needed for tests
            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.UserName).HasMaxLength(256);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FirstName);
            entity.Property(e => e.LastName);
            entity.Property(e => e.Rating);
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.Preferred).HasDefaultValue(false);
            entity.Property(e => e.PreferredPlus).HasDefaultValue(false);
            entity.Property(e => e.NotificationPreference)
                .HasDefaultValue(NotificationPreference.OnlyMyBuySell);
            entity.Property(e => e.PositionPreference)
                .HasDefaultValue(PositionPreference.TBD);
            entity.Property(e => e.Shoots)
                .HasDefaultValue(ShootPreference.TBD);

            // Ignore navigation properties
            entity.Ignore(e => e.Roles);
            entity.Ignore(e => e.BuyerTransactions);
            entity.Ignore(e => e.SellerTransactions);
            entity.Ignore(e => e.ActivityLogs);
            entity.Ignore(e => e.Regulars);

            // Ignore Identity properties
            entity.Ignore(e => e.LockoutEnd);
            entity.Ignore(e => e.AccessFailedCount);
            entity.Ignore(e => e.ConcurrencyStamp);
            entity.Ignore(e => e.EmailConfirmed);
            entity.Ignore(e => e.LockoutEnabled);
            entity.Ignore(e => e.LockoutEndDateUtc);
            entity.Ignore(e => e.NormalizedEmail);
            entity.Ignore(e => e.NormalizedUserName);
            entity.Ignore(e => e.PasswordHash);
            entity.Ignore(e => e.PhoneNumber);
            entity.Ignore(e => e.PhoneNumberConfirmed);
            entity.Ignore(e => e.SecurityStamp);
            entity.Ignore(e => e.TwoFactorEnabled);
        });

        // Configure BuySell
        modelBuilder.Entity<BuySell>(entity =>
        {
            entity.HasKey(e => e.BuySellId);
            entity.Property(e => e.CreateDateTime);
            entity.Property(e => e.UpdateDateTime);
            entity.Property(e => e.PaymentSent).HasDefaultValue(false);
            entity.Property(e => e.PaymentReceived).HasDefaultValue(false);
            entity.Property(e => e.TeamAssignment).HasDefaultValue(TeamAssignment.TBD);

            // Configure navigation properties properly
            entity.HasOne(e => e.Session)
                .WithMany(s => s.BuySells)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Buyer)
                .WithMany(u => u.BuyerTransactions)
                .HasForeignKey(e => e.BuyerUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Seller)
                .WithMany(u => u.SellerTransactions)
                .HasForeignKey(e => e.SellerUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreateByUser)
                .WithMany()
                .HasForeignKey(e => e.CreateByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.UpdateByUser)
                .WithMany()
                .HasForeignKey(e => e.UpdateByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure navigation property loading
            entity.Navigation(e => e.Session).AutoInclude();
            entity.Navigation(e => e.Buyer).AutoInclude();
            entity.Navigation(e => e.Seller).AutoInclude();
        });

        // Configure Regular entity
        modelBuilder.Entity<Regular>(entity =>
        {
            entity.HasKey(e => new { e.RegularSetId, e.UserId });

            // Ignore navigation properties
            entity.Ignore(e => e.RegularSet);
            entity.Ignore(e => e.User);
        });

        // Configure RegularSet entity
        modelBuilder.Entity<RegularSet>(entity =>
        {
            entity.HasKey(e => e.RegularSetId);

            // Ignore navigation properties
            entity.Ignore(e => e.Sessions);
            entity.Ignore(e => e.Regulars);
        });

        // Configure Session entity
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.SessionId);

            // Ignore navigation properties
            entity.Ignore(e => e.RegularSet);
            entity.Ignore(e => e.BuySells);
            entity.Ignore(e => e.ActivityLogs);
        });

        // Configure ActivityLog entity
        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasKey(e => e.ActivityLogId);

            // Ignore navigation properties
            entity.Ignore(e => e.Session);
            entity.Ignore(e => e.User);
        });

        // Explicitly ignore all Identity-related types
        modelBuilder.Ignore<IdentityRole>();
        modelBuilder.Ignore<IdentityUserRole<string>>();
        modelBuilder.Ignore<IdentityUserClaim<string>>();
        modelBuilder.Ignore<IdentityUserLogin<string>>();
        modelBuilder.Ignore<IdentityUserToken<string>>();
        modelBuilder.Ignore<IdentityRoleClaim<string>>();
        modelBuilder.Ignore<AspNetRole>();
    }
}

public class BuySellRepositoryTest : IDisposable
{
    private readonly Mock<ILogger<BuySellRepository>> _mockLogger;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly HockeyPickupContext _context;
    private readonly BuySellRepository _repository;
    private static int _dbCounter;
    private const string TEST_USER_ID = "testUser123";

    public BuySellRepositoryTest()
    {
        _mockLogger = new Mock<ILogger<BuySellRepository>>();
        _mockSessionRepository = new Mock<ISessionRepository>();

        var optionsBuilder = new DbContextOptionsBuilder<HockeyPickupContext>();
        optionsBuilder.UseInMemoryDatabase($"HockeyPickupTest_{Interlocked.Increment(ref _dbCounter)}");

        _context = new BuySellTestHockeyPickupContext(optionsBuilder.Options);
        _repository = new BuySellRepository(_context, _mockLogger.Object, _mockSessionRepository.Object);
    }

    private SessionDetailedResponse CreateDummySessionResponse()
    {
        return new SessionDetailedResponse
        {
            SessionId = 1,
            SessionDate = DateTime.UtcNow,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            RegularSetId = 1,
            Note = string.Empty,
            ActivityLogs = new List<ActivityLogResponse>(),
            CurrentRosters = new List<RosterPlayer>(),
            BuySells = new List<BuySellResponse>()
        };
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task SeedTestData()
    {
        // Clear existing data
        if (_context.BuySells != null) _context.BuySells.RemoveRange(_context.BuySells);
        if (_context.Users != null) _context.Users.RemoveRange(_context.Users);
        if (_context.Sessions != null) _context.Sessions.RemoveRange(_context.Sessions);
        await _context.SaveChangesAsync();

        // Add test session
        var session = new Session
        {
            SessionId = 1,
            SessionDate = DateTime.UtcNow.AddDays(1),
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };
        await _context.Sessions!.AddAsync(session);

        // Add test users
        var users = new[]
        {
            new AspNetUser { Id = "testUserId", UserName = "test@test.com" },
            new AspNetUser { Id = "buyerId", UserName = "buyer@test.com" },
            new AspNetUser { Id = "sellerId", UserName = "seller@test.com" }
        };
        await _context.Users!.AddRangeAsync(users);

            // Add test BuySells
        var buySells = new[]
        {
            new BuySell
            {
                BuySellId = 1,
                SessionId = 1,
                BuyerUserId = "buyerId",
                TeamAssignment = TeamAssignment.Light,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow
            },
            new BuySell
            {
                BuySellId = 2,
                SessionId = 1,
                SellerUserId = "sellerId",
                TeamAssignment = TeamAssignment.Dark,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow
            }
        };
        await _context.BuySells!.AddRangeAsync(buySells);

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateBuySellAsync_Success()
    {
        // Arrange
        await SeedTestData();
        var buySell = new BuySell
        {
            SessionId = 1,
            SellerUserId = "seller1",
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            CreateByUserId = TEST_USER_ID,
            UpdateByUserId = TEST_USER_ID,
            BuySellId = 22  // Different ID for create
        };
        var message = "Test message";

        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, message))
            .ReturnsAsync(CreateDummySessionResponse());

        // Act
        var result = await _repository.CreateBuySellAsync(buySell, message);

        // Assert
        result.Should().NotBeNull();
        result.SellerUserId.Should().Be("seller1");
    }

    [Fact]
    public async Task CreateBuySellAsync_ThrowsException()
    {
        // Arrange
        await SeedTestData();
        var buySell = new BuySell
        {
            SessionId = 1,
            BuyerUserId = "buyerId",
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            CreateByUserId = TEST_USER_ID,
            UpdateByUserId = TEST_USER_ID
        };
        var message = "Test message";

        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, message))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _repository.CreateBuySellAsync(buySell, message));
    }

    [Fact]
    public async Task UpdateBuySellAsync_Success()
    {
        // Arrange
        await SeedTestData();
        var buySell = await _repository.GetBuySellAsync(1);
        buySell!.PaymentSent = true;
        var message = "Payment sent";

        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, message))
            .ReturnsAsync(CreateDummySessionResponse());

        // Act
        var result = await _repository.UpdateBuySellAsync(buySell, message);

        // Assert
        result.Should().NotBeNull();
        result.PaymentSent.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateBuySellAsync_ThrowsException()
    {
        // Arrange
        await SeedTestData();
        var buySell = await _repository.GetBuySellAsync(1);
        buySell!.PaymentSent = true;
        var message = "Test message";

        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, message))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _repository.UpdateBuySellAsync(buySell, message));
    }

    [Fact]
    public async Task DeleteBuySellAsync_Success()
    {
        // Arrange
        await SeedTestData();
        var message = "Test deletion";

        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, message))
            .ReturnsAsync(CreateDummySessionResponse());

        // Act
        var result = await _repository.DeleteBuySellAsync(1, message);

        // Assert
        result.Should().BeTrue();
        var deletedBuySell = await _repository.GetBuySellAsync(1);
        deletedBuySell.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBuySellAsync_NotFound()
    {
        // Arrange
        await SeedTestData();
        var message = "Test deletion";

        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, message))
            .ReturnsAsync(CreateDummySessionResponse());

        // Act
        var result = await _repository.DeleteBuySellAsync(6, message);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBuySellAsync_ThrowsException()
    {
        // Arrange
        await SeedTestData();
        var message = "Test deletion";

        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, message))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _repository.DeleteBuySellAsync(1, message));
    }

    [Fact]
    public async Task GetSessionBuySellsAsync_Success()
    {
        // Arrange
        await SeedTestData();

        // Act
        var result = await _repository.GetSessionBuySellsAsync(1);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().BeInDescendingOrder(x => x.CreateDateTime);
        result.All(x => x.SessionId == 1).Should().BeTrue();
    }

    [Fact]
    public async Task GetSessionBuySellsAsync_ThrowsException()
    {
        // Arrange
        _context.Database.EnsureDeleted();
        await _context.Database.EnsureCreatedAsync();
        _context.BuySells = null; // Force null to simulate DB error

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _repository.GetSessionBuySellsAsync(1));
    }

    [Fact]
    public async Task GetUserBuySellsAsync_Success()
    {
        // Arrange
        await SeedTestData();

        // Act
        var result = await _repository.GetUserBuySellsAsync("buyerId");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().BuyerUserId.Should().Be("buyerId");
    }

    [Fact]
    public async Task GetUserBuySellsAsync_ThrowsException()
    {
        // Arrange
        _context.Database.EnsureDeleted();
        await _context.Database.EnsureCreatedAsync();
        _context.BuySells = null; // Force null to simulate DB error

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _repository.GetUserBuySellsAsync("buyerId"));
    }

    [Fact]
    public async Task GetUserBuySellsForSessionAsync_Success()
    {
        // Arrange
        await SeedTestData();

        // Act
        var result = await _repository.GetUserBuySellsAsync(1, "buyerId");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().BuyerUserId.Should().Be("buyerId");
        result.First().SessionId.Should().Be(1);
    }

    [Fact]
    public async Task GetUserBuySellsForSessionAsync_ThrowsException()
    {
        // Arrange
        _context.Database.EnsureDeleted();
        await _context.Database.EnsureCreatedAsync();
        _context.BuySells = null; // Force null to simulate DB error

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _repository.GetUserBuySellsAsync(1, "buyerId"));
    }

    [Fact]
    public async Task FindMatchingSellBuySellAsync_Success()
    {
        // Arrange
        await SeedTestData();

        // Add a matching sell BuySell
        var sellBuySell = new BuySell
        {
            SessionId = 1,
            SellerUserId = "sellerId",
            BuyerUserId = null,
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            CreateByUserId = TEST_USER_ID,
            UpdateByUserId = TEST_USER_ID
        };
        await _context.BuySells!.AddAsync(sellBuySell);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindMatchingSellBuySellAsync(1);

        // Assert
        result.Should().NotBeNull();
        result.SellerUserId.Should().Be("sellerId");
        result.BuyerUserId.Should().BeNull();
    }

    [Fact]
    public async Task FindMatchingSellBuySellAsync_ThrowsException()
    {
        // Arrange
        _context.Database.EnsureDeleted();
        await _context.Database.EnsureCreatedAsync();
        _context.BuySells = null; // Force null to simulate DB error

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _repository.FindMatchingSellBuySellAsync(1));
    }

    [Fact]
    public async Task FindMatchingBuyBuySellAsync_Success()
    {
        // Arrange
        await SeedTestData();

        // Add a matching buy BuySell
        var buyBuySell = new BuySell
        {
            SessionId = 1,
            BuyerUserId = "buyerId",
            SellerUserId = null,
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            CreateByUserId = TEST_USER_ID,
            UpdateByUserId = TEST_USER_ID
        };
        await _context.BuySells!.AddAsync(buyBuySell);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindMatchingBuyBuySellAsync(1);

        // Assert
        result.Should().NotBeNull();
        result.BuyerUserId.Should().Be("buyerId");
        result.SellerUserId.Should().BeNull();
    }

    [Fact]
    public async Task FindMatchingBuyBuySellAsync_ThrowsException()
    {
        // Arrange
        _context.Database.EnsureDeleted();
        await _context.Database.EnsureCreatedAsync();
        _context.BuySells = null; // Force null to simulate DB error

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _repository.FindMatchingBuyBuySellAsync(1));
    }

    [Fact]
    public async Task GetQueuePositionAsync_Success()
    {
        // Arrange
        await SeedTestData();

        // Add some BuySells to create a queue
        var buySell1 = new BuySell
        {
            SessionId = 1,
            SellerUserId = null,
            BuyerUserId = "buyer1",
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow.AddMinutes(-2),
            UpdateDateTime = DateTime.UtcNow,
            CreateByUserId = TEST_USER_ID,
            UpdateByUserId = TEST_USER_ID
        };
        var buySell2 = new BuySell
        {
            SessionId = 1,
            SellerUserId = null,
            BuyerUserId = "buyer2",
            TeamAssignment = TeamAssignment.Light,
            CreateDateTime = DateTime.UtcNow.AddMinutes(-1),
            UpdateDateTime = DateTime.UtcNow,
            CreateByUserId = TEST_USER_ID,
            UpdateByUserId = TEST_USER_ID
        };
        await _context.BuySells!.AddRangeAsync(buySell1, buySell2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetQueuePositionAsync(buySell1.BuySellId);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(2);
    }

    [Fact]
    public async Task GetQueuePositionAsync_NonExistentBuySell_ReturnsNull()
    {
        // Arrange
        await SeedTestData();

        // Act
        var result = await _repository.GetQueuePositionAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetQueuePositionAsync_ThrowsException()
    {
        // Arrange
        _context.Database.EnsureDeleted();
        await _context.Database.EnsureCreatedAsync();
        _context.BuySells = null; // Force null to simulate DB error

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _repository.GetQueuePositionAsync(1));
    }
}
#pragma warning restore IDE0031 // Use null propagation
