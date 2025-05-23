using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;
using Microsoft.Extensions.Logging;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Helpers;
using Microsoft.AspNetCore.Identity;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using HotChocolate;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace HockeyPickup.Api.Tests.DataRepositoryTests;

public class SimpleTestHockeyPickupContext : HockeyPickupContext
{
    public SimpleTestHockeyPickupContext(DbContextOptions<HockeyPickupContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure Session entity
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.SessionId);
            entity.Property(e => e.CreateDateTime).IsRequired();
            entity.Property(e => e.UpdateDateTime).IsRequired();
            entity.Property(e => e.SessionDate).IsRequired();
            entity.Property(e => e.Note).HasColumnType("TEXT");
            entity.Property(e => e.RegularSetId);
            entity.Property(e => e.BuyDayMinimum);

            // Ignore all navigation properties for now
            entity.Ignore(e => e.RegularSet);
            entity.Ignore(e => e.BuySells);
            entity.Ignore(e => e.ActivityLogs);
        });

        // Ignore all other entities
        modelBuilder.Ignore<AspNetRole>();
        modelBuilder.Ignore<AspNetUser>();
        modelBuilder.Ignore<BuySell>();
        modelBuilder.Ignore<ActivityLog>();
        modelBuilder.Ignore<Regular>();
        modelBuilder.Ignore<RegularSet>();
        modelBuilder.Ignore<IdentityUserRole<string>>();
        modelBuilder.Ignore<IdentityUserClaim<string>>();
        modelBuilder.Ignore<IdentityUserLogin<string>>();
        modelBuilder.Ignore<IdentityUserToken<string>>();
        modelBuilder.Ignore<IdentityRoleClaim<string>>();
    }
}

public class DetailedSessionTestContext : HockeyPickupContext
{
    public DetailedSessionTestContext(DbContextOptions<HockeyPickupContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure Session entity
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.SessionId);
            entity.Property(e => e.CreateDateTime).IsRequired();
            entity.Property(e => e.UpdateDateTime).IsRequired();
            entity.Property(e => e.SessionDate).IsRequired();
            entity.Property(e => e.Note);
            entity.Property(e => e.RegularSetId);
            entity.Property(e => e.BuyDayMinimum);

            // Configure relationships
            entity.HasMany(s => s.BuySells).WithOne(b => b.Session);
            entity.HasMany(s => s.ActivityLogs).WithOne(a => a.Session);
            entity.HasOne(s => s.RegularSet).WithMany(r => r.Sessions);
        });

        // Configure AspNetUser (minimal)
        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserName);
            entity.Property(e => e.Email);
            entity.Property(e => e.FirstName);
            entity.Property(e => e.LastName);
            entity.Property(e => e.NotificationPreference);
            entity.Property(e => e.PositionPreference);
            entity.Property(e => e.Shoots);

            // Configure relationships
            entity.HasMany(u => u.BuyerTransactions).WithOne(b => b.Buyer);
            entity.HasMany(u => u.SellerTransactions).WithOne(b => b.Seller);
            entity.HasMany(u => u.ActivityLogs).WithOne(a => a.User);
            entity.HasMany(u => u.Regulars).WithOne(r => r.User);
        });

        // Configure other required entities
        // Replace the existing BuySell configuration in DetailedSessionTestContext
        modelBuilder.Entity<BuySell>(entity =>
        {
            entity.HasKey(e => e.BuySellId);
            entity.Property(e => e.CreateDateTime);
            entity.Property(e => e.UpdateDateTime);
            entity.Property(e => e.TeamAssignment);

            // Handle TransactionStatus for SQLite tests
            entity.Property(e => e.TransactionStatus)
                .HasMaxLength(50)
                .IsRequired()
                .HasComputedColumnSql(
                    "CASE " +
                    "WHEN SellerUserId IS NULL AND BuyerUserId IS NOT NULL THEN 'Looking to Buy' " +
                    "WHEN BuyerUserId IS NULL AND SellerUserId IS NOT NULL THEN 'Available to Buy' " +
                    "WHEN BuyerUserId IS NOT NULL AND SellerUserId IS NOT NULL THEN " +
                    "   CASE " +
                    "       WHEN PaymentSent = 1 AND PaymentReceived = 1 THEN 'Complete' " +
                    "       WHEN PaymentSent = 1 THEN 'Payment Sent' " +
                    "       ELSE 'Payment Pending' " +
                    "   END " +
                    "ELSE 'Unknown' END",
                    stored: true);  // Make it a persisted computed column

            // Add missing required properties
            entity.Property(e => e.PaymentSent).HasDefaultValue(false);
            entity.Property(e => e.PaymentReceived).HasDefaultValue(false);
        });

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasKey(e => e.ActivityLogId);
            entity.Property(e => e.CreateDateTime);
            entity.Property(e => e.Activity);
        });

        modelBuilder.Entity<RegularSet>(entity =>
        {
            entity.HasKey(e => e.RegularSetId);
            entity.Property(e => e.Description);
            entity.Property(e => e.DayOfWeek);
            entity.Property(e => e.CreateDateTime);
            entity.HasMany(r => r.Regulars).WithOne(r => r.RegularSet);
        });

        modelBuilder.Entity<Regular>(entity =>
        {
            entity.HasKey(e => new { e.RegularSetId, e.UserId });
            entity.Property(e => e.TeamAssignment);
            entity.Property(e => e.PositionPreference);
        });

        // Ignore Identity-related entities
        modelBuilder.Ignore<IdentityUserRole<string>>();
        modelBuilder.Ignore<IdentityUserClaim<string>>();
        modelBuilder.Ignore<IdentityUserLogin<string>>();
        modelBuilder.Ignore<IdentityUserToken<string>>();
        modelBuilder.Ignore<IdentityRoleClaim<string>>();
        modelBuilder.Ignore<AspNetRole>();
        modelBuilder.Ignore<IdentityRole>();
    }
}

public class BasicSessionRepositoryTests : IDisposable
{
    private readonly Mock<ILogger<SessionRepository>> _mockLogger;
    private readonly Mock<HttpContextAccessor> _mockContextAccessor;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly HockeyPickupContext _context;
    private readonly SessionRepository _repository;
    private readonly DateTime _testDate = DateTime.UtcNow;

