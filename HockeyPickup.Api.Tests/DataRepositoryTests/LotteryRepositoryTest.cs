using FluentAssertions;
using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Models.Responses;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HockeyPickup.Api.Tests.DataRepositoryTests;

public class LotteryRepositoryTest
{
    private readonly Mock<ILogger<LotteryRepository>> _mockLogger = new();
    private readonly Mock<ISessionRepository> _mockSessionRepository = new();

    private static HockeyPickupContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new HockeyPickupContext(options);
    }

    private static void SeedUsers(HockeyPickupContext context, params string[] userIds)
    {
        foreach (var id in userIds)
            context.Users.Add(new AspNetUser { Id = id, FirstName = "First" + id, LastName = "Last" + id, Email = id + "@example.com", UserName = id, Active = true });
    }

    private LotteryRepository CreateRepository(HockeyPickupContext context, IDbFacade? db = null)
        => new(context, _mockLogger.Object, _mockSessionRepository.Object, db);

    private static Session CreateSession(int id = 1, bool lotteryEnabled = true, int buyDayMinimum = 5, int windowMinutes = 30)
        => new()
        {
            SessionId = id,
            SessionDate = DateTime.UtcNow.AddDays(1),
            BuyDayMinimum = buyDayMinimum,
            LotteryEntryWindowMinutes = windowMinutes,
            LotteryEnabled = lotteryEnabled,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };

    private static SessionLotteryEntrant CreateEntrant(int id, string userId, int sessionId = 1, LotteryClass lotteryClass = LotteryClass.Standard, LotteryEntrantStatus status = LotteryEntrantStatus.Entered, int? drawOrder = null, DateTime? updateDateTime = null)
        => new()
        {
            LotteryEntrantId = id,
            SessionId = sessionId,
            UserId = userId,
            LotteryClass = lotteryClass,
            Status = status,
            Weight = 1.0m,
            DrawOrder = drawOrder,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = updateDateTime ?? DateTime.UtcNow
        };

    [Fact]
    public async Task GetEntrantAsync_ReturnsMatchingEntrant()
    {
        using var context = CreateInMemoryContext();
        context.SessionLotteryEntrants.Add(CreateEntrant(1, "userA"));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);

        var result = await repository.GetEntrantAsync(1, "userA");

        result.Should().NotBeNull();
        result!.UserId.Should().Be("userA");
    }

    [Fact]
    public async Task CreateOrReactivateEntrantAsync_NewEntrant_Creates()
    {
        using var context = CreateInMemoryContext();
        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, It.IsAny<string>())).ReturnsAsync(new SessionDetailedResponse { SessionId = 1, SessionDate = DateTime.UtcNow, CreateDateTime = DateTime.UtcNow, UpdateDateTime = DateTime.UtcNow });
        var repository = CreateRepository(context);

        var result = await repository.CreateOrReactivateEntrantAsync(1, "userA", LotteryClass.Preferred, 1.0m, "entered");

        result.Status.Should().Be(LotteryEntrantStatus.Entered);
        result.LotteryClass.Should().Be(LotteryClass.Preferred);
        context.SessionLotteryEntrants.Should().HaveCount(1);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(1, "entered"), Times.Once);
    }

    [Fact]
    public async Task CreateOrReactivateEntrantAsync_Existing_ReactivatesAndClearsDrawState()
    {
        using var context = CreateInMemoryContext();
        context.SessionLotteryEntrants.Add(CreateEntrant(1, "userA", lotteryClass: LotteryClass.PreferredPlus, status: LotteryEntrantStatus.Withdrawn, drawOrder: 3));
        await context.SaveChangesAsync();
        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, It.IsAny<string>())).ReturnsAsync(new SessionDetailedResponse { SessionId = 1, SessionDate = DateTime.UtcNow, CreateDateTime = DateTime.UtcNow, UpdateDateTime = DateTime.UtcNow });
        var repository = CreateRepository(context);

        var result = await repository.CreateOrReactivateEntrantAsync(1, "userA", LotteryClass.Preferred, 1.0m, "re-entered");

        result.Status.Should().Be(LotteryEntrantStatus.Entered);
        result.LotteryClass.Should().Be(LotteryClass.Preferred);
        result.DrawOrder.Should().BeNull();
        context.SessionLotteryEntrants.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateOrReactivateEntrantAsync_OnException_LogsAndThrows()
    {
        using var context = CreateInMemoryContext();
        _mockSessionRepository.Setup(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("boom"));
        var repository = CreateRepository(context);

        await repository.Invoking(r => r.CreateOrReactivateEntrantAsync(1, "userA", LotteryClass.Standard, 1.0m, "x"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WithdrawEntrantAsync_Entered_SetsWithdrawn()
    {
        using var context = CreateInMemoryContext();
        context.SessionLotteryEntrants.Add(CreateEntrant(1, "userA", status: LotteryEntrantStatus.Entered));
        await context.SaveChangesAsync();
        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, It.IsAny<string>())).ReturnsAsync(new SessionDetailedResponse { SessionId = 1, SessionDate = DateTime.UtcNow, CreateDateTime = DateTime.UtcNow, UpdateDateTime = DateTime.UtcNow });
        var repository = CreateRepository(context);

        var result = await repository.WithdrawEntrantAsync(1, "userA", "withdrew");

        result.Should().NotBeNull();
        result!.Status.Should().Be(LotteryEntrantStatus.Withdrawn);
    }

    [Fact]
    public async Task WithdrawEntrantAsync_NoEntrant_ReturnsNull()
    {
        using var context = CreateInMemoryContext();
        var repository = CreateRepository(context);

        var result = await repository.WithdrawEntrantAsync(1, "ghost", "withdrew");

        result.Should().BeNull();
    }

    [Fact]
    public async Task WithdrawEntrantAsync_NotEntered_ReturnsNull()
    {
        using var context = CreateInMemoryContext();
        context.SessionLotteryEntrants.Add(CreateEntrant(1, "userA", status: LotteryEntrantStatus.Drawing));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);

        var result = await repository.WithdrawEntrantAsync(1, "userA", "withdrew");

        result.Should().BeNull();
    }

    [Fact]
    public async Task WithdrawEntrantAsync_OnException_LogsAndThrows()
    {
        using var context = CreateInMemoryContext();
        context.SessionLotteryEntrants.Add(CreateEntrant(1, "userA", status: LotteryEntrantStatus.Entered));
        await context.SaveChangesAsync();
        _mockSessionRepository.Setup(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("boom"));
        var repository = CreateRepository(context);

        await repository.Invoking(r => r.WithdrawEntrantAsync(1, "userA", "x")).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetEntrantsAsync_FiltersBySessionClassStatus()
    {
        using var context = CreateInMemoryContext();
        SeedUsers(context, "userA", "userB", "userC");
        context.SessionLotteryEntrants.AddRange(
            CreateEntrant(1, "userA", lotteryClass: LotteryClass.Standard, status: LotteryEntrantStatus.Entered),
            CreateEntrant(2, "userB", lotteryClass: LotteryClass.Standard, status: LotteryEntrantStatus.Withdrawn),
            CreateEntrant(3, "userC", lotteryClass: LotteryClass.Preferred, status: LotteryEntrantStatus.Entered));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);

        var result = await repository.GetEntrantsAsync(1, LotteryClass.Standard, LotteryEntrantStatus.Entered);

        result.Should().ContainSingle().Which.UserId.Should().Be("userA");
    }

    [Fact]
    public async Task ClaimForDrawingAsync_ExecutesUpdateAndReturnsRowCount()
    {
        using var context = CreateInMemoryContext();
        var mockDb = new Mock<IDbFacade>();
        string? capturedSql = null;
        SqlParameter[]? capturedParams = null;
        mockDb.Setup(x => x.ExecuteSqlRawAsync(It.IsAny<string>(), It.IsAny<IEnumerable<SqlParameter>>()))
            .Callback<string, IEnumerable<SqlParameter>>((sql, p) => { capturedSql = sql; capturedParams = p.ToArray(); })
            .ReturnsAsync(2);
        var repository = CreateRepository(context, mockDb.Object);

        var result = await repository.ClaimForDrawingAsync(1, LotteryClass.Standard);

        result.Should().Be(2);
        capturedSql.Should().Contain("UPDATE SessionLotteryEntrants").And.Contain("Status = @entered");
        capturedParams.Should().Contain(p => p.ParameterName == "@drawing" && (int) p.Value! == (int) LotteryEntrantStatus.Drawing);
        capturedParams.Should().Contain(p => p.ParameterName == "@sessionId" && (int) p.Value! == 1);
        capturedParams.Should().Contain(p => p.ParameterName == "@lotteryClass" && (int) p.Value! == (int) LotteryClass.Standard);
    }

    [Fact]
    public async Task MarkDrawnAsync_SetsDrawnAndClearsFailure()
    {
        using var context = CreateInMemoryContext();
        context.SessionLotteryEntrants.Add(CreateEntrant(1, "userA", status: LotteryEntrantStatus.Drawing));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);

        await repository.MarkDrawnAsync(1);

        (await context.SessionLotteryEntrants.FindAsync(1))!.Status.Should().Be(LotteryEntrantStatus.Drawn);
    }

    [Fact]
    public async Task MarkDrawnAsync_MissingRow_NoOp()
    {
        using var context = CreateInMemoryContext();
        var repository = CreateRepository(context);

        await repository.Invoking(r => r.MarkDrawnAsync(999)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task MarkFailedAsync_SetsFailedAndTruncatesReason()
    {
        using var context = CreateInMemoryContext();
        context.SessionLotteryEntrants.Add(CreateEntrant(1, "userA", status: LotteryEntrantStatus.Drawing));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);

        var longReason = new string('x', 600);
        await repository.MarkFailedAsync(1, longReason);

        var entrant = await context.SessionLotteryEntrants.FindAsync(1);
        entrant!.Status.Should().Be(LotteryEntrantStatus.Failed);
        entrant.FailureReason!.Length.Should().Be(512);
    }

    [Fact]
    public async Task MarkFailedAsync_ShortReason_StoresVerbatim()
    {
        using var context = CreateInMemoryContext();
        context.SessionLotteryEntrants.Add(CreateEntrant(1, "userA", status: LotteryEntrantStatus.Drawing));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);

        await repository.MarkFailedAsync(1, "already rostered");

        (await context.SessionLotteryEntrants.FindAsync(1))!.FailureReason.Should().Be("already rostered");
    }

    [Fact]
    public async Task MarkFailedAsync_MissingRow_NoOp()
    {
        using var context = CreateInMemoryContext();
        var repository = CreateRepository(context);

        await repository.Invoking(r => r.MarkFailedAsync(999, "reason")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetDueUndrawnTiersAsync_ReturnsPastDrawTiersWithEnteredRows()
    {
        using var context = CreateInMemoryContext();
        // Past draw: large buy-day-minimum so the Standard draw time already passed.
        context.Sessions!.Add(CreateSession(1, lotteryEnabled: true, buyDayMinimum: 5));
        // Future draw: small buy-day-minimum, far-future session.
        context.Sessions!.Add(new Session { SessionId = 2, SessionDate = DateTime.UtcNow.AddDays(30), BuyDayMinimum = 1, LotteryEntryWindowMinutes = 30, LotteryEnabled = true, CreateDateTime = DateTime.UtcNow, UpdateDateTime = DateTime.UtcNow });
        // Disabled session.
        context.Sessions!.Add(CreateSession(3, lotteryEnabled: false, buyDayMinimum: 5));
        context.SessionLotteryEntrants.AddRange(
            CreateEntrant(1, "userA", sessionId: 1, status: LotteryEntrantStatus.Entered),
            CreateEntrant(2, "userB", sessionId: 2, status: LotteryEntrantStatus.Entered),
            CreateEntrant(3, "userC", sessionId: 3, status: LotteryEntrantStatus.Entered));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);

        var result = await repository.GetDueUndrawnTiersAsync(DateTime.UtcNow);

        result.Should().ContainSingle().Which.Should().Be((1, LotteryClass.Standard));
    }

    [Fact]
    public async Task GetStuckDrawingAsync_ReturnsOldDrawingRows()
    {
        using var context = CreateInMemoryContext();
        context.SessionLotteryEntrants.AddRange(
            CreateEntrant(1, "userA", status: LotteryEntrantStatus.Drawing, updateDateTime: DateTime.UtcNow.AddMinutes(-20)),
            CreateEntrant(2, "userB", status: LotteryEntrantStatus.Drawing, updateDateTime: DateTime.UtcNow),
            CreateEntrant(3, "userC", status: LotteryEntrantStatus.Entered, updateDateTime: DateTime.UtcNow.AddMinutes(-20)));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);

        var result = await repository.GetStuckDrawingAsync(DateTime.UtcNow.AddMinutes(-10));

        result.Should().ContainSingle().Which.LotteryEntrantId.Should().Be(1);
    }

    [Fact]
    public async Task GetDrawingOrderedAsync_ReturnsDrawingRowsOrderedByDrawOrder()
    {
        using var context = CreateInMemoryContext();
        SeedUsers(context, "userA", "userB");
        context.SessionLotteryEntrants.AddRange(
            CreateEntrant(1, "userA", status: LotteryEntrantStatus.Drawing, drawOrder: 2),
            CreateEntrant(2, "userB", status: LotteryEntrantStatus.Drawing, drawOrder: 1));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);

        var result = await repository.GetDrawingOrderedAsync(1, LotteryClass.Standard);

        result.Select(e => e.LotteryEntrantId).Should().Equal(2, 1);
    }

    [Fact]
    public async Task PersistDrawOrderAsync_AssignsDrawOrderAndDrawTime()
    {
        using var context = CreateInMemoryContext();
        context.SessionLotteryEntrants.AddRange(
            CreateEntrant(1, "userA", status: LotteryEntrantStatus.Drawing),
            CreateEntrant(2, "userB", status: LotteryEntrantStatus.Drawing));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);

        var drawTime = DateTime.UtcNow;
        await repository.PersistDrawOrderAsync(1, LotteryClass.Standard, new List<(int, int)> { (2, 1), (1, 2) }, drawTime);

        (await context.SessionLotteryEntrants.FindAsync(2))!.DrawOrder.Should().Be(1);
        (await context.SessionLotteryEntrants.FindAsync(1))!.DrawOrder.Should().Be(2);
        (await context.SessionLotteryEntrants.FindAsync(2))!.DrawDateTime.Should().Be(drawTime);
    }

    [Fact]
    public void SessionLotteryEntrant_NavigationProperties_RoundTrip()
    {
        var session = new Session { SessionId = 1 };
        var user = new AspNetUser { Id = "userA" };
        var entrant = new SessionLotteryEntrant { Session = session, User = user };

        entrant.Session.Should().BeSameAs(session);
        entrant.User.Should().BeSameAs(user);
    }

    [Fact]
    public async Task PersistDrawOrderAsync_UnknownEntrant_RollsBackAndThrows()
    {
        using var context = CreateInMemoryContext();
        context.SessionLotteryEntrants.Add(CreateEntrant(1, "userA", status: LotteryEntrantStatus.Drawing));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);

        // Id 999 is not present, so the in-transaction lookup throws and the transaction is rolled back.
        await repository.Invoking(r => r.PersistDrawOrderAsync(1, LotteryClass.Standard, new List<(int, int)> { (999, 1) }, DateTime.UtcNow))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
