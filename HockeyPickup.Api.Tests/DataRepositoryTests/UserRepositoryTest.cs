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

    public UserRepositoryTest()
    {
        _mockLogger = new Mock<ILogger<UserRepository>>();

        // Create the options builder for HockeyPickupContext
        var optionsBuilder = new DbContextOptionsBuilder<HockeyPickupContext>();
        optionsBuilder.UseSqlite("DataSource=:memory:");

        // Create the test context with the correct options type
        _context = new UserTestHockeyPickupContext(optionsBuilder.Options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

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
    public async Task GetBasicUsersAsync_ReturnsOnlyActiveUsers()
    {
        // Act
        var result = await _repository.GetBasicUsersAsync();

        // Assert
        result.Should().HaveCount(2); // Only active users
        result.Select(u => u.Id).Should().BeEquivalentTo(new[] { "user1", "user3" });
    }

    [Fact]
    public async Task GetBasicUsersAsync_MapsPropertiesCorrectly()
    {
        // Act
        var result = await _repository.GetBasicUsersAsync();

        // Assert
        var activeUser = result.Should().ContainSingle(u => u.Id == "user1").Subject;
        activeUser.Should().BeEquivalentTo(new UserBasicResponse
        {
            Id = "user1",
            UserName = "user1@example.com",
            Email = "user1@example.com",
            PayPalEmail = "user1@example.com",
            FirstName = "Active",
            LastName = "User",
            Preferred = true,
            PreferredPlus = false,
            Active = true,
            NotificationPreference = NotificationPreference.All,
            DateCreated = DateTime.Parse("02/25/1969")
        });
    }

    [Fact]
    public async Task GetDetailedUsersAsync_ReturnsOnlyActiveUsers()
    {
        // Act
        var result = await _repository.GetDetailedUsersAsync();

        // Assert
        result.Should().HaveCount(2); // Only active users
        result.Select(u => u.Id).Should().BeEquivalentTo(new[] { "user1", "user3" });
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
            Rating = 5.0m,
            Preferred = true,
            PreferredPlus = true,
            Active = true,
            NotificationPreference = NotificationPreference.All,
            DateCreated = DateTime.Parse("02/25/1969")
        });
    }

    [Fact]
    public async Task GetBasicUsersAsync_WithNoUsers_ReturnsEmptyList()
    {
        // Arrange
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        // Act
        var result = await _repository.GetBasicUsersAsync();

        // Assert
        result.Should().BeEmpty();
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