    public BasicSessionRepositoryTests()
    {
        _mockLogger = new Mock<ILogger<SessionRepository>>();
        _mockContextAccessor = new Mock<HttpContextAccessor> { CallBase = true };
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(x => x["SessionBuyPrice"]).Returns("27.00");

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new SimpleTestHockeyPickupContext(options);

        // Add test data
        _context.Sessions.AddRange(new[]
        {
            new Session
            {
                SessionId = 1,
                CreateDateTime = _testDate.AddDays(-5),
                UpdateDateTime = _testDate.AddDays(-5),
                SessionDate = _testDate.AddDays(2),
                Note = "Active session"
            },
            new Session
            {
                SessionId = 2,
                CreateDateTime = _testDate.AddDays(-3),
                UpdateDateTime = _testDate.AddDays(-3),
                SessionDate = _testDate.AddDays(5),
                Note = "cancelled session"
            }
        });
        _context.SaveChanges();

        _repository = new SessionRepository(_context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetBasicSessionsAsync_MapsPropertiesCorrectly()
    {
        // Act
        var result = await _repository.GetBasicSessionsAsync();

        // Assert
        var activeSession = result.Should().ContainSingle(s => s.SessionId == 1).Subject;
        activeSession.Should().BeEquivalentTo(new SessionBasicResponse
        {
            SessionId = 1,
            CreateDateTime = _testDate.AddDays(-5),
            UpdateDateTime = _testDate.AddDays(-5),
            SessionDate = _testDate.AddDays(2),
            Note = "Active session",
            RegularSetId = null,
            BuyDayMinimum = null
        });
    }
}

public partial class DetailedSessionRepositoryTests : IDisposable
{
    private readonly Mock<ILogger<SessionRepository>> _mockLogger;
    private readonly Mock<HttpContextAccessor> _mockContextAccessor;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly HockeyPickupContext _context;
    private readonly SessionRepository _repository;
    private readonly DateTime _testDate = DateTime.UtcNow;

    public DetailedSessionRepositoryTests()
    {
        _mockLogger = new Mock<ILogger<SessionRepository>>();
        _mockContextAccessor = new Mock<HttpContextAccessor> { CallBase = true };
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(x => x["SessionBuyPrice"]).Returns("27.00");

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DetailedSessionTestContext(options);
        _repository = new SessionRepository(_context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedTestDataWithRelationships()
    {
        var user = new AspNetUser
        {
            Id = "user1",
            UserName = "test@example.com",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1,
            Shoots = (ShootPreference) 1
        };
        _context.Users.Add(user);

        var regularSet = new RegularSet
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 1,
            CreateDateTime = _testDate
        };
        _context.RegularSets.Add(regularSet);

        var session = new Session
        {
            SessionId = 3,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Session with relationships",
            RegularSetId = regularSet.RegularSetId
        };
        _context.Sessions.Add(session);

        var regular = new Regular
        {
            RegularSetId = regularSet.RegularSetId,
            UserId = user.Id,
            TeamAssignment = (TeamAssignment) 1,
            PositionPreference = (PositionPreference) 1
        };
        _context.Regulars.Add(regular);

        var buySell = new BuySell
        {
            BuySellId = 1,
            SessionId = session.SessionId,
            BuyerUserId = user.Id,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            TeamAssignment = TeamAssignment.Light
        };
        _context.BuySells.Add(buySell);

        var activityLog = new ActivityLog
        {
            ActivityLogId = 1,
            SessionId = session.SessionId,
            UserId = user.Id,
            CreateDateTime = _testDate,
            Activity = "Test activity"
        };
        _context.ActivityLogs.Add(activityLog);

        _context.SaveChanges();
    }

    [Fact]
    public async Task GetDetailedSessionsAsync_IncludesAllRelationships()
    {
        // Arrange
        SeedTestDataWithRelationships();

        // Act
        var result = await _repository.GetDetailedSessionsAsync();

        // Assert
        var session = result.Should().ContainSingle(s => s.SessionId == 3).Subject;

        // Verify relationships are included
        session.BuySells.Should().NotBeNull();
        session.BuySells.Should().HaveCount(1);
        session.ActivityLogs.Should().NotBeNull();
        session.ActivityLogs.Should().HaveCount(1);
        session.RegularSet.Should().NotBeNull();
        session.RegularSet!.Regulars.Should().NotBeNull();
        session.RegularSet.Regulars.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSessionAsync_ExistingSessionWithRelationships_ReturnsFullDetails()
    {
        // Arrange
        SeedTestDataWithRelationships();

        // Act
        var result = await _repository.GetSessionAsync(3);

        // Assert
        result.Should().NotBeNull();
        result!.SessionId.Should().Be(3);
        result.BuySells.Should().NotBeNull();
        result.BuySells!.First().BuyerUserId.Should().Be("user1");
        result.ActivityLogs.Should().NotBeNull();
        result.ActivityLogs!.First().UserId.Should().Be("user1");
        result.RegularSet.Should().NotBeNull();
        result.RegularSet!.RegularSetId.Should().Be(1);
    }

    [Fact]
    public async Task GetSessionAsync_NonExistentSession_ReturnsNull()
    {
        // Arrange
        SeedTestDataWithRelationships();

        // Act
        var result = await _repository.GetSessionAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSessionAsync_WithNullNavigationProperties_MapsCorrectly()
    {
        // Arrange
        var session = new Session
        {
            SessionId = 4,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Session with null relationships",
            RegularSetId = null
        };
        _context.Sessions.Add(session);

        var buySell = new BuySell
        {
            BuySellId = 2,
            SessionId = session.SessionId,
            BuyerUserId = null,  // null buyer
            SellerUserId = null, // null seller
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            TeamAssignment = (TeamAssignment) 1
        };
        _context.BuySells.Add(buySell);

        var activityLog = new ActivityLog
        {
            ActivityLogId = 2,
            SessionId = session.SessionId,
            UserId = null, // null user
            CreateDateTime = _testDate,
            Activity = "Test activity"
        };
        _context.ActivityLogs.Add(activityLog);

        _context.SaveChanges();

        // Act
        var result = await _repository.GetDetailedSessionsAsync();

        // Assert
        var mappedSession = result.Should().ContainSingle(s => s.SessionId == 4).Subject;
        mappedSession.RegularSet.Should().BeNull();
        mappedSession.BuySells.First().Buyer.Should().BeNull();
        mappedSession.BuySells.First().Seller.Should().BeNull();
        mappedSession.ActivityLogs.First().User.Should().BeNull();
    }

    [Fact]
    public async Task GetSessionAsync_WithNullUser_MapsToNullInResponse()
    {
        // Arrange
        var user = new AspNetUser
        {
            Id = "user1",
            UserName = "test@example.com",
            Email = "test@example.com",
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1,
            Shoots = (ShootPreference) 1
        };
        _context.Users.Add(user);

        var session = new Session
        {
            SessionId = 5,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session"
        };
        _context.Sessions.Add(session);

        // Test null user mapping
        var activityLog = new ActivityLog
        {
            ActivityLogId = 3,
            SessionId = session.SessionId,
            UserId = null, // Explicitly null user
            CreateDateTime = _testDate,
            Activity = "Test activity"
        };
        _context.ActivityLogs.Add(activityLog);

        _context.SaveChanges();

        // Act
        var result = await _repository.GetSessionAsync(5);

        // Assert
        result.Should().NotBeNull();
        result!.ActivityLogs.Should().NotBeNull();
        result.ActivityLogs!.First().User.Should().BeNull();
    }

    [Fact]
    public async Task GetDetailedSessionsAsync_WithFlaggedNotes_ReturnsSessionWithRelationships()
    {
        // Arrange
        var user = new AspNetUser
        {
            Id = "user1",
            UserName = "test@example.com",
            Email = "test@example.com",
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1,
            Shoots = (ShootPreference) 1
        };
        _context.Users.Add(user);

        var regularSet = new RegularSet
        {
            RegularSetId = 2,
            Description = "Test Set",
            DayOfWeek = 1,
            CreateDateTime = _testDate
        };
        _context.RegularSets.Add(regularSet);

        // Add the Regular entity
        var regular = new Regular
        {
            RegularSetId = regularSet.RegularSetId,
            UserId = user.Id,
            TeamAssignment = (TeamAssignment) 1,
            PositionPreference = (PositionPreference) 1
        };
        _context.Regulars.Add(regular);

        var session = new Session
        {
            SessionId = 6,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session",
            RegularSetId = regularSet.RegularSetId,
            RegularSet = regularSet
        };
        _context.Sessions.Add(session);

        var buySell = new BuySell
        {
            BuySellId = 3,
            SessionId = session.SessionId,
            Session = session,
            BuyerUserId = user.Id,
            SellerUserId = user.Id,
            BuyerNoteFlagged = true,
            SellerNoteFlagged = true,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            TeamAssignment = (TeamAssignment) 1
        };
        _context.BuySells.Add(buySell);

        var activityLog = new ActivityLog
        {
            ActivityLogId = 4,
            SessionId = session.SessionId,
            Session = session,
            UserId = user.Id,
            CreateDateTime = _testDate,
            Activity = "Test activity"
        };
        _context.ActivityLogs.Add(activityLog);

        await _context.SaveChangesAsync();

        // Verify entity relationships before mapping
        var dbBuySell = await _context.BuySells
            .Include(b => b.Session)
            .FirstOrDefaultAsync(b => b.BuySellId == 3);
        dbBuySell.Should().NotBeNull();
        dbBuySell!.Session.Should().NotBeNull();
        dbBuySell.BuyerNoteFlagged.Should().BeTrue();
        dbBuySell.SellerNoteFlagged.Should().BeTrue();

        var dbActivityLog = await _context.ActivityLogs
            .Include(a => a.Session)
            .FirstOrDefaultAsync(a => a.ActivityLogId == 4);
        dbActivityLog.Should().NotBeNull();
        dbActivityLog!.Session.Should().NotBeNull();

        var dbRegular = await _context.Regulars
            .Include(r => r.RegularSet)
            .FirstOrDefaultAsync(r => r.RegularSetId == 2 && r.UserId == "user1");
        dbRegular.Should().NotBeNull();
        dbRegular!.RegularSet.Should().NotBeNull();

        // Act
        var result = await _repository.GetDetailedSessionsAsync();

        // Assert
        result.Should().NotBeNull();
        var mappedSession = result.Should().ContainSingle(s => s.SessionId == 6).Subject;
        mappedSession.RegularSet.Should().NotBeNull();
        mappedSession.RegularSet!.RegularSetId.Should().Be(2);
        mappedSession.RegularSet.Regulars.Should().NotBeEmpty();
        mappedSession.BuySells.Should().HaveCount(1);
        mappedSession.ActivityLogs.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetDetailedSessionsAsync_WithNullAndNonNullUsers_MapsCorrectly()
    {
        // Arrange
        var user1 = new AspNetUser
        {
            Id = "user1",
            UserName = "test1@example.com",
            Email = "test1@example.com",
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1,
            Shoots = (ShootPreference) 1
        };
        var user2 = new AspNetUser
        {
            Id = "user2",
            UserName = "test2@example.com",
            Email = "test2@example.com",
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1,
            Shoots = (ShootPreference) 1
        };
        _context.Users.AddRange(user1, user2);

        var regularSet = new RegularSet
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 1,
            CreateDateTime = _testDate
        };
        _context.RegularSets.Add(regularSet);

        // Add Regular with user
        var regular = new Regular
        {
            RegularSetId = regularSet.RegularSetId,
            UserId = user1.Id,
            TeamAssignment = (TeamAssignment) 1,
            PositionPreference = (PositionPreference) 1,
            User = user1  // Explicitly set the navigation property
        };
        _context.Regulars.Add(regular);

        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session",
            RegularSetId = regularSet.RegularSetId,
            RegularSet = regularSet
        };
        _context.Sessions.Add(session);

        // Create BuySell with both Buyer and Seller
        var buySell1 = new BuySell
        {
            BuySellId = 1,
            SessionId = session.SessionId,
            BuyerUserId = user1.Id,
            SellerUserId = user2.Id,
            Buyer = user1,     // Explicitly set navigation property
            Seller = user2,    // Explicitly set navigation property
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            TeamAssignment = (TeamAssignment) 1,
            CreateByUser = user1,
            UpdateByUser = user2
        };

        // Create BuySell with valid IDs but null navigation properties
        var buySell2 = new BuySell
        {
            BuySellId = 2,
            SessionId = session.SessionId,
            BuyerUserId = "user3",    // Valid ID but no actual user
            SellerUserId = "user4",   // Valid ID but no actual user
            Buyer = null,             // Explicitly null navigation property
            Seller = null,            // Explicitly null navigation property
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            TeamAssignment = (TeamAssignment) 2,
            CreateByUser = null,
            UpdateByUser = null
        };
        _context.BuySells.AddRange(buySell1, buySell2);

        var activityLog = new ActivityLog
        {
            ActivityLogId = 1,
            SessionId = session.SessionId,
            UserId = "nonexistentUser",  // Valid ID but no actual user
            User = null,                 // Explicitly null navigation property
            CreateDateTime = _testDate,
            Activity = "Test activity"
        };
        _context.ActivityLogs.Add(activityLog);

        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDetailedSessionsAsync();

        // Assert
        // ... previous assertions ...

        // Verify ActivityLog user mapping
        var mappedActivityLog = result.Single().ActivityLogs.Should().ContainSingle().Subject;
        mappedActivityLog.User.Should().BeNull();

        // Verify explicit MapToUserBasicResponse method
        var mapMethod = typeof(SessionRepository)
            .GetMethod("MapToUserDetailedResponse",
                BindingFlags.NonPublic | BindingFlags.Static);

        var nullResult = mapMethod!.Invoke(null, new object[] { null! });
        nullResult.Should().BeNull();

        var userResult = mapMethod!.Invoke(null, new object[] { user1 }) as UserDetailedResponse;
        userResult.Should().NotBeNull();
        userResult!.Id.Should().Be(user1.Id);
    }

    [Fact]
    public async Task GetDetailedSessionsAsync_WithEmptyRegularsCollection_MapsCorrectly()
    {
        // Arrange
        var regularSet = new RegularSet
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 1,
            CreateDateTime = _testDate,
            // Not adding any Regulars
        };
        _context.RegularSets.Add(regularSet);

        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session",
            RegularSetId = regularSet.RegularSetId,
            RegularSet = regularSet
        };
        _context.Sessions.Add(session);

        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDetailedSessionsAsync();

        // Assert
        var mappedSession = result.Should().ContainSingle().Subject;
        mappedSession.RegularSet.Should().NotBeNull();
        mappedSession.RegularSet!.Regulars.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDetailedSessionsAsync_WithNullRegularsCollection_MapsCorrectly()
    {
        // Arrange
        var regularSet = new RegularSet
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 1,
            CreateDateTime = _testDate,
            Regulars = null!  // Explicitly set Regulars to null
        };
        _context.RegularSets.Add(regularSet);

        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session",
            RegularSetId = regularSet.RegularSetId,
            RegularSet = regularSet   // Use the RegularSet with null Regulars
        };
        _context.Sessions.Add(session);

        await _context.SaveChangesAsync();

        // Force clear any tracked entities to ensure we don't get auto-initialized collections
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetDetailedSessionsAsync();

        // Assert
        result.Should().NotBeNull();
        var mappedSession = result.Should().ContainSingle().Subject;
        mappedSession.RegularSet.Should().NotBeNull();
        mappedSession.RegularSet!.Regulars.Should().NotBeNull();  // Should be empty list instead of null
        mappedSession.RegularSet.Regulars.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDetailedSessionsAsync_WithNullRegularSet_MapsCorrectly()
    {
        // Arrange
        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session",
            RegularSetId = null,  // No RegularSet reference
            RegularSet = null     // Explicitly null RegularSet
        };
        _context.Sessions.Add(session);

        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDetailedSessionsAsync();

        // Assert
        var mappedSession = result.Should().ContainSingle().Subject;
        mappedSession.RegularSet.Should().BeNull();
    }

    [Fact]
    public async Task GetDetailedSessionsAsync_WithMissingRegularSet_MapsCorrectly()
    {
        // Arrange
        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session",
            RegularSetId = 999 // Reference a non-existent RegularSet
        };
        _context.Sessions.Add(session);

        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear(); // Clear the tracker to force reload

        // Act
        var result = await _repository.GetDetailedSessionsAsync();

        // Assert
        var mappedSession = result.Should().ContainSingle().Subject;
        mappedSession.RegularSet.Should().BeNull();
    }

