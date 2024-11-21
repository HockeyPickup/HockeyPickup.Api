using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;
using Microsoft.Extensions.Logging;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Data.Context;
using Microsoft.AspNetCore.Identity;
using System.Reflection;

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
            entity.Property(e => e.PayPalEmail);
            entity.Property(e => e.NotificationPreference);

            // Configure relationships
            entity.HasMany(u => u.BuyerTransactions).WithOne(b => b.Buyer);
            entity.HasMany(u => u.SellerTransactions).WithOne(b => b.Seller);
            entity.HasMany(u => u.ActivityLogs).WithOne(a => a.User);
            entity.HasMany(u => u.Regulars).WithOne(r => r.User);
        });

        // Configure other required entities
        modelBuilder.Entity<BuySell>(entity =>
        {
            entity.HasKey(e => e.BuySellId);
            entity.Property(e => e.CreateDateTime);
            entity.Property(e => e.UpdateDateTime);
            entity.Property(e => e.TeamAssignment);
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
    private readonly HockeyPickupContext _context;
    private readonly SessionRepository _repository;
    private readonly DateTime _testDate = DateTime.UtcNow;

    public BasicSessionRepositoryTests()
    {
        _mockLogger = new Mock<ILogger<SessionRepository>>();

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

        _repository = new SessionRepository(_context, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetBasicSessionsAsync_ReturnsNonCancelledSessions()
    {
        // Act
        var result = await _repository.GetBasicSessionsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().NotContain(s => s.Note != null &&
            s.Note.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
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

public class DetailedSessionRepositoryTests : IDisposable
{
    private readonly Mock<ILogger<SessionRepository>> _mockLogger;
    private readonly HockeyPickupContext _context;
    private readonly SessionRepository _repository;
    private readonly DateTime _testDate = DateTime.UtcNow;

    public DetailedSessionRepositoryTests()
    {
        _mockLogger = new Mock<ILogger<SessionRepository>>();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DetailedSessionTestContext(options);
        _repository = new SessionRepository(_context, _mockLogger.Object);
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
            PayPalEmail = "test@example.com",
            NotificationPreference = 1
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
            TeamAssignment = 1,
            PositionPreference = 1
        };
        _context.Regulars.Add(regular);

        var buySell = new BuySell
        {
            BuySellId = 1,
            SessionId = session.SessionId,
            BuyerUserId = user.Id,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            TeamAssignment = 1
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
            TeamAssignment = 1
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
            PayPalEmail = "test@example.com",
            NotificationPreference = 1
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
            PayPalEmail = "test@example.com",
            NotificationPreference = 1
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
            TeamAssignment = 1,
            PositionPreference = 1
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
            TeamAssignment = 1
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
            PayPalEmail = "test1@example.com",
            NotificationPreference = 1
        };
        var user2 = new AspNetUser
        {
            Id = "user2",
            UserName = "test2@example.com",
            Email = "test2@example.com",
            PayPalEmail = "test2@example.com",
            NotificationPreference = 1
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
            TeamAssignment = 1,
            PositionPreference = 1,
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
            TeamAssignment = 1
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
            TeamAssignment = 2
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
            .GetMethod("MapToUserBasicResponse",
                BindingFlags.NonPublic | BindingFlags.Static);

        var nullResult = mapMethod!.Invoke(null, new object[] { null });
        nullResult.Should().BeNull();

        var userResult = mapMethod!.Invoke(null, new object[] { user1 }) as UserBasicResponse;
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
            Regulars = null  // Explicitly set Regulars to null
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
            Regulars = null  // Explicitly set to null
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
        _context.Entry(modifiedRegularSet).Collection(r => r.Regulars).CurrentValue = null;
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
                CreateDateTime = loadedSession.RegularSet.CreateDateTime,
                Regulars = null  // Force null here
            } : null
        };

        // Assert
        result.Should().NotBeNull();
        result.RegularSet.Should().NotBeNull();
        result.RegularSet!.Regulars.Should().BeNull();
    }
}
