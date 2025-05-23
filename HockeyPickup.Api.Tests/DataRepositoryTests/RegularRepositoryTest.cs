using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Models.Responses;
using System.Reflection;
using Moq;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.Extensions.Logging;

namespace HockeyPickup.Api.Tests.DataRepositoryTests;

public class RegularRepositoryTests : IDisposable
{
    private readonly DbContextOptions<HockeyPickupContext> _options;
    private readonly HockeyPickupContext _context;
    private readonly RegularRepository _repository;
    private readonly DateTime _testDate = DateTime.UtcNow;
    private readonly Mock<ILogger<RegularRepository>> _mockLogger;

    public RegularRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DetailedSessionTestContext(_options);
        _mockLogger = new Mock<ILogger<RegularRepository>>();
        _repository = new RegularRepository(_context, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task SeedTestData()
    {
        var users = new List<AspNetUser>
        {
            new()
            {
                Id = "user1",
                UserName = "test1@example.com",
                Email = "test1@example.com",
                FirstName = "John",
                LastName = "Doe",
                NotificationPreference = (NotificationPreference) 1,
                PositionPreference = (PositionPreference) 1,
                Shoots = (ShootPreference) 1,
                Rating = 4.5m,
                Preferred = true,
                PreferredPlus = false,
                Active = true,
                EmergencyName = "Jane Doe",
                EmergencyPhone = "123-456-7890",
                LockerRoom13 = true,
                DateCreated = _testDate
            },
            new()
            {
                Id = "user2",
                UserName = "test2@example.com",
                Email = "test2@example.com",
                FirstName = "Jane",
                LastName = "Smith",
                NotificationPreference = (NotificationPreference) 2,
                PositionPreference = (PositionPreference) 2,
                Shoots = (ShootPreference) 2,
                Rating = 3.5m,
                Preferred = false,
                PreferredPlus = true,
                Active = true,
                EmergencyName = "John Smith",
                EmergencyPhone = "098-765-4321",
                LockerRoom13 = false,
                DateCreated = _testDate
            }
        };
        await _context.Users.AddRangeAsync(users);

        var regularSets = new List<RegularSet>
        {
            new()
            {
                RegularSetId = 1,
                Description = "Monday Night",
                DayOfWeek = 1,
                CreateDateTime = _testDate
            },
            new()
            {
                RegularSetId = 2,
                Description = "Wednesday Night",
                DayOfWeek = 3,
                CreateDateTime = _testDate
            }
        };
        await _context.RegularSets.AddRangeAsync(regularSets);

        var regulars = new List<Regular>
        {
            new()
            {
                RegularSetId = 1,
                UserId = "user1",
                TeamAssignment = (TeamAssignment) 1,
                PositionPreference = (PositionPreference) 2
            },
            new()
            {
                RegularSetId = 1,
                UserId = "user2",
                TeamAssignment = (TeamAssignment) 2,
                PositionPreference = (PositionPreference) 1
            }
        };
        await _context.Regulars.AddRangeAsync(regulars);

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetRegularSetsAsync_ReturnsAllSets()
    {
        // Arrange
        await SeedTestData();

        // Act
        var result = await _repository.GetRegularSetsAsync();

        // Assert
        var regularSets = result.ToList();
        regularSets.Should().NotBeNull();
        regularSets.Should().HaveCount(2);
        regularSets[0].Description.Should().Be("Monday Night");
        regularSets[0].Regulars.Should().HaveCount(2);
        regularSets[1].Description.Should().Be("Wednesday Night");
        regularSets[1].Regulars.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRegularSetsAsync_MapsUserDetailsCorrectly()
    {
        // Arrange
        await SeedTestData();

        // Act
        var result = await _repository.GetRegularSetsAsync();

        // Assert
        var firstSet = result.First();
        var firstRegular = firstSet.Regulars.First();
        firstRegular.User.Should().NotBeNull();
        firstRegular.User!.FirstName.Should().Be("John");
        firstRegular.User.LastName.Should().Be("Doe");
        firstRegular.User.Rating.Should().Be(4.5m);
        firstRegular.User.Preferred.Should().BeTrue();
        firstRegular.User.PreferredPlus.Should().BeFalse();
        firstRegular.User.Active.Should().BeTrue();
        firstRegular.User.EmergencyName.Should().Be("Jane Doe");
        firstRegular.User.EmergencyPhone.Should().Be("123-456-7890");
        firstRegular.User.NotificationPreference.Should().Be(NotificationPreference.All);
        firstRegular.User.PositionPreference.Should().Be(PositionPreference.Forward);
        firstRegular.User.Shoots.Should().Be(ShootPreference.Left);
        firstRegular.User.LockerRoom13.Should().BeTrue();
        firstRegular.User.DateCreated.Should().Be(_testDate);
        firstRegular.User.Roles.Should().NotBeNull();
        firstRegular.User.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRegularSetAsync_ValidId_ReturnsCorrectSet()
    {
        // Arrange
        await SeedTestData();

        // Act
        var result = await _repository.GetRegularSetAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.RegularSetId.Should().Be(1);
        result.Description.Should().Be("Monday Night");
        result.DayOfWeek.Should().Be(1);
        result.Regulars.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRegularSetAsync_InvalidId_ReturnsNull()
    {
        // Arrange
        await SeedTestData();

        // Act
        var result = await _repository.GetRegularSetAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRegularSetsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetRegularSetsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRegularSetsAsync_OrdersByCreateDateTimeDescending()
    {
        // Arrange
        var sets = new List<RegularSet>
    {
        new() {
            RegularSetId = 1,
            Description = "Oldest",
            DayOfWeek = 5,
            CreateDateTime = _testDate.AddDays(-2)
        },
        new() {
            RegularSetId = 2,
            Description = "Newest",
            DayOfWeek = 1,
            CreateDateTime = _testDate
        },
        new() {
            RegularSetId = 3,
            Description = "Middle",
            DayOfWeek = 3,
            CreateDateTime = _testDate.AddDays(-1)
        }
    };
        await _context.RegularSets.AddRangeAsync(sets);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _repository.GetRegularSetsAsync()).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Description.Should().Be("Newest");
        result[1].Description.Should().Be("Middle");
        result[2].Description.Should().Be("Oldest");
        result.Select(x => x.CreateDateTime).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetRegularSetsAsync_HandlesNullUsers()
    {
        // Arrange
        var set = new RegularSet
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 1,
            CreateDateTime = _testDate,
            Regulars = new List<Regular>
            {
                new Regular
                {
                    RegularSetId = 1,
                    UserId = "nonexistent",
                    TeamAssignment = (TeamAssignment) 1,
                    PositionPreference = (PositionPreference) 1
                }
            }
        };

        await _context.RegularSets.AddAsync(set);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRegularSetsAsync();

        // Assert
        result.Should().ContainSingle()
            .Which.Regulars.Should().ContainSingle()
            .Which.User.Should().BeNull();
    }

    [Fact]
    public void MapToDetailedResponse_WithNullRegularSet_ReturnsNull()
    {
        // Arrange
        var mapMethod = typeof(RegularRepository)
            .GetMethod("MapToDetailedResponse",
                BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = mapMethod!.Invoke(null, new object[] { null! });

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void MapRegulars_WithNullCollection_ReturnsEmptyList()
    {
        // Arrange
        var mapMethod = typeof(RegularRepository)
            .GetMethod("MapRegulars",
                BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = mapMethod!.Invoke(null, new object[] { null! });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<RegularDetailedResponse>>();
        (result as List<RegularDetailedResponse>).Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateRegularSetAsync_ValidRequest_ReturnsDuplicatedSet()
    {
        // Arrange
        var sourceId = 1;
        var newDescription = "Duplicated Set";
        var mockDb = new Mock<IDbFacade>();

        var expectedNewId = 2;
        mockDb.Setup(x => x.ExecuteSqlRawAsync(It.IsAny<string>(), It.IsAny<IEnumerable<SqlParameter>>()))
            .Callback<string, IEnumerable<SqlParameter>>((sql, parameters) =>
            {
                parameters.Last().Value = expectedNewId;
            })
            .ReturnsAsync(1);

        var repository = new RegularRepository(_context, _mockLogger.Object, mockDb.Object);

        // Act
        var result = await repository.DuplicateRegularSetAsync(sourceId, newDescription);

        // Assert
        mockDb.Verify(x => x.ExecuteSqlRawAsync(
            It.Is<string>(sql => sql.Contains("CopyRoster")),
            It.Is<IEnumerable<SqlParameter>>(p => p.Any(param => param.Value.ToString() == sourceId.ToString()))),
            Times.Once);
    }

    [Fact]
    public async Task DuplicateRegularSetAsync_InvalidSourceId_ReturnsNull()
    {
        // Arrange
        var mockDb = new Mock<IDbFacade>();
        mockDb.Setup(x => x.ExecuteSqlRawAsync(It.IsAny<string>(), It.IsAny<IEnumerable<SqlParameter>>()))
            .ThrowsAsync(new Exception("Source not found"));

        var repository = new RegularRepository(_context, _mockLogger.Object, mockDb.Object);

        // Act
        var result = await repository.DuplicateRegularSetAsync(999, "Test");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DuplicateRegularSetAsync_VerifiesParameters()
    {
        // Arrange
        var sourceId = 1;
        var newDescription = "Test Description";
        var mockDb = new Mock<IDbFacade>();
        mockDb.Setup(x => x.ExecuteSqlRawAsync(It.IsAny<string>(), It.IsAny<IEnumerable<SqlParameter>>()))
            .ReturnsAsync(1);

        var repository = new RegularRepository(_context, _mockLogger.Object, mockDb.Object);

        // Act
        await repository.DuplicateRegularSetAsync(sourceId, newDescription);

        // Assert
        mockDb.Verify(x => x.ExecuteSqlRawAsync(
            It.IsAny<string>(),
            It.Is<IEnumerable<SqlParameter>>(p =>
                p.Any(param => param.ParameterName == "@RegularSetId" && (int) param.Value == sourceId) &&
                p.Any(param => param.ParameterName == "@NewRosterDescription" && (string) param.Value == newDescription))),
            Times.Once);
    }

    [Fact]
    public async Task UpdateRegularSetAsync_ValidRequest_ReturnsUpdatedSet()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 1;
        var newDescription = "Updated Monday Night";
        var newDayOfWeek = 2;
        var newArchived = true;

        // Act
        var result = await _repository.UpdateRegularSetAsync(regularSetId, newDescription, newDayOfWeek, newArchived);

        // Assert
        result.Should().NotBeNull();
        result!.RegularSetId.Should().Be(regularSetId);
        result.Description.Should().Be(newDescription);
        result.DayOfWeek.Should().Be(newDayOfWeek);
        result.Archived.Should().BeTrue();
        result.Regulars.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateRegularSetAsync_InvalidId_ReturnsNull()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 999;
        var newDescription = "Invalid Set";
        var newDayOfWeek = 2;
        var newArchived = false;

        // Act
        var result = await _repository.UpdateRegularSetAsync(regularSetId, newDescription, newDayOfWeek, newArchived);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateRegularSetAsync_PreservesRegularsAfterUpdate()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 1;
        var newDescription = "Updated Set";
        var newDayOfWeek = 2;
        var newArchived = true;

        // Act
        var result = await _repository.UpdateRegularSetAsync(regularSetId, newDescription, newDayOfWeek, newArchived);

        // Assert
        result.Should().NotBeNull();
        result!.Regulars.Should().HaveCount(2);
        result.Regulars.Should().Contain(r => r.UserId == "user1");
        result.Regulars.Should().Contain(r => r.UserId == "user2");
    }

    [Fact]
    public async Task UpdateRegularSetAsync_DatabaseError_ReturnsNull()
    {
        // Arrange
        var mockContext = new Mock<HockeyPickupContext>(_options);
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Test database error"));

        var repository = new RegularRepository(mockContext.Object, _mockLogger.Object);

        // Act
        var result = await repository.UpdateRegularSetAsync(1, "Test", 1, false);

        // Assert
        result.Should().BeNull();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateRegularSetAsync_VerifiesPropertyUpdates()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 1;
        var newDescription = "New Description";
        var newDayOfWeek = 3;
        var newArchived = true;

        // Act
        await _repository.UpdateRegularSetAsync(regularSetId, newDescription, newDayOfWeek, newArchived);

        // Assert
        var updatedEntity = await _context.RegularSets
            .FirstOrDefaultAsync(rs => rs.RegularSetId == regularSetId);

        updatedEntity.Should().NotBeNull();
        updatedEntity!.Description.Should().Be(newDescription);
        updatedEntity.DayOfWeek.Should().Be(newDayOfWeek);
        updatedEntity.Archived.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePlayerPositionAsync_ValidRequest_UpdatesPosition()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 1;
        var userId = "user1";
        var newPosition = 1;

        // Act
        var result = await _repository.UpdatePlayerPositionAsync(regularSetId, userId, (PositionPreference) newPosition);

        // Assert
        result.Should().NotBeNull();
        result!.RegularSetId.Should().Be(regularSetId);
        result.Regulars.Should().Contain(r =>
            r.UserId == userId &&
            r.PositionPreference == (PositionPreference) newPosition);

        var updatedEntity = await _context.Regulars
            .FirstOrDefaultAsync(r => r.RegularSetId == regularSetId && r.UserId == userId);
        updatedEntity.Should().NotBeNull();
        updatedEntity!.PositionPreference.Should().Be((PositionPreference) newPosition);
    }

    [Fact]
    public async Task UpdatePlayerTeamAsync_ValidRequest_UpdatesTeam()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 1;
        var userId = "user1";
        var newTeam = 2;

        // Act
        var result = await _repository.UpdatePlayerTeamAsync(regularSetId, userId, (TeamAssignment) newTeam);

        // Assert
        result.Should().NotBeNull();
        result!.RegularSetId.Should().Be(regularSetId);
        result.Regulars.Should().Contain(r =>
            r.UserId == userId &&
            r.TeamAssignment == (TeamAssignment) newTeam);

        var updatedEntity = await _context.Regulars
            .FirstOrDefaultAsync(r => r.RegularSetId == regularSetId && r.UserId == userId);
        updatedEntity.Should().NotBeNull();
        updatedEntity!.TeamAssignment.Should().Be((TeamAssignment) newTeam);
    }

    [Fact]
    public async Task UpdatePlayerPositionAsync_DatabaseError_ReturnsNull()
    {
        // Arrange
        var mockContext = new Mock<HockeyPickupContext>(_options);
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Test database error"));

        var repository = new RegularRepository(mockContext.Object, _mockLogger.Object);

        // Act
        var result = await repository.UpdatePlayerPositionAsync(1, "user1", (PositionPreference) 1);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePlayerTeamAsync_DatabaseError_ReturnsNull()
    {
        // Arrange
        var mockContext = new Mock<HockeyPickupContext>(_options);
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Test database error"));

        var repository = new RegularRepository(mockContext.Object, _mockLogger.Object);

        // Act
        var result = await repository.UpdatePlayerTeamAsync(1, "user1", (TeamAssignment) 1);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePlayerPositionAsync_InvalidRegularSetId_ReturnsNull()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 999;
        var userId = "user1";
        var newPosition = 1;

        // Act
        var result = await _repository.UpdatePlayerPositionAsync(regularSetId, userId, (PositionPreference) newPosition);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePlayerPositionAsync_InvalidUserId_ReturnsNull()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 1;
        var userId = "invalid-user";
        var newPosition = 1;

        // Act
        var result = await _repository.UpdatePlayerPositionAsync(regularSetId, userId, (PositionPreference) newPosition);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePlayerTeamAsync_InvalidRegularSetId_ReturnsNull()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 999;
        var userId = "user1";
        var newTeam = 2;

        // Act
        var result = await _repository.UpdatePlayerTeamAsync(regularSetId, userId, (TeamAssignment) newTeam);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePlayerTeamAsync_InvalidUserId_ReturnsNull()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 1;
        var userId = "invalid-user";
        var newTeam = 2;

        // Act
        var result = await _repository.UpdatePlayerTeamAsync(regularSetId, userId, (TeamAssignment) newTeam);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRegularSetAsync_Success_DeletesSetAndRegulars()
    {
        await SeedTestData();
        var regularSetId = 1;

        var (success, message) = await _repository.DeleteRegularSetAsync(regularSetId);

        success.Should().BeTrue();
        message.Should().Be("Regular set deleted successfully");

        var regularSet = await _context.RegularSets.FindAsync(regularSetId);
        regularSet.Should().BeNull();

        var regulars = await _context.Regulars
            .Where(r => r.RegularSetId == regularSetId)
            .ToListAsync();
        regulars.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteRegularSetAsync_WithActiveSessions_ReturnsFalse()
    {
        await SeedTestData();
        var regularSetId = 1;
        var session = new Session
        {
            SessionId = 1,
            RegularSetId = regularSetId,
            SessionDate = DateTime.UtcNow,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };
        await _context.Sessions.AddAsync(session);
        await _context.SaveChangesAsync();

        var (success, message) = await _repository.DeleteRegularSetAsync(regularSetId);

        success.Should().BeFalse();
        message.Should().Be("Cannot delete regular set as it is being used by one or more sessions");

        var regularSet = await _context.RegularSets.FindAsync(regularSetId);
        regularSet.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteRegularSetAsync_InvalidId_ReturnsFalse()
    {
        await SeedTestData();
        var regularSetId = 999;

        var (success, message) = await _repository.DeleteRegularSetAsync(regularSetId);

        success.Should().BeFalse();
        message.Should().Be("Regular set not found");
    }

    [Fact]
    public async Task DeleteRegularSetAsync_DbError_ReturnsFalse()
    {
        var mockContext = new Mock<HockeyPickupContext>(_options);
        mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var repository = new RegularRepository(mockContext.Object, _mockLogger.Object);

        var (success, message) = await repository.DeleteRegularSetAsync(1);

        success.Should().BeFalse();
        message.Should().Contain("Error deleting regular set");
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task AddPlayerAsync_ValidRequest_AddsPlayer()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 1;
        var userId = "user3"; // New user
        var teamAssignment = 1;
        var positionPreference = 2;

        // Act
        var result = await _repository.AddPlayerAsync(regularSetId, userId, (TeamAssignment) teamAssignment, (PositionPreference) positionPreference);

        // Assert
        result.Should().NotBeNull();
        result!.RegularSetId.Should().Be(regularSetId);
        result.Regulars.Should().Contain(r =>
            r.UserId == userId &&
            r.TeamAssignment == (TeamAssignment) teamAssignment &&
            r.PositionPreference == (PositionPreference) positionPreference);

        var addedEntity = await _context.Regulars
            .FirstOrDefaultAsync(r => r.RegularSetId == regularSetId && r.UserId == userId);
        addedEntity.Should().NotBeNull();
    }

    [Fact]
    public async Task RemovePlayerAsync_ValidRequest_RemovesPlayer()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 1;
        var userId = "user1";

        // Act
        var result = await _repository.RemovePlayerAsync(regularSetId, userId);

        // Assert
        result.Should().NotBeNull();
        result!.RegularSetId.Should().Be(regularSetId);
        result.Regulars.Should().NotContain(r => r.UserId == userId);

        var removedEntity = await _context.Regulars
            .FirstOrDefaultAsync(r => r.RegularSetId == regularSetId && r.UserId == userId);
        removedEntity.Should().BeNull();
    }

    [Fact]
    public async Task RemovePlayerAsync_InvalidRequest_ReturnsNull()
    {
        // Arrange
        await SeedTestData();
        var regularSetId = 999;
        var userId = "nonexistent";

        // Act
        var result = await _repository.RemovePlayerAsync(regularSetId, userId);

        // Assert
        result.Should().BeNull();
    }

    // Repository tests
    [Fact]
    public async Task AddPlayerAsync_DatabaseError_LogsAndReturnsNull()
    {
        // Arrange
        var mockContext = new Mock<HockeyPickupContext>(_options);
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var repository = new RegularRepository(mockContext.Object, _mockLogger.Object);

        // Act
        var result = await repository.AddPlayerAsync(1, "user1", (TeamAssignment) 1, (PositionPreference) 1);

        // Assert
        result.Should().BeNull();
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error adding regular player")),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task RemovePlayerAsync_DatabaseError_LogsAndReturnsNull()
    {
        // Arrange
        var mockContext = new Mock<HockeyPickupContext>(_options);
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var repository = new RegularRepository(mockContext.Object, _mockLogger.Object);

        // Act
        var result = await repository.RemovePlayerAsync(1, "user1");

        // Assert
        result.Should().BeNull();
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error removing regular player")),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task CreateRegularSetAsync_ValidRequest_CreatesAndReturnsSet()
    {
        // Arrange
        var description = "New Regular Set";
        var dayOfWeek = 1;

        // Act
        var result = await _repository.CreateRegularSetAsync(description, dayOfWeek);

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be(description);
        result.DayOfWeek.Should().Be(dayOfWeek);
        result.Archived.Should().BeFalse();
        result.Regulars.Should().BeEmpty();

        var savedSet = await _context.RegularSets!.FirstOrDefaultAsync(rs => rs.Description == description);
        savedSet.Should().NotBeNull();
        savedSet!.Description.Should().Be(description);
        savedSet.DayOfWeek.Should().Be(dayOfWeek);
        savedSet.Archived.Should().BeFalse();
    }

    [Fact]
    public async Task CreateRegularSetAsync_DbError_LogsAndReturnsNull()
    {
        // Arrange
        var mockContext = new Mock<HockeyPickupContext>(_options);
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var repository = new RegularRepository(mockContext.Object, _mockLogger.Object);

        // Act
        var result = await repository.CreateRegularSetAsync("Test Set", 1);

        // Assert
        result.Should().BeNull();
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error creating regular set")),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task CreateRegularSetAsync_SetsCreateDateTime()
    {
        // Arrange
        var description = "Test Set";
        var dayOfWeek = 1;
        var beforeCreate = DateTime.UtcNow;

        // Act
        var result = await _repository.CreateRegularSetAsync(description, dayOfWeek);

        // Assert
        result.Should().NotBeNull();
        result!.CreateDateTime.Should().BeAfter(beforeCreate);
        result.CreateDateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateRegularSetAsync_CreatesSingleSet()
    {
        // Arrange
        var description = "Test Set";
        var dayOfWeek = 1;

        // Act
        await _repository.CreateRegularSetAsync(description, dayOfWeek);

        // Assert
        var sets = await _context.RegularSets!.ToListAsync();
        sets.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateRegularSetAsync_ReturnsMappedResponse()
    {
        // Arrange
        var description = "Test Set";
        var dayOfWeek = 1;

        // Act
        var result = await _repository.CreateRegularSetAsync(description, dayOfWeek);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<RegularSetDetailedResponse>();
        result!.Description.Should().Be(description);
        result.DayOfWeek.Should().Be(dayOfWeek);
        result.Regulars.Should().NotBeNull();
        result.Regulars.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRegularSetAsync_GeneratesNewId()
    {
        // Arrange
        var firstDescription = "First Set";
        var secondDescription = "Second Set";
        var dayOfWeek = 1;

        // Act
        var firstResult = await _repository.CreateRegularSetAsync(firstDescription, dayOfWeek);
        var secondResult = await _repository.CreateRegularSetAsync(secondDescription, dayOfWeek);

        // Assert
        firstResult.Should().NotBeNull();
        secondResult.Should().NotBeNull();
        secondResult!.RegularSetId.Should().BeGreaterThan(firstResult!.RegularSetId);
    }

    [Fact]
    public async Task GetRegularSetsAsync_MapsPaymentMethodsCorrectly()
    {
        // Arrange
        var user = new AspNetUser
        {
            Id = "user1",
            UserName = "test@example.com",
            Email = "test@example.com",
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1,
            Shoots = (ShootPreference) 1,
            PaymentMethods = new List<UserPaymentMethod>
        {
            new()
            {
                UserPaymentMethodId = 1,
                MethodType = PaymentMethodType.PayPal,
                Identifier = "user@paypal.com",
                PreferenceOrder = 1,
                IsActive = true
            },
            new()
            {
                UserPaymentMethodId = 2,
                MethodType = PaymentMethodType.Venmo,
                Identifier = "@venmo-user",
                PreferenceOrder = 2,
                IsActive = true
            }
        }
        };

        var regularSet = new RegularSet
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 1,
            CreateDateTime = _testDate,
            Regulars = new List<Regular>
        {
            new()
            {
                RegularSetId = 1,
                UserId = user.Id,
                TeamAssignment = (TeamAssignment)1,
                PositionPreference = (PositionPreference)1,
                User = user
            }
        }
        };

        await _context.Users.AddAsync(user);
        await _context.RegularSets.AddAsync(regularSet);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRegularSetsAsync();

        // Assert
        var firstSet = result.First();
        var firstRegular = firstSet.Regulars.First();
        firstRegular.User.Should().NotBeNull();
        firstRegular.User!.PaymentMethods.Should().NotBeNull();
        firstRegular.User.PaymentMethods.Should().HaveCount(2);

        var firstPaymentMethod = firstRegular.User.PaymentMethods.First();
        firstPaymentMethod.UserPaymentMethodId.Should().Be(1);
        firstPaymentMethod.MethodType.Should().Be(PaymentMethodType.PayPal);
        firstPaymentMethod.Identifier.Should().Be("user@paypal.com");
        firstPaymentMethod.PreferenceOrder.Should().Be(1);
        firstPaymentMethod.IsActive.Should().BeTrue();

        var secondPaymentMethod = firstRegular.User.PaymentMethods.Skip(1).First();
        secondPaymentMethod.UserPaymentMethodId.Should().Be(2);
        secondPaymentMethod.MethodType.Should().Be(PaymentMethodType.Venmo);
        secondPaymentMethod.Identifier.Should().Be("@venmo-user");
        secondPaymentMethod.PreferenceOrder.Should().Be(2);
        secondPaymentMethod.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetRegularSetsAsync_EmptyPaymentMethods_MapsToEmptyList()
    {
        // Arrange
        var user = new AspNetUser
        {
            Id = "user1",
            UserName = "test@example.com",
            Email = "test@example.com",
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1,
            Shoots = (ShootPreference) 1,
            PaymentMethods = new List<UserPaymentMethod>() // Empty list
        };

        var regularSet = new RegularSet
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 1,
            CreateDateTime = _testDate,
            Regulars = new List<Regular>
        {
            new()
            {
                RegularSetId = 1,
                UserId = user.Id,
                TeamAssignment = (TeamAssignment)1,
                PositionPreference = (PositionPreference)1,
                User = user
            }
        }
        };

        await _context.Users.AddAsync(user);
        await _context.RegularSets.AddAsync(regularSet);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRegularSetsAsync();

        // Assert
        var firstSet = result.First();
        var firstRegular = firstSet.Regulars.First();
        firstRegular.User.Should().NotBeNull();
        firstRegular.User!.PaymentMethods.Should().NotBeNull();
        firstRegular.User.PaymentMethods.Should().BeEmpty();
    }
}