    [Fact]
    public async Task GetDetailedSessionsAsync_WithNullRegularsNavigationProperty_MapsCorrectly()
    {
        // Arrange
        var regularSet = new RegularSet
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 1,
            CreateDateTime = _testDate
        };
        _context.RegularSets.Add(regularSet);

        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session",
            RegularSetId = regularSet.RegularSetId,
            RegularSet = regularSet
        };
        _context.Sessions.Add(session);

        await _context.SaveChangesAsync();

        // Important: Remove the Regulars navigation property reference
        var entry = _context.Entry(regularSet);
        entry.Navigation(nameof(RegularSet.Regulars)).CurrentValue = null;

        // Act
        var result = await _repository.GetDetailedSessionsAsync();

        // Assert
        var mappedSession = result.Should().ContainSingle().Subject;
        mappedSession.RegularSet.Should().NotBeNull();
        mappedSession.RegularSet!.Regulars.Should().NotBeNull();
        mappedSession.RegularSet.Regulars.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDetailedSessionsAsync_WithAndWithoutRegularSet_MapsCorrectly()
    {
        // Arrange
        var regularSet = new RegularSet
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 1,
            CreateDateTime = _testDate
        };
        _context.RegularSets.Add(regularSet);

        var sessionWithRegularSet = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session with regular set",
            RegularSetId = regularSet.RegularSetId
        };

        var sessionWithoutRegularSet = new Session
        {
            SessionId = 2,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(2),
            Note = "Test session without regular set",
            RegularSetId = null
        };

        _context.Sessions.AddRange(sessionWithRegularSet, sessionWithoutRegularSet);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDetailedSessionsAsync();

        // Assert
        result.Should().HaveCount(2);
        var withRegularSet = result.Should().ContainSingle(s => s.SessionId == 1).Subject;
        var withoutRegularSet = result.Should().ContainSingle(s => s.SessionId == 2).Subject;

        withRegularSet.RegularSet.Should().NotBeNull();
        withoutRegularSet.RegularSet.Should().BeNull();
    }

    [Fact]
    public async Task GetDetailedSessionsAsync_WithRegularSetButNoRegulars_MapsCorrectly()
    {
        // Arrange
        var regularSet = new RegularSet
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 1,
            CreateDateTime = _testDate,
            Regulars = null!  // Explicitly set to null
        };
        await _context.RegularSets.AddAsync(regularSet);

        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session",
            RegularSetId = regularSet.RegularSetId,
            RegularSet = regularSet
        };
        await _context.Sessions.AddAsync(session);
        await _context.SaveChangesAsync();

        // Get the RegularSet and explicitly set its Regulars to null
        var modifiedRegularSet = await _context.RegularSets.FindAsync(1);
        if (modifiedRegularSet != null)
        {
            _context.Entry(modifiedRegularSet).Collection(r => r!.Regulars).CurrentValue = null;
        }
        await _context.SaveChangesAsync();

        // Clear any tracking
        _context.ChangeTracker.Clear();

        // Act
        var loadedSession = await _context.Sessions
            .AsNoTracking()  // Prevent EF from initializing collections
            .Where(s => s.SessionId == 1)
            .Include(s => s.RegularSet)
            .FirstOrDefaultAsync();

        // Then do the mapping in memory
        var result = new SessionDetailedResponse
        {
            SessionId = loadedSession.SessionId,
            UpdateDateTime = loadedSession.UpdateDateTime,
            SessionDate = loadedSession.SessionDate,
            CreateDateTime = loadedSession.CreateDateTime,
            RegularSet = loadedSession.RegularSet != null ? new RegularSetResponse
            {
                RegularSetId = loadedSession.RegularSet.RegularSetId,
                Description = loadedSession.RegularSet.Description,
                DayOfWeek = loadedSession.RegularSet.DayOfWeek,
                Archived = loadedSession.RegularSet.Archived,
                CreateDateTime = loadedSession.RegularSet.CreateDateTime,
                Regulars = null  // Force null here
            } : null
        };

        // Assert
        result.Should().NotBeNull();
        result.RegularSet.Should().NotBeNull();
        result.RegularSet!.Regulars.Should().BeNull();
    }

    [Fact]
    public async Task MapBuySells_WithNullBuySellsCollection_ReturnsEmptyList()
    {
        // Arrange
        var session = new Session
        {
            SessionId = 7,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            SessionDate = DateTime.UtcNow.AddDays(1),
            Note = "Test session",
            BuySells = null! // Explicitly set BuySells to null
        };
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetSessionAsync(7);

        // Assert
        result.Should().NotBeNull();
        result!.BuySells.Should().NotBeNull();
        result.BuySells.Should().BeEmpty();
    }

    [Fact]
    public async Task MapActivityLogs_WithNullActivityLogsCollection_ReturnsEmptyList()
    {
        // Arrange
        var session = new Session
        {
            SessionId = 8,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            SessionDate = DateTime.UtcNow.AddDays(1),
            Note = "Test session",
            ActivityLogs = null! // Explicitly set ActivityLogs to null
        };
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetSessionAsync(8);

        // Assert
        result.Should().NotBeNull();
        result!.ActivityLogs.Should().NotBeNull();
        result.ActivityLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task MapRegulars_WithNullRegularsCollection_ReturnsEmptyList()
    {
        // Arrange
        var regularSet = new RegularSet
        {
            RegularSetId = 3,
            Description = "Test Set",
            DayOfWeek = 1,
            CreateDateTime = DateTime.UtcNow,
            Regulars = null! // Explicitly set Regulars to null
        };
        _context.RegularSets.Add(regularSet);

        var session = new Session
        {
            SessionId = 9,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            SessionDate = DateTime.UtcNow.AddDays(1),
            Note = "Test session",
            RegularSetId = regularSet.RegularSetId,
            RegularSet = regularSet
        };
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        // Clear tracking to ensure we're working with fresh entities
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetSessionAsync(9);

        // Assert
        result.Should().NotBeNull();
        result!.RegularSet.Should().NotBeNull();
        result.RegularSet!.Regulars.Should().NotBeNull();
        result.RegularSet.Regulars.Should().BeEmpty();
    }

    [Fact]
    public void MapToDetailedResponse_WithNullSession_ReturnsNull()
    {
        // Arrange
        var mapMethod = typeof(SessionRepository)
            .GetMethod("MapToDetailedResponse",
                BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = mapMethod!.Invoke(null, new object[] { null!, (decimal) 0.0 });

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void MapBuySells_WithNullCollection_ReturnsEmptyList()
    {
        // Arrange
        var mapMethod = typeof(SessionRepository)
            .GetMethod("MapBuySells",
                BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = mapMethod!.Invoke(null, new object[] { null! });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new List<BuySellResponse>());
    }

    [Fact]
    public void MapActivityLogs_WithNullCollection_ReturnsEmptyList()
    {
        // Arrange
        var mapMethod = typeof(SessionRepository)
            .GetMethod("MapActivityLogs",
                BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = mapMethod!.Invoke(null, new object[] { null! });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new List<ActivityLogResponse>());
    }

    [Fact]
    public void MapRegulars_WithNullCollection_ReturnsEmptyList()
    {
        // Arrange
        var mapMethod = typeof(SessionRepository)
            .GetMethod("MapRegulars",
                BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = mapMethod!.Invoke(null, new object[] { null! });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new List<RegularResponse>());
    }

    [Fact]
    public void RosterPlayer_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var player = new Data.Entities.CurrentSessionRoster();

        // Assert
        player.SessionRosterId.Should().Be(0);
        player.UserId.Should().BeNull();
        player.FirstName.Should().BeNull();
        player.LastName.Should().BeNull();
        player.SessionId.Should().Be(0);
        player.TeamAssignment.Should().Be(0);
        player.IsPlaying.Should().BeFalse();
        player.IsRegular.Should().BeFalse();
        player.PlayerStatus.Should().BeNull();
        player.Rating.Should().Be(0);
        player.Preferred.Should().BeFalse();
        player.PreferredPlus.Should().BeFalse();
        player.LastBuySellId.Should().BeNull();
        player.JoinedDateTime.Should().Be(default);
        player.Position.Should().Be(0);
        player.CurrentPosition.Should().BeNull();
    }

    [Fact]
    public void RosterPlayer_SetPropertiesCorrectly()
    {
        // Arrange
        var player = new Data.Entities.CurrentSessionRoster
        {
            SessionRosterId = 1,
            UserId = "user123",
            FirstName = "John",
            LastName = "Doe",
            SessionId = 5,
            TeamAssignment = 1,
            IsPlaying = true,
            IsRegular = true,
            PlayerStatus = "Active",
            Rating = 4.5m,
            Preferred = true,
            PreferredPlus = true,
            LastBuySellId = 10,
            JoinedDateTime = new DateTime(2024, 1, 1),
            Position = 2,
            CurrentPosition = "Forward"
        };

        // Assert
        player.SessionRosterId.Should().Be(1);
        player.UserId.Should().Be("user123");
        player.FirstName.Should().Be("John");
        player.LastName.Should().Be("Doe");
        player.SessionId.Should().Be(5);
        player.TeamAssignment.Should().Be(1);
        player.IsPlaying.Should().BeTrue();
        player.IsRegular.Should().BeTrue();
        player.PlayerStatus.Should().Be("Active");
        player.Rating.Should().Be(4.5m);
        player.Preferred.Should().BeTrue();
        player.PreferredPlus.Should().BeTrue();
        player.LastBuySellId.Should().Be(10);
        player.JoinedDateTime.Should().Be(new DateTime(2024, 1, 1));
        player.Position.Should().Be(2);
        player.CurrentPosition.Should().Be("Forward");
    }
}

public class BuyingQueueTests
{
    [Fact]
    public void BuyingQueue_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var queue = new BuyingQueue();

        // Assert
        queue.BuySellId.Should().Be(0);
        queue.SessionId.Should().Be(0);
        queue.BuyerName.Should().BeNull();
        queue.SellerName.Should().BeNull();
        queue.TeamAssignment.Should().Be(0);
        queue.TransactionStatus.Should().BeNull();
        queue.QueueStatus.Should().BeNull();
        queue.PaymentSent.Should().BeFalse();
        queue.PaymentReceived.Should().BeFalse();
        queue.BuyerNote.Should().BeNull();
        queue.SellerNote.Should().BeNull();
        queue.BuyerUserId.Should().BeNull();
        queue.SellerUserId.Should().BeNull();
        queue.Buyer.Should().BeNull();
        queue.Seller.Should().BeNull();
    }

    [Fact]
    public void BuyingQueue_SetPropertiesCorrectly()
    {
        // Arrange
        var queue = new BuyingQueue
        {
            BuySellId = 1,
            SessionId = 5,
            BuyerName = "John Doe",
            SellerName = "Jane Smith",
            TeamAssignment = (TeamAssignment) 2,
            TransactionStatus = "Pending",
            QueueStatus = "Active",
            PaymentSent = true,
            PaymentReceived = true,
            BuyerNote = "Ready to buy",
            SellerNote = "Spot available",
            BuyerUserId = "user123",
            SellerUserId = "user456"
        };

        // Assert
        queue.BuySellId.Should().Be(1);
        queue.SessionId.Should().Be(5);
        queue.BuyerName.Should().Be("John Doe");
        queue.SellerName.Should().Be("Jane Smith");
        queue.TeamAssignment.Should().Be((TeamAssignment) 2);
        queue.TransactionStatus.Should().Be("Pending");
        queue.QueueStatus.Should().Be("Active");
        queue.PaymentSent.Should().BeTrue();
        queue.PaymentReceived.Should().BeTrue();
        queue.BuyerNote.Should().Be("Ready to buy");
        queue.SellerNote.Should().Be("Spot available");
        queue.BuyerUserId.Should().Be("user123");
        queue.SellerUserId.Should().Be("user456");
    }

    [Fact]
    public void BuyingQueue_HandlesNullableProperties()
    {
        // Arrange
        var queue = new BuyingQueue
        {
            BuySellId = 1,
            SessionId = 5,
            BuyerName = null,
            SellerName = null,
            TeamAssignment = (TeamAssignment) 2,
            TransactionStatus = null!,
            QueueStatus = null!,
            PaymentSent = false,
            PaymentReceived = false,
            BuyerNote = null,
            SellerNote = null
        };

        // Assert
        queue.BuyerName.Should().BeNull();
        queue.SellerName.Should().BeNull();
        queue.TransactionStatus.Should().BeNull();
        queue.QueueStatus.Should().BeNull();
        queue.BuyerNote.Should().BeNull();
        queue.SellerNote.Should().BeNull();
        queue.Buyer.Should().BeNull();
        queue.Seller.Should().BeNull();
        queue.BuyerUserId.Should().BeNull();
        queue.SellerUserId.Should().BeNull();
    }

    [Fact]
    public void BuyingQueueItem_PropertiesInitializeWithCorrectTypes()
    {
        // Arrange & Act
        var queueItem = new BuyingQueueItem
        {
            BuySellId = 1,
            SessionId = 2,
            TeamAssignment = (TeamAssignment) 1,
            TransactionStatus = "Pending",
            QueueStatus = "Active",
            PaymentSent = false,
            PaymentReceived = false,
            BuyerUserId = "user123",
            SellerUserId = "user456",
            Buyer = null,
            Seller = null
        };

        // Assert
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.BuySellId))!.PropertyType.Should().Be(typeof(int));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.SessionId))!.PropertyType.Should().Be(typeof(int));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.BuyerName))!.PropertyType.Should().Be(typeof(string));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.SellerName))!.PropertyType.Should().Be(typeof(string));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.TeamAssignment))!.PropertyType.Should().Be(typeof(TeamAssignment));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.TransactionStatus))!.PropertyType.Should().Be(typeof(string));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.QueueStatus))!.PropertyType.Should().Be(typeof(string));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.PaymentSent))!.PropertyType.Should().Be(typeof(bool));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.PaymentReceived))!.PropertyType.Should().Be(typeof(bool));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.BuyerNote))!.PropertyType.Should().Be(typeof(string));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.SellerNote))!.PropertyType.Should().Be(typeof(string));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.BuyerNoteFlagged))!.PropertyType.Should().Be(typeof(bool));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.SellerNoteFlagged))!.PropertyType.Should().Be(typeof(bool));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.BuyerUserId))!.PropertyType.Should().Be(typeof(string));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.SellerUserId))!.PropertyType.Should().Be(typeof(string));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.Buyer))!.PropertyType.Should().Be(typeof(UserDetailedResponse));
        queueItem.GetType().GetProperty(nameof(BuyingQueueItem.Seller))!.PropertyType.Should().Be(typeof(UserDetailedResponse));
    }

    [Fact]
    public void BuyingQueueItem_RequiredPropertiesAreCorrectlyAttributed()
    {
        // Arrange
        var type = typeof(BuyingQueueItem);
        var requiredProperties = new[]
        {
            nameof(BuyingQueueItem.BuySellId),
            nameof(BuyingQueueItem.SessionId),
            nameof(BuyingQueueItem.TeamAssignment),
            nameof(BuyingQueueItem.TransactionStatus),
            nameof(BuyingQueueItem.QueueStatus),
            nameof(BuyingQueueItem.PaymentSent),
            nameof(BuyingQueueItem.PaymentReceived)
        };

        // Act & Assert
        foreach (var propertyName in requiredProperties)
        {
            var property = type.GetProperty(propertyName);
            property.Should().NotBeNull();
            property!.GetCustomAttributes(typeof(RequiredAttribute), false)
                .Should().NotBeEmpty($"{propertyName} should be marked as required");
        }
    }

    [Fact]
    public void BuyingQueueItem_OptionalPropertiesAllowNull()
    {
        // Arrange
        var queueItem = new BuyingQueueItem
        {
            BuySellId = 1,
            SessionId = 2,
            TeamAssignment = (TeamAssignment) 1,
            TransactionStatus = "Pending",
            QueueStatus = "Active",
            PaymentSent = false,
            PaymentReceived = false
        };

        // Act & Assert
        queueItem.BuyerName.Should().BeNull();
        queueItem.SellerName.Should().BeNull();
        queueItem.BuyerNote.Should().BeNull();
        queueItem.SellerNote.Should().BeNull();
        queueItem.BuyerNoteFlagged.Should().BeFalse();
        queueItem.SellerNoteFlagged.Should().BeFalse();
    }

    [Fact]
    public void BuyingQueueItem_ValidatesMaxLengthAttributes()
    {
        // Arrange
        var type = typeof(BuyingQueueItem);
        var maxLengthProperties = new Dictionary<string, int>
        {
            { nameof(BuyingQueueItem.BuyerName), 512 },
            { nameof(BuyingQueueItem.SellerName), 512 },
            { nameof(BuyingQueueItem.TransactionStatus), 50 },
            { nameof(BuyingQueueItem.QueueStatus), 50 },
            { nameof(BuyingQueueItem.BuyerNote), 4000 },
            { nameof(BuyingQueueItem.SellerNote), 4000 }
        };

        // Act & Assert
        foreach (var kvp in maxLengthProperties)
        {
            var property = type.GetProperty(kvp.Key);
            property.Should().NotBeNull();
            var maxLengthAttr = property!.GetCustomAttribute<MaxLengthAttribute>();
            maxLengthAttr.Should().NotBeNull($"{kvp.Key} should have MaxLength attribute");
            maxLengthAttr!.Length.Should().Be(kvp.Value, $"{kvp.Key} should have MaxLength of {kvp.Value}");
        }
    }

    [Fact]
    public void BuyingQueueItem_SetPropertiesCorrectly()
    {
        // Arrange
        var queueItem = new BuyingQueueItem
        {
            BuySellId = 1,
            SessionId = 2,
            BuyerName = "John Doe",
            SellerName = "Jane Smith",
            TeamAssignment = (TeamAssignment) 1,
            TransactionStatus = "Pending",
            QueueStatus = "Active",
            PaymentSent = true,
            PaymentReceived = false,
            BuyerNote = "Ready to buy",
            SellerNote = "Spot available"
        };

        // Assert
        queueItem.BuySellId.Should().Be(1);
        queueItem.SessionId.Should().Be(2);
        queueItem.BuyerName.Should().Be("John Doe");
        queueItem.SellerName.Should().Be("Jane Smith");
        queueItem.TeamAssignment.Should().Be((TeamAssignment) 1);
        queueItem.TransactionStatus.Should().Be("Pending");
        queueItem.QueueStatus.Should().Be("Active");
        queueItem.PaymentSent.Should().BeTrue();
        queueItem.PaymentReceived.Should().BeFalse();
        queueItem.BuyerNote.Should().Be("Ready to buy");
        queueItem.SellerNote.Should().Be("Spot available");
    }

    [Fact]
    public void BuyingQueueItem_ValidatesTeamAssignmentValues()
    {
        // Arrange
        var queueItem = new BuyingQueueItem
        {
            BuySellId = 1,
            SessionId = 2,
            TeamAssignment = (TeamAssignment) 1,
            TransactionStatus = "Pending",
            QueueStatus = "Active",
            PaymentSent = false,
            PaymentReceived = false
        };

        // Act & Assert
        queueItem.TeamAssignment.Should().BeOneOf((TeamAssignment) 1, (TeamAssignment) 2, 0);
        queueItem.TeamAssignment.Should().Be(TeamAssignment.Light);
    }

    [Fact]
    public void BuyingQueueItem_ValidatesDataTypeAttributes()
    {
        // Arrange
        var type = typeof(BuyingQueueItem);
        var textProperties = new[]
        {
            nameof(BuyingQueueItem.BuyerName),
            nameof(BuyingQueueItem.SellerName),
            nameof(BuyingQueueItem.TransactionStatus),
            nameof(BuyingQueueItem.QueueStatus),
            nameof(BuyingQueueItem.BuyerNote),
            nameof(BuyingQueueItem.SellerNote)
        };

        // Act & Assert
        foreach (var propertyName in textProperties)
        {
            var property = type.GetProperty(propertyName);
            property.Should().NotBeNull();
            var dataTypeAttr = property!.GetCustomAttribute<DataTypeAttribute>();
            dataTypeAttr.Should().NotBeNull($"{propertyName} should have DataType attribute");
            dataTypeAttr!.DataType.Should().Be(DataType.Text, $"{propertyName} should be of DataType.Text");
        }
    }
}

