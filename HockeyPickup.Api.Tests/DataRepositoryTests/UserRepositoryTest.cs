using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;
using Microsoft.Extensions.Logging;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Data.Context;
using Microsoft.AspNetCore.Identity;

namespace HockeyPickup.Api.Tests.Data.Repositories;

// Test-specific DbContext
public class TestHockeyPickupContext : HockeyPickupContext
{
    public TestHockeyPickupContext(DbContextOptions<HockeyPickupContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Start with a clean slate - don't call base.OnModelCreating

        // Configure only AspNetUser
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

            // Ignore ALL navigation properties and Identity-related properties
            entity.Ignore(e => e.Roles);
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

public class UserRepositoryTest : IDisposable
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
        _context = new TestHockeyPickupContext(optionsBuilder.Options);
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
                Rating = 4.5m
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
                Rating = 3.5m
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
                Rating = 5.0m
            }
        });
        _context.SaveChanges();

        _repository = new UserRepository(_context, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
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
            FirstName = "Active",
            LastName = "User",
            IsPreferred = true,
            IsPreferredPlus = false,
            Active = true
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
            FirstName = "Preferred",
            LastName = "Plus",
            Rating = 5.0m,
            IsPreferred = true,
            IsPreferredPlus = true,
            Active = true
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
