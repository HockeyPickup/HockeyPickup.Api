using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;
using Microsoft.Extensions.Logging;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Data.Context;

namespace HockeyPickup.Api.Tests.Data.Repositories;

// Test-specific DbContext
public class TestHockeyPickupContext : DbContext
{
    public TestHockeyPickupContext(DbContextOptions<TestHockeyPickupContext> options) : base(options)
    {
    }

    public DbSet<AspNetUser> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.ToTable("AspNetUsers");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.UserName).HasMaxLength(256);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FirstName).HasMaxLength(256);
            entity.Property(e => e.LastName).HasMaxLength(256);
            entity.Property(e => e.PayPalEmail).HasMaxLength(256);
            entity.Property(e => e.VenmoAccount).HasMaxLength(256);
            entity.Property(e => e.MobileLast4).HasMaxLength(4);
            entity.Property(e => e.EmergencyName).HasMaxLength(256);
            entity.Property(e => e.EmergencyPhone).HasMaxLength(20);
            entity.Property(e => e.Rating).HasColumnType("REAL");

            entity.Property(e => e.Active);
            entity.Property(e => e.Preferred);
            entity.Property(e => e.PreferredPlus);
            entity.Property(e => e.LockerRoom13);

            entity.Property(e => e.NotificationPreference);
        });
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

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new HockeyPickupContext(options); // Use the actual context
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