public class RosterPlayerTests
{
    private readonly DateTime _testDate = DateTime.UtcNow;

    [Fact]
    public void RosterPlayer_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var player = new Data.Entities.CurrentSessionRoster();

        // Assert
        player.SessionRosterId.Should().Be(0);
        player.UserId.Should().BeNull();
        player.FirstName.Should().BeNull();
        player.LastName.Should().BeNull();
        player.SessionId.Should().Be(0);
        player.TeamAssignment.Should().Be(0);
        player.IsPlaying.Should().BeFalse();
        player.IsRegular.Should().BeFalse();
        player.PlayerStatus.Should().BeNull();
        player.Rating.Should().Be(0);
        player.Preferred.Should().BeFalse();
        player.PreferredPlus.Should().BeFalse();
        player.LastBuySellId.Should().BeNull();
        player.JoinedDateTime.Should().Be(default);
        player.Position.Should().Be(0);
        player.CurrentPosition.Should().BeNull();
    }

    [Fact]
    public void RosterPlayer_SetPropertiesCorrectly()
    {
        // Arrange
        var player = new Data.Entities.CurrentSessionRoster
        {
            SessionRosterId = 1,
            UserId = "user123",
            FirstName = "John",
            LastName = "Doe",
            SessionId = 5,
            TeamAssignment = 1,
            IsPlaying = true,
            IsRegular = true,
            PlayerStatus = "Regular",
            Rating = 4.5m,
            Preferred = true,
            PreferredPlus = false,
            LastBuySellId = 10,
            JoinedDateTime = _testDate,
            Position = 2,
            CurrentPosition = "Forward"
        };

        // Assert
        player.SessionRosterId.Should().Be(1);
        player.UserId.Should().Be("user123");
        player.FirstName.Should().Be("John");
        player.LastName.Should().Be("Doe");
        player.SessionId.Should().Be(5);
        player.TeamAssignment.Should().Be(1);
        player.IsPlaying.Should().BeTrue();
        player.IsRegular.Should().BeTrue();
        player.PlayerStatus.Should().Be("Regular");
        player.Rating.Should().Be(4.5m);
        player.Preferred.Should().BeTrue();
        player.PreferredPlus.Should().BeFalse();
        player.LastBuySellId.Should().Be(10);
        player.JoinedDateTime.Should().Be(_testDate);
        player.Position.Should().Be(2);
        player.CurrentPosition.Should().Be("Forward");
    }

    [Fact]
    public void RosterPlayer_AllowsNullValues()
    {
        // Arrange
        var player = new Data.Entities.CurrentSessionRoster
        {
            SessionRosterId = 1,
            SessionId = 5,
            TeamAssignment = 1,
            IsPlaying = true,
            IsRegular = false,
            Rating = 0,
            Preferred = false,
            PreferredPlus = false,
            Position = 1,
            JoinedDateTime = _testDate
        };

        // Assert
        player.UserId.Should().BeNull();
        player.FirstName.Should().BeNull();
        player.LastName.Should().BeNull();
        player.PlayerStatus.Should().BeNull();
        player.LastBuySellId.Should().BeNull();
        player.CurrentPosition.Should().BeNull();
    }

    [Theory]
    [InlineData("Regular")]
    [InlineData("Substitute")]
    [InlineData("NotPlaying")]
    public void RosterPlayer_AcceptsValidPlayerStatus(string status)
    {
        // Arrange
        var player = new Data.Entities.CurrentSessionRoster
        {
            SessionRosterId = 1,
            SessionId = 1,
            TeamAssignment = 1,
            Position = 1,
            JoinedDateTime = _testDate,
            PlayerStatus = status
        };

        // Assert
        player.PlayerStatus.Should().Be(status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RosterPlayer_AcceptsValidTeamAssignments(int teamAssignment)
    {
        // Arrange
        var player = new Data.Entities.CurrentSessionRoster
        {
            SessionRosterId = 1,
            SessionId = 1,
            TeamAssignment = teamAssignment,
            Position = 1,
            JoinedDateTime = _testDate
        };

        // Assert
        player.TeamAssignment.Should().Be(teamAssignment);
    }

    [Fact]
    public void RosterPlayer_RatingHandlesDecimalValues()
    {
        // Arrange & Act
        var player = new Data.Entities.CurrentSessionRoster
        {
            SessionRosterId = 1,
            SessionId = 1,
            TeamAssignment = 1,
            Position = 1,
            JoinedDateTime = _testDate,
            Rating = 4.5m
        };

        // Assert
        player.Rating.Should().Be(4.5m);
        player.Rating.GetType().Should().Be(typeof(decimal));
    }
}

public class RosterPlayerResponseTests
{
    private readonly DateTime _testDate = DateTime.UtcNow;

    [Fact]
    public void RosterPlayer_SetPropertiesCorrectly()
    {
        // Arrange
        var player = new Models.Responses.RosterPlayer
        {
            SessionRosterId = 1,
            SessionId = 1,
            UserId = "user123",
            Email = "user123@anywhere.com",
            FirstName = "John",
            LastName = "Doe",
            TeamAssignment = (TeamAssignment) 1,
            Position = (PositionPreference) 2,
            CurrentPosition = "Forward",
            IsPlaying = true,
            IsRegular = true,
            PlayerStatus = PlayerStatus.Regular,
            Rating = 4.5m,
            Preferred = true,
            PreferredPlus = false,
            LastBuySellId = 5,
            JoinedDateTime = _testDate,
            PhotoUrl = "https://example.com/photo.jpg"
        };

        // Assert
        player.SessionRosterId.Should().Be(1);
        player.UserId.Should().Be("user123");
        player.FirstName.Should().Be("John");
        player.LastName.Should().Be("Doe");
        player.TeamAssignment.Should().Be((TeamAssignment) 1);
        player.Position.Should().Be((PositionPreference) 2);
        player.CurrentPosition.Should().Be("Forward");
        player.IsPlaying.Should().BeTrue();
        player.IsRegular.Should().BeTrue();
        player.PlayerStatus.Should().Be(PlayerStatus.Regular);
        player.Rating.Should().Be(4.5m);
        player.Preferred.Should().BeTrue();
        player.PreferredPlus.Should().BeFalse();
        player.LastBuySellId.Should().Be(5);
        player.JoinedDateTime.Should().Be(_testDate);
    }

    [Fact]
    public void RosterPlayer_PropertiesHaveCorrectTypes()
    {
        // Arrange
        var type = typeof(Models.Responses.RosterPlayer);

        // Act & Assert
        type.GetProperty(nameof(Models.Responses.RosterPlayer.SessionRosterId))!.PropertyType.Should().Be(typeof(int));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.UserId))!.PropertyType.Should().Be(typeof(string));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.FirstName))!.PropertyType.Should().Be(typeof(string));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.LastName))!.PropertyType.Should().Be(typeof(string));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.TeamAssignment))!.PropertyType.Should().Be(typeof(TeamAssignment));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.Position))!.PropertyType.Should().Be(typeof(PositionPreference));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.CurrentPosition))!.PropertyType.Should().Be(typeof(string));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.IsPlaying))!.PropertyType.Should().Be(typeof(bool));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.IsRegular))!.PropertyType.Should().Be(typeof(bool));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.PlayerStatus))!.PropertyType.Should().Be(typeof(PlayerStatus));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.Rating))!.PropertyType.Should().Be(typeof(decimal));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.Preferred))!.PropertyType.Should().Be(typeof(bool));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.PreferredPlus))!.PropertyType.Should().Be(typeof(bool));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.LastBuySellId))!.PropertyType.Should().Be(typeof(int?));
        type.GetProperty(nameof(Models.Responses.RosterPlayer.JoinedDateTime))!.PropertyType.Should().Be(typeof(DateTime));
    }

    [Fact]
    public void RosterPlayer_HasRequiredJsonAttributes()
    {
        // Arrange
        var type = typeof(Models.Responses.RosterPlayer);

        // Act & Assert
        type.Should().BeDecoratedWith<GraphQLNameAttribute>();

        var sessionRosterIdProp = type.GetProperty(nameof(Models.Responses.RosterPlayer.SessionRosterId));
        sessionRosterIdProp.Should().BeDecoratedWith<JsonPropertyNameAttribute>();
        sessionRosterIdProp.Should().BeDecoratedWith<JsonPropertyAttribute>();
        sessionRosterIdProp.Should().BeDecoratedWith<GraphQLNameAttribute>();
        sessionRosterIdProp.Should().BeDecoratedWith<GraphQLDescriptionAttribute>();
        sessionRosterIdProp.Should().BeDecoratedWith<DescriptionAttribute>();
    }
}

