using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Models.Responses;
using System.Reflection;

namespace HockeyPickup.Api.Tests.DataRepositoryTests;

public class RegularRepositoryTests : IDisposable
{
    private readonly DbContextOptions<HockeyPickupContext> _options;
    private readonly HockeyPickupContext _context;
    private readonly RegularRepository _repository;
    private readonly DateTime _testDate = DateTime.UtcNow;

    public RegularRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DetailedSessionTestContext(_options);
        _repository = new RegularRepository(_context);
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
                PayPalEmail = "test1@paypal.com",
                NotificationPreference = 1,
                Rating = 4.5m,
                Preferred = true,
                PreferredPlus = false,
                Active = true,
                EmergencyName = "Jane Doe",
                EmergencyPhone = "123-456-7890",
                MobileLast4 = "1234",
                VenmoAccount = "@johndoe",
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
                PayPalEmail = "test2@paypal.com",
                NotificationPreference = 2,
                Rating = 3.5m,
                Preferred = false,
                PreferredPlus = true,
                Active = true,
                EmergencyName = "John Smith",
                EmergencyPhone = "098-765-4321",
                MobileLast4 = "5678",
                VenmoAccount = "@janesmith",
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
                TeamAssignment = 1,
                PositionPreference = 2
            },
            new()
            {
                RegularSetId = 1,
                UserId = "user2",
                TeamAssignment = 2,
                PositionPreference = 1
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
        firstRegular.User.MobileLast4.Should().Be("1234");
        firstRegular.User.VenmoAccount.Should().Be("@johndoe");
        firstRegular.User.PayPalEmail.Should().Be("test1@paypal.com");
        firstRegular.User.NotificationPreference.Should().Be(NotificationPreference.All);
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
                    TeamAssignment = 1,
                    PositionPreference = 1
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
}
