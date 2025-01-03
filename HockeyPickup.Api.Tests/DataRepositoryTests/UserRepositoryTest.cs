using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;
using Microsoft.Extensions.Logging;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Data.Context;
using Microsoft.AspNetCore.Identity;

namespace HockeyPickup.Api.Tests.DataRepositoryTests;

// Test-specific DbContext
public class UserTestHockeyPickupContext : HockeyPickupContext
{
    public UserTestHockeyPickupContext(DbContextOptions<HockeyPickupContext> options)
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
            entity.Property(e => e.PayPalEmail).HasDefaultValue("");
            entity.Property(e => e.NotificationPreference).HasDefaultValue(1);
            entity.Property(e => e.PositionPreference).HasDefaultValue(1);

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

        // Configure BuySell entity
        modelBuilder.Entity<BuySell>(entity =>
        {
            entity.HasKey(e => e.BuySellId);

            // Ignore navigation properties
            entity.Ignore(e => e.Buyer);
            entity.Ignore(e => e.Seller);
            entity.Ignore(e => e.Session);
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

public partial class UserRepositoryTest
{
    private readonly Mock<ILogger<UserRepository>> _mockLogger;
    private readonly HockeyPickupContext _context;
    private readonly UserRepository _repository;
    private static int _dbCounter;

    public UserRepositoryTest()
    {
        _mockLogger = new Mock<ILogger<UserRepository>>();

        var optionsBuilder = new DbContextOptionsBuilder<HockeyPickupContext>();
        optionsBuilder.UseInMemoryDatabase($"HockeyPickupTest_{Interlocked.Increment(ref _dbCounter)}");

        _context = new UserTestHockeyPickupContext(optionsBuilder.Options);
        _context.Users.AddRange(new[]
        {
            new AspNetUser
            {
                Id = "user1",
                UserName = "user1@example.com",
                Email = "user1@example.com",
                FirstName = "Active",
                LastName = "User",
                Active = true,
                LockerRoom13 = true,
                Preferred = true,
                PreferredPlus = false,
                Rating = 4.5m,
                PayPalEmail = "user1@example.com",
                DateCreated = DateTime.Parse("02/25/1969")
            },
            new AspNetUser
            {
                Id = "user2",
                UserName = "user2@example.com",
                Email = "user2@example.com",
                FirstName = "Inactive",
                LastName = "User",
                Active = false,
                LockerRoom13 = true,
                Preferred = false,
                PreferredPlus = false,
                Rating = 3.5m,
                PayPalEmail = "user2@example.com",
                DateCreated = DateTime.Parse("02/25/1969")
            },
            new AspNetUser
            {
                Id = "user3",
                UserName = "user3@example.com",
                Email = "user3@example.com",
                FirstName = "Preferred",
                LastName = "Plus",
                Active = true,
                Preferred = true,
                PreferredPlus = true,
                Rating = 5.0m,
                PayPalEmail = "user3@example.com",
                DateCreated = DateTime.Parse("02/25/1969")
            }
        });
        _context.SaveChanges();

        _repository = new UserRepository(_context, _mockLogger.Object);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }

    protected void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetDetailedUsersAsync_MapsPropertiesCorrectly()
    {
        // Act
        var result = await _repository.GetDetailedUsersAsync();

        // Assert
        var preferredPlusUser = result.Should().ContainSingle(u => u.Id == "user3").Subject;
        preferredPlusUser.Should().BeEquivalentTo(new UserDetailedResponse
        {
            Id = "user3",
            UserName = "user3@example.com",
            Email = "user3@example.com",
            PayPalEmail = "user3@example.com",
            FirstName = "Preferred",
            LastName = "Plus",
            Rating = 0m,
            Preferred = true,
            PreferredPlus = true,
            Active = true,
            NotificationPreference = NotificationPreference.All,
            PositionPreference = PositionPreference.TBD,
            DateCreated = DateTime.Parse("02/25/1969")
        });
    }

    [Fact]
    public async Task GetDetailedUsersAsync_WithNoUsers_ReturnsEmptyList()
    {
        // Arrange
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        // Act
        var result = await _repository.GetDetailedUsersAsync();

        // Assert
        result.Should().BeEmpty();
    }
}

public partial class UserRepositoryTest
{
    [Fact]
    public async Task GetUserAsync_UserExists_ReturnsUserResponse()
    {
        // Arrange
        var userId = "test-user-id";
        await _context.Users.AddAsync(new AspNetUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Preferred = true,
            PreferredPlus = false,
            Active = true
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.UserName.Should().Be("testuser");
        result.Email.Should().Be("test@example.com");
        result.FirstName.Should().Be("Test");
        result.LastName.Should().Be("User");
        result.Preferred.Should().BeTrue();
        result.PreferredPlus.Should().BeFalse();
        result.Active.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserAsync_UserDoesNotExist_ReturnsNull()
    {
        // Arrange
        var userId = "nonexistent-id";

        // Act
        var result = await _repository.GetUserAsync(userId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_MultipleUsers_ReturnsCorrectUser()
    {
        // Arrange
        var targetUserId = "target-user-id";
        await _context.Users.AddRangeAsync(
            new AspNetUser { Id = "other-id-1", UserName = "other1" },
            new AspNetUser
            {
                Id = targetUserId,
                UserName = "target",
                Email = "target@example.com",
                FirstName = "Target",
                LastName = "User",
                Preferred = true,
                PreferredPlus = true,
                Active = true
            },
            new AspNetUser { Id = "other-id-2", UserName = "other2" }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserAsync(targetUserId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(targetUserId);
        result.UserName.Should().Be("target");
    }
}

public partial class UserRepositoryTest
{
    [Fact]
    public async Task GetLockerRoom13SessionsAsync_ReturnsEmptyWhenNoSessions()
    {
        // Act
        var result = await _repository.GetLockerRoom13SessionsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLockerRoom13SessionsAsync_FiltersOutPastAndCancelledSessions()
    {
        // Arrange
        var sessions = new[]
        {
            new Session
            {
                SessionId = 1,
                SessionDate = DateTime.UtcNow.AddDays(1),
                Note = "Future session",
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow
            },
            new Session
            {
                SessionId = 2,
                SessionDate = DateTime.UtcNow.AddDays(-1),
                Note = "Past session",
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow
            },
            new Session
            {
                SessionId = 3,
                SessionDate = DateTime.UtcNow.AddDays(2),
                Note = "This session was cancelled",
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow
            }
        };
        await _context.Sessions.AddRangeAsync(sessions);

        var user = new AspNetUser
        {
            Id = "lr13user",
            UserName = "test@example.com",
            LockerRoom13 = true,
            Active = true,
            Preferred = false,
            PreferredPlus = false
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetLockerRoom13SessionsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Single().SessionId.Should().Be(1);
    }

    [Fact]
    public async Task GetLockerRoom13SessionsAsync_HandlesAllPlayerStatuses()
    {
        // Arrange
        var session = new Session
        {
            SessionId = 1,
            SessionDate = DateTime.UtcNow.AddDays(1),
            Note = "Test session",
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };
        await _context.Sessions.AddAsync(session);

        var users = new[]
        {
            new AspNetUser
            {
                Id = "regular",
                UserName = "regular@test.com",
                FirstName = "Regular",
                LastName = "Player",
                LockerRoom13 = true,
                Active = true,
                Preferred = true,
                PreferredPlus = false
            },
            new AspNetUser
            {
                Id = "substitute",
                UserName = "sub@test.com",
                FirstName = "Sub",
                LastName = "Player",
                LockerRoom13 = true,
                Active = true,
                Preferred = false,
                PreferredPlus = false
            },
            new AspNetUser
            {
                Id = "queue",
                UserName = "queue@test.com",
                FirstName = "Queue",
                LastName = "Player",
                LockerRoom13 = true,
                Active = true,
                Preferred = false,
                PreferredPlus = false
            },
            new AspNetUser
            {
                Id = "nonlr13",
                UserName = "nonlr13@test.com",
                FirstName = "Non",
                LastName = "LR13",
                LockerRoom13 = false,
                Active = true,
                Preferred = false,
                PreferredPlus = false
            }
        };
        await _context.Users.AddRangeAsync(users);

        var rosters = new[]
        {
            new SessionRoster
            {
                SessionId = 1,
                UserId = "regular",
                IsRegular = true,
                IsPlaying = true,
                JoinedDateTime = DateTime.UtcNow,
                Position = 1
            },
            new SessionRoster
            {
                SessionId = 1,
                UserId = "substitute",
                IsRegular = false,
                IsPlaying = true,
                JoinedDateTime = DateTime.UtcNow,
                Position = 2
            }
        };
        await _context.SessionRosters.AddRangeAsync(rosters);

        var buySell = new BuySell
        {
            SessionId = 1,
            BuyerUserId = "queue",
            SellerUserId = null,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };
        await _context.BuySells.AddAsync(buySell);

        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetLockerRoom13SessionsAsync();

        // Assert
        result.Should().HaveCount(1);
        var players = result.Single().LockerRoom13Players;
        players.Should().HaveCount(5);

        players.Should().Contain(p => p.Id == "regular" && p.PlayerStatus == PlayerStatus.Regular);
        players.Should().Contain(p => p.Id == "substitute" && p.PlayerStatus == PlayerStatus.Substitute);
        players.Should().Contain(p => p.Id == "queue" && p.PlayerStatus == PlayerStatus.InQueue);
        players.Should().NotContain(p => p.Id == "nonlr13");
    }

    [Fact]
    public async Task GetLockerRoom13SessionsAsync_OrdersSessionsAndPlayers()
    {
        // Arrange
        var sessions = new[]
        {
            new Session
            {
                SessionId = 1,
                SessionDate = DateTime.UtcNow.AddDays(2),
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow
            },
            new Session
            {
                SessionId = 2,
                SessionDate = DateTime.UtcNow.AddDays(1),
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow
            }
        };
        await _context.Sessions.AddRangeAsync(sessions);

        // Act
        var result = await _repository.GetLockerRoom13SessionsAsync();

        // Assert
        var sessionList = result.ToList();
        sessionList.Should().BeInAscendingOrder(s => s.SessionDate);

        foreach (var session in sessionList)
        {
            session.LockerRoom13Players.Should().BeInAscendingOrder(p => p.LastName)
                .And.ThenBeInAscendingOrder(p => p.FirstName);
        }
    }
}