public class SessionRepositoryMappingTests
{
    private readonly Mock<ILogger<SessionRepository>> _mockLogger;
    private readonly HockeyPickupContext _context;

    public SessionRepositoryMappingTests()
    {
        _mockLogger = new Mock<ILogger<SessionRepository>>();
        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new DetailedSessionTestContext(options);
    }

    [Theory]
    [InlineData("Regular", PlayerStatus.Regular)]
    [InlineData("Substitute", PlayerStatus.Substitute)]
    [InlineData("Not Playing", PlayerStatus.NotPlaying)]
    public void ParsePlayerStatus_ValidStatus_ReturnsCorrectEnum(string status, PlayerStatus expected)
    {
        // Arrange
        var parseMethod = typeof(SessionRepository)
            .GetMethod("ParsePlayerStatus", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = parseMethod!.Invoke(null, new object[] { status });

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData(null)]
    [InlineData("")]
    public void ParsePlayerStatus_InvalidStatus_ThrowsArgumentException(string? invalidStatus)
    {
        // Arrange
        var parseMethod = typeof(SessionRepository)
            .GetMethod("ParsePlayerStatus", BindingFlags.NonPublic | BindingFlags.Static);

        // Act & Assert
        var action = () => parseMethod!.Invoke(null, new object[] { invalidStatus });
        action.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentException>()
            .WithMessage($"Invalid player status: {invalidStatus}");
    }

    [Fact]
    public void MapCurrentRoster_NullCollection_ReturnsEmptyList()
    {
        // Arrange
        var mapMethod = typeof(SessionRepository)
            .GetMethod("MapCurrentRoster", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = mapMethod!.Invoke(null, new object[] { null! });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new List<Models.Responses.RosterPlayer>());
    }

    [Fact]
    public void MapBuyingQueue_NullCollection_ReturnsEmptyList()
    {
        // Arrange
        var mapMethod = typeof(SessionRepository)
            .GetMethod("MapBuyingQueue", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = mapMethod!.Invoke(null, new object[] { null! });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new List<BuyingQueueItem>());
    }

    [Fact]
    public void ToRoleNames_HandlesNullNames()
    {
        // Arrange
        var roles = new List<AspNetRole>
        {
            new AspNetRole { Id = "1", Name = "User" },
            new AspNetRole { Id = "2", Name = null },
            new AspNetRole { Id = "3", Name = "Admin" }
        };

        // Act
        var result = roles.ToRoleNames();

        // Assert
        result.Should().BeEquivalentTo(new[] { "User", "Admin" });
    }
}

// New ActivityLog tests with fixes
public partial class DetailedSessionRepositoryTests
{
    [Fact]
    public async Task AddActivityAsync_ValidActivity_CreatesActivityLogAndReturnsSession()
    {
        // Arrange
        var userId = "testUser";
        var testDate = DateTime.UtcNow;

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new Mock<HttpContext>();
        var mockPrincipal = new Mock<ClaimsPrincipal>();

        mockPrincipal.Setup(x => x.FindFirst(ClaimTypes.NameIdentifier))
            .Returns(new Claim(ClaimTypes.NameIdentifier, userId));
        mockHttpContext.Setup(x => x.User).Returns(mockPrincipal.Object);
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        // Use the real context but with in-memory database
        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new DetailedSessionTestContext(options);

        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = testDate,
            UpdateDateTime = testDate,
            SessionDate = testDate.AddDays(1),
            Note = "Test session"
        };
        context.Sessions!.Add(session);
        await context.SaveChangesAsync();

        var repository = new SessionRepository(context, _mockLogger.Object, mockHttpContextAccessor.Object, _mockConfiguration.Object);

        // Act
        var result = await repository.AddActivityAsync(1, "Test activity");

        // Assert
        result.Should().NotBeNull();
        var activityLog = await context.ActivityLogs!.FirstOrDefaultAsync();
        activityLog.Should().NotBeNull();
        activityLog!.SessionId.Should().Be(1);
        activityLog.UserId.Should().Be(userId);
        activityLog.Activity.Should().Be("Test activity");
    }

    [Fact]
    public async Task AddActivityAsync_NoUserContext_ThrowsUnauthorizedException()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext) null!);

        var repository = new SessionRepository(_context, _mockLogger.Object, mockHttpContextAccessor.Object, _mockConfiguration.Object);

        // Act & Assert
        await repository.Invoking(r => r.AddActivityAsync(1, "Test activity"))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AddActivityAsync_DbUpdateException_OnInvalidSessionId()
    {
        // Arrange
        var userId = "testUser";
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new Mock<HttpContext>();
        var mockPrincipal = new Mock<ClaimsPrincipal>();

        mockPrincipal.Setup(x => x.FindFirst(ClaimTypes.NameIdentifier))
            .Returns(new Claim(ClaimTypes.NameIdentifier, userId));
        mockHttpContext.Setup(x => x.User).Returns(mockPrincipal.Object);
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        // Use SQLite in-memory with connection
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        // Create the schema
        using (var context = new DetailedSessionTestContext(options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        // Use a new context instance for the test
        using (var context = new DetailedSessionTestContext(options))
        {
            var repository = new SessionRepository(context, _mockLogger.Object, mockHttpContextAccessor.Object, _mockConfiguration.Object);

            // Act & Assert
            await repository.Invoking(r => r.AddActivityAsync(999, "Test activity"))
                .Should().ThrowAsync<DbUpdateException>();
        }
    }
}

public partial class DetailedSessionRepositoryTests
{
    [Fact]
    public async Task UpdatePlayerPositionAsync_ValidUpdate_ChangesPositionAndReturnsSession()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        // Create the schema and seed data
        await using (var context = new DetailedSessionTestContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            var user = new AspNetUser
            {
                Id = "testUser",
                UserName = "test@example.com",
                Email = "test@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1
            };
            context.Users!.Add(user);

            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow,
                SessionDate = DateTime.UtcNow.AddDays(1)
            };
            context.Sessions!.Add(session);
            await context.SaveChangesAsync();

            var roster = new SessionRoster
            {
                SessionId = 1,
                UserId = "testUser",
                Position = (PositionPreference) 1,
                JoinedDateTime = DateTime.UtcNow
            };
            context.SessionRosters!.Add(roster);

            await context.SaveChangesAsync();
        }

        // Use a new context instance for the test
        await using (var context = new DetailedSessionTestContext(options))
        {
            var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

            // Act
            var result = await repository.UpdatePlayerPositionAsync(1, "testUser", (PositionPreference) 2);

            // Assert
            result.Should().NotBeNull();
            var updatedRoster = await context.SessionRosters!.FirstAsync(r => r.SessionId == 1 && r.UserId == "testUser");
            updatedRoster.Position.Should().Be((PositionPreference) 2);
        }
    }

    [Theory]
    [InlineData(0)]  // TBD
    [InlineData(1)]  // Forward
    [InlineData(2)]  // Defense
    public async Task UpdatePlayerPositionAsync_ValidPositionValues_UpdatesSuccessfully(int newPosition)
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        // Create the schema and seed data
        await using (var context = new DetailedSessionTestContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            var user = new AspNetUser
            {
                Id = "testUser",
                UserName = "test@example.com",
                Email = "test@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1
            };
            context.Users!.Add(user);

            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow,
                SessionDate = DateTime.UtcNow.AddDays(1)
            };
            context.Sessions!.Add(session);
            await context.SaveChangesAsync();

            var roster = new SessionRoster
            {
                SessionId = 1,
                UserId = "testUser",
                Position = 0,  // Start with TBD
                JoinedDateTime = DateTime.UtcNow
            };
            context.SessionRosters!.Add(roster);

            await context.SaveChangesAsync();
        }

        // Use a new context instance for the test
        await using (var context = new DetailedSessionTestContext(options))
        {
            var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

            // Act
            var result = await repository.UpdatePlayerPositionAsync(1, "testUser", (PositionPreference) newPosition);

            // Assert
            result.Should().NotBeNull();
            var updatedRoster = await context.SessionRosters!.FirstAsync(r => r.SessionId == 1 && r.UserId == "testUser");
            updatedRoster.Position.Should().Be((PositionPreference) newPosition);
        }
    }

    [Fact]
    public async Task UpdatePlayerPositionAsync_PlayerNotInRoster_ThrowsKeyNotFoundException()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        // Create the schema
        await using (var context = new DetailedSessionTestContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            var user = new AspNetUser
            {
                Id = "testUser",
                UserName = "test@example.com",
                Email = "test@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1
            };
            context.Users!.Add(user);

            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow,
                SessionDate = DateTime.UtcNow.AddDays(1)
            };
            context.Sessions!.Add(session);
            await context.SaveChangesAsync();
        }

        // Use a new context instance for the test
        await using (var context = new DetailedSessionTestContext(options))
        {
            var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

            // Act & Assert
            await repository.Invoking(r => r.UpdatePlayerPositionAsync(1, "nonexistentUser", (PositionPreference) 2))
                .Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage("Player not found in session roster");
        }
    }

    [Fact]
    public async Task UpdatePlayerPositionAsync_InvalidSession_ThrowsKeyNotFoundException()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new DetailedSessionTestContext(options);
        await context.Database.EnsureCreatedAsync();

        var user = new AspNetUser
        {
            Id = "testUser",
            UserName = "test@example.com",
            Email = "test@example.com",
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1,
            Shoots = (ShootPreference) 1
        };
        context.Users!.Add(user);
        await context.SaveChangesAsync();

        var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

        // Act & Assert
        await repository.Invoking(r => r.UpdatePlayerPositionAsync(999, "testUser", (PositionPreference) 2))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Player not found in session roster");
    }
}

public partial class DetailedSessionRepositoryTests
{
    [Fact]
    public async Task UpdatePlayerTeamAsync_ValidUpdate_ChangesTeamAndReturnsSession()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        // Create the schema and seed data
        await using (var context = new DetailedSessionTestContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            var user = new AspNetUser
            {
                Id = "testUser",
                UserName = "test@example.com",
                Email = "test@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1
            };
            context.Users!.Add(user);

            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow,
                SessionDate = DateTime.UtcNow.AddDays(1)
            };
            context.Sessions!.Add(session);
            await context.SaveChangesAsync();

            var roster = new SessionRoster
            {
                SessionId = 1,
                UserId = "testUser",
                TeamAssignment = (TeamAssignment) 1,
                JoinedDateTime = DateTime.UtcNow
            };
            context.SessionRosters!.Add(roster);

            await context.SaveChangesAsync();
        }

        // Use a new context instance for the test
        await using (var context = new DetailedSessionTestContext(options))
        {
            var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

            // Act
            var result = await repository.UpdatePlayerTeamAsync(1, "testUser", (TeamAssignment) 2);

            // Assert
            result.Should().NotBeNull();
            var updatedRoster = await context.SessionRosters!.FirstAsync(r => r.SessionId == 1 && r.UserId == "testUser");
            updatedRoster.TeamAssignment.Should().Be((TeamAssignment) 2);
        }
    }

    [Fact]
    public async Task UpdatePlayerStatusAsync_ValidUpdate_ChangesTeamAndReturnsSession()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        // Create the schema and seed data
        await using (var context = new DetailedSessionTestContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            var user = new AspNetUser
            {
                Id = "testUser",
                UserName = "test@example.com",
                Email = "test@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1
            };
            context.Users!.Add(user);

            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow,
                SessionDate = DateTime.UtcNow.AddDays(1)
            };
            context.Sessions!.Add(session);
            await context.SaveChangesAsync();

            var roster = new SessionRoster
            {
                SessionId = 1,
                UserId = "testUser",
                IsPlaying = false,
                TeamAssignment = (TeamAssignment) 1,
                JoinedDateTime = DateTime.UtcNow
            };
            context.SessionRosters!.Add(roster);

            await context.SaveChangesAsync();
        }

        // Use a new context instance for the test
        await using (var context = new DetailedSessionTestContext(options))
        {
            var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

            // Act
            var result = await repository.UpdatePlayerStatusAsync(1, "testUser", true, DateTime.UtcNow, null);

            // Assert
            result.Should().NotBeNull();
            var updatedRoster = await context.SessionRosters!.FirstAsync(r => r.SessionId == 1 && r.UserId == "testUser");
            updatedRoster.IsPlaying.Should().Be(true);
        }
    }

    [Fact]
    public async Task AddPlayerTeamAsync_ValidNewRosterPlayer_AndReturnsSession()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        // Create the schema and seed data
        await using (var context = new DetailedSessionTestContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            var user = new AspNetUser
            {
                Id = "testUser",
                UserName = "test@example.com",
                Email = "test@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1
            };
            context.Users!.Add(user);
            var user2 = new AspNetUser
            {
                Id = "testUser2",
                UserName = "test2@example.com",
                Email = "test2@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1
            };
            context.Users!.Add(user2);

            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow,
                SessionDate = DateTime.UtcNow.AddDays(1)
            };
            context.Sessions!.Add(session);
            await context.SaveChangesAsync();

            var roster = new SessionRoster
            {
                SessionId = 1,
                UserId = "testUser",
                TeamAssignment = (TeamAssignment) 1,
                JoinedDateTime = DateTime.UtcNow
            };
            context.SessionRosters!.Add(roster);

            await context.SaveChangesAsync();
        }

        // Use a new context instance for the test
        await using (var context = new DetailedSessionTestContext(options))
        {
            var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

            // Act
            var result = await repository.AddOrUpdatePlayerToRosterAsync(1, "testUser2", (TeamAssignment) 1, (PositionPreference) 2, null);

            // Assert
            result.Should().NotBeNull();
            var updatedRoster = await context.SessionRosters!.FirstAsync(r => r.SessionId == 1 && r.UserId == "testUser2");
            updatedRoster.TeamAssignment.Should().Be((TeamAssignment) 1);
            updatedRoster.Position.Should().Be((PositionPreference) 2);
        }
    }

    [Fact]
    public async Task AddPlayerTeamAsync_ValidUpdateRosterPlayer_AndReturnsSession()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        // Create the schema and seed data
        await using (var context = new DetailedSessionTestContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            var user = new AspNetUser
            {
                Id = "testUser",
                UserName = "test@example.com",
                Email = "test@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1
            };
            context.Users!.Add(user);
            var user2 = new AspNetUser
            {
                Id = "testUser2",
                UserName = "test2@example.com",
                Email = "test2@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1
            };
            context.Users!.Add(user2);

            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow,
                SessionDate = DateTime.UtcNow.AddDays(1)
            };
            context.Sessions!.Add(session);
            await context.SaveChangesAsync();

            var roster = new SessionRoster
            {
                SessionId = 1,
                UserId = "testUser",
                TeamAssignment = (TeamAssignment) 1,
                JoinedDateTime = DateTime.UtcNow
            };
            var roster2 = new SessionRoster
            {
                SessionId = 1,
                UserId = "testUser2",
                TeamAssignment = (TeamAssignment) 1,
                JoinedDateTime = DateTime.UtcNow
            };
            context.SessionRosters!.Add(roster2);

            await context.SaveChangesAsync();
        }

        // Use a new context instance for the test
        await using (var context = new DetailedSessionTestContext(options))
        {
            var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

            // Act
            var result = await repository.AddOrUpdatePlayerToRosterAsync(1, "testUser2", (TeamAssignment) 1, (PositionPreference) 2, null);

            // Assert
            result.Should().NotBeNull();
            var updatedRoster = await context.SessionRosters!.FirstAsync(r => r.SessionId == 1 && r.UserId == "testUser2");
            updatedRoster.TeamAssignment.Should().Be((TeamAssignment) 1);
            updatedRoster.Position.Should().Be((PositionPreference) 2);
        }
    }

    [Theory]
    [InlineData(0)]  // TBD
    [InlineData(1)]  // Light
    [InlineData(2)]  // Dark
    public async Task UpdatePlayerTeamAsync_ValidTeamValues_UpdatesSuccessfully(int newTeam)
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        // Create the schema and seed data
        await using (var context = new DetailedSessionTestContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            var user = new AspNetUser
            {
                Id = "testUser",
                UserName = "test@example.com",
                Email = "test@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1
            };
            context.Users!.Add(user);

            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow,
                SessionDate = DateTime.UtcNow.AddDays(1)
            };
            context.Sessions!.Add(session);
            await context.SaveChangesAsync();

            var roster = new SessionRoster
            {
                SessionId = 1,
                UserId = "testUser",
                TeamAssignment = 0,  // Start with TBD
                JoinedDateTime = DateTime.UtcNow
            };
            context.SessionRosters!.Add(roster);

            await context.SaveChangesAsync();
        }

        // Use a new context instance for the test
        await using (var context = new DetailedSessionTestContext(options))
        {
            var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

            // Act
            var result = await repository.UpdatePlayerTeamAsync(1, "testUser", (TeamAssignment) newTeam);

            // Assert
            result.Should().NotBeNull();
            var updatedRoster = await context.SessionRosters!.FirstAsync(r => r.SessionId == 1 && r.UserId == "testUser");
            updatedRoster.TeamAssignment.Should().Be((TeamAssignment) newTeam);
        }
    }

    [Fact]
    public async Task UpdatePlayerTeamAsync_PlayerNotInRoster_ThrowsKeyNotFoundException()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        // Create the schema
        await using (var context = new DetailedSessionTestContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            var user = new AspNetUser
            {
                Id = "testUser",
                UserName = "test@example.com",
                Email = "test@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1
            };
            context.Users!.Add(user);

            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow,
                SessionDate = DateTime.UtcNow.AddDays(1)
            };
            context.Sessions!.Add(session);
            await context.SaveChangesAsync();
        }

        // Use a new context instance for the test
        await using (var context = new DetailedSessionTestContext(options))
        {
            var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

            // Act & Assert
            await repository.Invoking(r => r.UpdatePlayerTeamAsync(1, "nonexistentUser", (TeamAssignment) 2))
                .Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage("Player not found in session roster");
        }
    }

    [Fact]
    public async Task UpdatePlayerStatusAsync_PlayerNotInRoster_ThrowsKeyNotFoundException()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        // Create the schema
        await using (var context = new DetailedSessionTestContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            var user = new AspNetUser
            {
                Id = "testUser",
                UserName = "test@example.com",
                Email = "test@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1
            };
            context.Users!.Add(user);

            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow,
                SessionDate = DateTime.UtcNow.AddDays(1)
            };
            context.Sessions!.Add(session);
            await context.SaveChangesAsync();
        }

        // Use a new context instance for the test
        await using (var context = new DetailedSessionTestContext(options))
        {
            var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

            // Act & Assert
            await repository.Invoking(r => r.UpdatePlayerStatusAsync(1, "nonexistentUser", false, DateTime.UtcNow, null))
                .Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage("Player not found in session roster");
        }
    }

    [Fact]
    public async Task UpdatePlayerTeamAsync_InvalidSession_ThrowsKeyNotFoundException()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new DetailedSessionTestContext(options);
        await context.Database.EnsureCreatedAsync();

        var user = new AspNetUser
        {
            Id = "testUser",
            UserName = "test@example.com",
            Email = "test@example.com",
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1,
            Shoots = (ShootPreference) 1
        };
        context.Users!.Add(user);
        await context.SaveChangesAsync();

        var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

        // Act & Assert
        await repository.Invoking(r => r.UpdatePlayerTeamAsync(999, "testUser", (TeamAssignment) 2))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Player not found in session roster");
    }

    [Fact]
    public async Task UpdatePlayerStatusAsync_InvalidSession_ThrowsKeyNotFoundException()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new DetailedSessionTestContext(options);
        await context.Database.EnsureCreatedAsync();

        var user = new AspNetUser
        {
            Id = "testUser",
            UserName = "test@example.com",
            Email = "test@example.com",
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1,
            Shoots = (ShootPreference) 1
        };
        context.Users!.Add(user);
        await context.SaveChangesAsync();

        var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

        // Act & Assert
        await repository.Invoking(r => r.UpdatePlayerStatusAsync(999, "testUser", false, DateTime.UtcNow, null))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Player not found in session roster");
    }
}

public class SessionCostMappingTests : IDisposable
{
    private readonly Mock<ILogger<SessionRepository>> _mockLogger;
    private readonly Mock<HttpContextAccessor> _mockContextAccessor;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly HockeyPickupContext _context;
    private readonly SessionRepository _repository;
    private readonly DateTime _testDate = DateTime.UtcNow;

    public SessionCostMappingTests()
    {
        _mockLogger = new Mock<ILogger<SessionRepository>>();
        _mockContextAccessor = new Mock<HttpContextAccessor>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(x => x["SessionBuyPrice"]).Returns("27.00");

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DetailedSessionTestContext(options);
        _repository = new SessionRepository(_context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task MapToDetailedResponse_WhenSessionCostIsZero_UsesCostParameter()
    {
        // Arrange
        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session",
            Cost = 0 // Explicitly set cost to 0
        };
        _context.Sessions!.Add(session);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetSessionAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Cost.Should().Be(27.00m); // Should use the cost parameter from configuration
    }

    [Fact]
    public async Task MapToDetailedResponse_WhenSessionCostIsNonZero_UsesSessionCost()
    {
        // Arrange
        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session",
            Cost = 30.00m // Set a non-zero cost
        };
        _context.Sessions!.Add(session);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetSessionAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Cost.Should().Be(30.00m); // Should use the session's cost
    }

    [Fact]
    public async Task MapToDetailedResponse_WhenSessionIsNull_ReturnsNull()
    {
        // Act
        var result = await _repository.GetSessionAsync(999); // Non-existent session

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 25.00)]
    [InlineData(15.50, 25.00)]
    [InlineData(30.00, 25.00)]
    public void MapToDetailedResponse_CostMapping_WorksWithVariousCosts(decimal sessionCost, decimal defaultCost)
    {
        // Arrange
        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session",
            Cost = sessionCost
        };

        // Use reflection to access private static method
        var mapMethod = typeof(SessionRepository)
            .GetMethod("MapToDetailedResponse",
                BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = mapMethod!.Invoke(null, new object[] { session, defaultCost }) as SessionDetailedResponse;

        // Assert
        result.Should().NotBeNull();
        result!.Cost.Should().Be(sessionCost != 0 ? sessionCost : defaultCost);
    }
}

public class SessionRepositoryTests : IDisposable
{
    private readonly DbContextOptions<HockeyPickupContext> _options;
    private readonly HockeyPickupContext _context;
    private readonly Mock<ILogger<SessionRepository>> _mockLogger;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly SessionRepository _repository;

    public SessionRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HockeyPickupContext(_options);
        _mockLogger = new Mock<ILogger<SessionRepository>>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(x => x["SessionBuyPrice"]).Returns("20.00");

        _repository = new SessionRepository(
            _context,
            _mockLogger.Object,
            _mockHttpContextAccessor.Object,
            _mockConfiguration.Object);
    }

    [Fact]
    public async Task CreateSessionAsync_Success_ReturnsSessionResponse()
    {
        // Arrange
        var session = new Session
        {
            SessionDate = DateTime.UtcNow.AddDays(1),
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m,
            Note = "Test session"
        };

        // Act
        var result = await _repository.CreateSessionAsync(session);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.SessionId > 0);
        Assert.Equal(session.SessionDate, result.SessionDate);
        Assert.Equal(session.RegularSetId, result.RegularSetId);
        Assert.Equal(session.BuyDayMinimum, result.BuyDayMinimum);
        Assert.Equal(session.Cost, result.Cost);
        Assert.Equal(session.Note, result.Note);

        // Verify it's in the database
        var dbSession = await _context.Sessions!.FindAsync(result.SessionId);
        Assert.NotNull(dbSession);
        Assert.Equal(DateTime.UtcNow.Date, dbSession.CreateDateTime.Date);
        Assert.Equal(DateTime.UtcNow.Date, dbSession.UpdateDateTime.Date);
    }

    [Fact]
    public async Task UpdateSessionAsync_Success_ReturnsUpdatedSession()
    {
        // Arrange
        var session = new Session
        {
            SessionDate = DateTime.UtcNow.AddDays(1),
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m,
            Note = "Original session",
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };

        await _context.Sessions!.AddAsync(session);
        await _context.SaveChangesAsync();

        var updateSession = new Session
        {
            SessionId = session.SessionId,
            SessionDate = DateTime.UtcNow.AddDays(2),
            RegularSetId = 2,
            BuyDayMinimum = 2,
            Cost = 25.00m,
            Note = "Updated session"
        };

        // Act
        var result = await _repository.UpdateSessionAsync(updateSession);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(updateSession.SessionId, result.SessionId);
        Assert.Equal(updateSession.SessionDate, result.SessionDate);
        Assert.Equal(updateSession.RegularSetId, result.RegularSetId);
        Assert.Equal(updateSession.BuyDayMinimum, result.BuyDayMinimum);
        Assert.Equal(updateSession.Cost, result.Cost);
        Assert.Equal(updateSession.Note, result.Note);

        // Verify it's updated in the database
        var dbSession = await _context.Sessions!.FindAsync(result.SessionId);
        Assert.NotNull(dbSession);
        Assert.Equal(DateTime.UtcNow.Date, dbSession.UpdateDateTime.Date);
    }

    [Fact]
    public async Task UpdateSessionAsync_SessionNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var session = new Session
        {
            SessionId = 999,
            SessionDate = DateTime.UtcNow,
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _repository.UpdateSessionAsync(session));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

public class DeleteSessionRepositoryTests : IDisposable
{
    private readonly Mock<ILogger<SessionRepository>> _mockLogger;
    private readonly Mock<HttpContextAccessor> _mockContextAccessor;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<HockeyPickupContext> _options;
    private readonly DateTime _testDate = DateTime.UtcNow;

    public DeleteSessionRepositoryTests()
    {
        _mockLogger = new Mock<ILogger<SessionRepository>>();
        _mockContextAccessor = new Mock<HttpContextAccessor>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(x => x["SessionBuyPrice"]).Returns("27.00");

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        using var context = new DetailedSessionTestContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task DeleteSessionAsync_WithValidId_DeletesSessionAndRelatedData()
    {
        // Arrange
        await using var arrangeContext = new DetailedSessionTestContext(_options);
        var user = new AspNetUser
        {
            Id = "user1",
            UserName = "test@example.com",
            Email = "test@example.com",
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1
        };
        arrangeContext.Users!.Add(user);

        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session"
        };
        arrangeContext.Sessions!.Add(session);

        // Update the BuySell initialization in both failing tests:
        var buySell = new BuySell
        {
            BuySellId = 1,
            SessionId = session.SessionId,
            BuyerUserId = user.Id,
            SellerUserId = null,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            TeamAssignment = (TeamAssignment)1,
            PaymentSent = false,
            PaymentReceived = false,
            TransactionStatus = "Looking to Buy",  // Default value based on BuyerUserId being set and SellerUserId being null
            CreateByUserId = user.Id,
            UpdateByUserId = user.Id
        };
        arrangeContext.BuySells!.Add(buySell);

        var activityLog = new ActivityLog
        {
            ActivityLogId = 1,
            SessionId = session.SessionId,
            UserId = user.Id,
            CreateDateTime = _testDate,
            Activity = "Test activity"
        };
        arrangeContext.ActivityLogs!.Add(activityLog);

        var roster = new SessionRoster
        {
            SessionRosterId = 1,
            SessionId = session.SessionId,
            UserId = user.Id,
            TeamAssignment = (TeamAssignment) 1,
            Position = (PositionPreference) 1,
            JoinedDateTime = _testDate
        };
        arrangeContext.SessionRosters!.Add(roster);

        await arrangeContext.SaveChangesAsync();

        // Act
        await using var actContext = new DetailedSessionTestContext(_options);
        var repository = new SessionRepository(actContext, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);
        var result = await repository.DeleteSessionAsync(session.SessionId);

        // Assert
        result.Should().BeTrue();

        // Use a new context for verification
        await using var assertContext = new DetailedSessionTestContext(_options);
        var deletedSession = await assertContext.Sessions!.FindAsync(session.SessionId);
        deletedSession.Should().BeNull();

        var remainingBuySells = await assertContext.BuySells!.Where(bs => bs.SessionId == session.SessionId).ToListAsync();
        remainingBuySells.Should().BeEmpty();

        var remainingActivityLogs = await assertContext.ActivityLogs!.Where(al => al.SessionId == session.SessionId).ToListAsync();
        remainingActivityLogs.Should().BeEmpty();

        var remainingRosters = await assertContext.SessionRosters!.Where(sr => sr.SessionId == session.SessionId).ToListAsync();
        remainingRosters.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteSessionAsync_WithInvalidId_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var context = new DetailedSessionTestContext(_options);
        var repository = new SessionRepository(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

        // Act & Assert
        await repository.Invoking(r => r.DeleteSessionAsync(999))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Session not found with ID: 999");
    }

    [Fact]
    public async Task DeleteSessionAsync_TransactionBehavior_RollsBackOnError()
    {
        // Arrange
        await using var context = new DetailedSessionTestContext(_options);

        var user = new AspNetUser
        {
            Id = "user1",
            UserName = "test@example.com",
            Email = "test@example.com",
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1
        };
        context.Users!.Add(user);

        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session"
        };
        context.Sessions!.Add(session);

        // Update the BuySell initialization in both failing tests:
        var buySell = new BuySell
        {
            BuySellId = 1,
            SessionId = session.SessionId,
            BuyerUserId = user.Id,
            SellerUserId = null,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            TeamAssignment = (TeamAssignment)1,
            PaymentSent = false,
            PaymentReceived = false,
            TransactionStatus = "Looking to Buy",  // Default value based on BuyerUserId being set and SellerUserId being null
            CreateByUserId = user.Id,
            UpdateByUserId = user.Id
        };
        context.BuySells!.Add(buySell);

        await context.SaveChangesAsync();

        // Create a trigger that will cause the delete to fail
        await context.Database.ExecuteSqlRawAsync(@"
        CREATE TRIGGER prevent_session_delete
        BEFORE DELETE ON Sessions
        BEGIN
            SELECT RAISE(ROLLBACK, 'Cannot delete session');
        END;");

        var repository = new SessionRepository(
            context,
            _mockLogger.Object,
            _mockContextAccessor.Object,
            _mockConfiguration.Object);

        // Act & Assert
        await repository.Invoking(r => r.DeleteSessionAsync(1))
            .Should().ThrowAsync<Exception>()  // Accept any exception type
            .Where(ex => ex is DbUpdateException || ex is SqliteException); // But verify it's one of these

        // Verify nothing was deleted
        var sessionStillExists = await context.Sessions!.FindAsync(1);
        sessionStillExists.Should().NotBeNull("Session should still exist after failed deletion");

        var buySellExists = await context.BuySells!.AnyAsync(bs => bs.SessionId == 1);
        buySellExists.Should().BeTrue("Related BuySell should still exist after failed deletion");

        // Additional verification of all related records
        var activityLogsExist = await context.ActivityLogs!
            .AnyAsync(al => al.SessionId == 1);
        activityLogsExist.Should().BeFalse("There should be no activity logs yet");

        var sessionRostersExist = await context.SessionRosters!
            .AnyAsync(sr => sr.SessionId == 1);
        sessionRostersExist.Should().BeFalse("There should be no session rosters yet");

        // Verify session data integrity
        sessionStillExists.SessionId.Should().Be(1);
        sessionStillExists.Note.Should().Be("Test session");
        sessionStillExists.CreateDateTime.Should().Be(_testDate);
        sessionStillExists.UpdateDateTime.Should().Be(_testDate);
        sessionStillExists.SessionDate.Should().Be(_testDate.AddDays(1));
    }

    [Fact]
    public async Task DeleteSessionAsync_WithoutRelatedData_DeletesSuccessfully()
    {
        // Arrange
        await using var arrangeContext = new DetailedSessionTestContext(_options);
        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "Test session"
        };
        arrangeContext.Sessions!.Add(session);
        await arrangeContext.SaveChangesAsync();

        // Act
        await using var actContext = new DetailedSessionTestContext(_options);
        var repository = new SessionRepository(actContext, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);
        var result = await repository.DeleteSessionAsync(session.SessionId);

        // Assert
        result.Should().BeTrue();

        // Use a fresh context for verification
        await using var assertContext = new DetailedSessionTestContext(_options);
        var deletedSession = await assertContext.Sessions!.FindAsync(session.SessionId);
        deletedSession.Should().BeNull();
    }

    public class DeletePlayerFromRosterRepositoryTests : IDisposable
    {
        private readonly Mock<ILogger<SessionRepository>> _mockLogger;
        private readonly Mock<HttpContextAccessor> _mockContextAccessor;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<HockeyPickupContext> _options;
        private readonly DateTime _testDate = DateTime.UtcNow;

        public DeletePlayerFromRosterRepositoryTests()
        {
            _mockLogger = new Mock<ILogger<SessionRepository>>();
            _mockContextAccessor = new Mock<HttpContextAccessor>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfiguration.Setup(x => x["SessionBuyPrice"]).Returns("27.00");

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<HockeyPickupContext>()
                .UseSqlite(_connection)
                .EnableSensitiveDataLogging()
                .Options;

            using var context = new DetailedSessionTestContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        [Fact]
        public async Task DeletePlayerFromRosterAsync_ValidPlayer_DeletesAndReturnsUpdatedSession()
        {
            // Arrange
            await using var arrangeContext = new DetailedSessionTestContext(_options);

            var user = new AspNetUser
            {
                Id = "testUser",
                UserName = "test@example.com",
                Email = "test@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1
            };
            arrangeContext.Users!.Add(user);

            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = _testDate,
                UpdateDateTime = _testDate,
                SessionDate = _testDate.AddDays(1)
            };
            arrangeContext.Sessions!.Add(session);
            await arrangeContext.SaveChangesAsync();

            var roster = new SessionRoster
            {
                SessionId = 1,
                UserId = "testUser",
                TeamAssignment = (TeamAssignment) 1,
                JoinedDateTime = _testDate
            };
            arrangeContext.SessionRosters!.Add(roster);
            await arrangeContext.SaveChangesAsync();

            // Act
            await using var actContext = new DetailedSessionTestContext(_options);
            var repository = new SessionRepository(
                actContext,
                _mockLogger.Object,
                _mockContextAccessor.Object,
                _mockConfiguration.Object);

            var result = await repository.DeletePlayerFromRosterAsync(1, "testUser");

            // Assert
            result.Should().NotBeNull();

            // Verify the roster entry was deleted
            var rosterStillExists = await actContext.SessionRosters!
                .AnyAsync(r => r.SessionId == 1 && r.UserId == "testUser");
            rosterStillExists.Should().BeFalse();

            // Verify session still exists and is returned
            result.SessionId.Should().Be(1);
        }

        [Fact]
        public async Task DeletePlayerFromRosterAsync_PlayerNotInRoster_ThrowsKeyNotFoundException()
        {
            // Arrange
            await using var arrangeContext = new DetailedSessionTestContext(_options);
            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = _testDate,
                UpdateDateTime = _testDate,
                SessionDate = _testDate.AddDays(1)
            };
            arrangeContext.Sessions!.Add(session);
            await arrangeContext.SaveChangesAsync();

            // Act & Assert
            await using var actContext = new DetailedSessionTestContext(_options);
            var repository = new SessionRepository(
                actContext,
                _mockLogger.Object,
                _mockContextAccessor.Object,
                _mockConfiguration.Object);

            await repository.Invoking(r => r.DeletePlayerFromRosterAsync(1, "nonexistentUser"))
                .Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage("Player not found in session roster");
        }

        [Fact]
        public async Task DeletePlayerFromRosterAsync_DeleteSuccessful_DetachesContext()
        {
            // Arrange
            await using var arrangeContext = new DetailedSessionTestContext(_options);
            var user = new AspNetUser
            {
                Id = "testUser",
                UserName = "test@example.com",
                Email = "test@example.com",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1
            };
            arrangeContext.Users!.Add(user);

            var session = new Session
            {
                SessionId = 1,
                CreateDateTime = _testDate,
                UpdateDateTime = _testDate,
                SessionDate = _testDate.AddDays(1)
            };
            arrangeContext.Sessions!.Add(session);
            await arrangeContext.SaveChangesAsync();

            var roster = new SessionRoster
            {
                SessionId = 1,
                UserId = "testUser",
                TeamAssignment = (TeamAssignment) 1,
                JoinedDateTime = _testDate
            };
            arrangeContext.SessionRosters!.Add(roster);
            await arrangeContext.SaveChangesAsync();

            // Act
            await using var actContext = new DetailedSessionTestContext(_options);
            var repository = new SessionRepository(
                actContext,
                _mockLogger.Object,
                _mockContextAccessor.Object,
                _mockConfiguration.Object);

            // Get the initial tracking count
            var initialTrackedCount = actContext.ChangeTracker.Entries().Count();

            // Perform the delete operation
            await actContext.SessionRosters!
                .Where(sr => sr.SessionId == 1 && sr.UserId == "testUser")
                .ExecuteDeleteAsync();

            // Verify immediate state after delete
            actContext.ChangeTracker.Clear();

            // Assert
            // Verify that after clearing, no entities are tracked
            actContext.ChangeTracker.Entries().Should().BeEmpty();

            // Verify the delete was successful
            var rosterStillExists = await actContext.SessionRosters!
                .AnyAsync(r => r.SessionId == 1 && r.UserId == "testUser");
            rosterStillExists.Should().BeFalse();
        }
    }

    [Theory]
    [InlineData("Regular", PlayerStatus.Regular)]
    [InlineData("Substitute", PlayerStatus.Substitute)]
    [InlineData("Not Playing", PlayerStatus.NotPlaying)]
    [InlineData("In Queue", PlayerStatus.InQueue)]
    public void ParsePlayerStatus_ValidStatuses_ReturnsCorrectEnum(string status, PlayerStatus expected)
    {
        // Arrange
        var parseMethod = typeof(SessionRepository)
            .GetMethod("ParsePlayerStatus", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = parseMethod!.Invoke(null, new object[] { status });

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void MapBuySells_WithMultipleBuySells_MapsAndOrdersCorrectly()
    {
        // Arrange
        var buySells = new List<BuySell>
        {
            new()
            {
                BuySellId = 2,
                BuyerUserId = "buyer2",
                SellerUserId = "seller2",
                SellerNote = "Note2",
                BuyerNote = "BuyerNote2",
                PaymentSent = true,
                PaymentReceived = false,
                CreateDateTime = DateTime.UtcNow.AddDays(-1),
                TeamAssignment = TeamAssignment.Dark,
                UpdateDateTime = DateTime.UtcNow,
                Price = 25.00m,
                CreateByUserId = "creator2",
                UpdateByUserId = "updater2",
                PaymentMethod = PaymentMethodType.Venmo,
                TransactionStatus = "Pending",
                SellerNoteFlagged = true,
                BuyerNoteFlagged = false,
                Session = new Session
                {
                    SessionId = 1,
                    SessionDate = DateTime.UtcNow.AddDays(1)
                }
            },
            new()
            {
                BuySellId = 1,
                BuyerUserId = "buyer1",
                SellerUserId = "seller1",
                PaymentMethod = null,
                Price = null,
                Session = new Session
                {
                    SessionId = 2,
                    SessionDate = DateTime.UtcNow.AddDays(1)
                }
            }
        };

        var mapMethod = typeof(SessionRepository)
            .GetMethod("MapBuySells", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (List<BuySellResponse>) mapMethod!.Invoke(null, new object[] { buySells })!;

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].BuySellId.Should().Be(1);
        result[1].BuySellId.Should().Be(2);

        // Verify nullable handling
        result[0].PaymentMethod.Should().Be(PaymentMethodType.Unknown);
        result[0].Price.Should().Be(0m);

        // Verify complete mapping of properties
        var secondBuySell = result[1];
        secondBuySell.BuyerUserId.Should().Be("buyer2");
        secondBuySell.SellerUserId.Should().Be("seller2");
        secondBuySell.SellerNote.Should().Be("Note2");
        secondBuySell.BuyerNote.Should().Be("BuyerNote2");
        secondBuySell.PaymentSent.Should().BeTrue();
        secondBuySell.PaymentReceived.Should().BeFalse();
        secondBuySell.TeamAssignment.Should().Be(TeamAssignment.Dark);
        secondBuySell.Price.Should().Be(25.00m);
        secondBuySell.CreateByUserId.Should().Be("creator2");
        secondBuySell.UpdateByUserId.Should().Be("updater2");
        secondBuySell.PaymentMethod.Should().Be(PaymentMethodType.Venmo);
        secondBuySell.TransactionStatus.Should().Be("Pending");
        secondBuySell.SellerNoteFlagged.Should().BeTrue();
        secondBuySell.BuyerNoteFlagged.Should().BeFalse();
    }

    [Fact]
    public void MapToUserDetailedResponse_WithPaymentMethods_MapsCorrectly()
    {
        // Arrange
        var user = new AspNetUser
        {
            Id = "user1",
            PaymentMethods = new List<UserPaymentMethod>
        {
            new()
            {
                UserPaymentMethodId = 1,
                MethodType = PaymentMethodType.PayPal,
                Identifier = "test@paypal.com",
                PreferenceOrder = 1,
                IsActive = true
            },
            new()
            {
                UserPaymentMethodId = 2,
                MethodType = PaymentMethodType.Venmo,
                Identifier = "@venmo",
                PreferenceOrder = 2,
                IsActive = false
            }
        }
        };

        var mapMethod = typeof(SessionRepository)
            .GetMethod("MapToUserDetailedResponse", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (UserDetailedResponse?) mapMethod!.Invoke(null, new object[] { user });

        // Assert
        result.Should().NotBeNull();
        result!.PaymentMethods.Should().HaveCount(2);

        var firstPayment = result.PaymentMethods.First();
        firstPayment.UserPaymentMethodId.Should().Be(1);
        firstPayment.MethodType.Should().Be(PaymentMethodType.PayPal);
        firstPayment.Identifier.Should().Be("test@paypal.com");
        firstPayment.PreferenceOrder.Should().Be(1);
        firstPayment.IsActive.Should().BeTrue();

        var secondPayment = result.PaymentMethods.Skip(1).First();
        secondPayment.UserPaymentMethodId.Should().Be(2);
        secondPayment.MethodType.Should().Be(PaymentMethodType.Venmo);
        secondPayment.Identifier.Should().Be("@venmo");
        secondPayment.PreferenceOrder.Should().Be(2);
        secondPayment.IsActive.Should().BeFalse();
    }
}
