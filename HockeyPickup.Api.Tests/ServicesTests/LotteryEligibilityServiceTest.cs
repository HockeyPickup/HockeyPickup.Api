using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Xunit;

namespace HockeyPickup.Api.Tests.ServicesTests;

public class LotteryEligibilityServiceTests
{
    private readonly LotteryEligibilityService _service = new();

    // Spec example: session 02/25 7:30am, BuyDayMinimum 6, window 30 min.
    // PreferredPlus entry 02/18 9:25-9:55 (draw 9:55), Preferred entry 02/18 9:30-10:00 (draw 10:00),
    // Standard entry 02/19 9:30-10:00 (draw 10:00).
    private static readonly DateTime SessionDate = new(2026, 2, 25, 7, 30, 0);

    private static SessionDetailedResponse CreateSession(bool lotteryEnabled = true) => new()
    {
        SessionId = 1,
        SessionDate = SessionDate,
        BuyDayMinimum = 6,
        LotteryEntryWindowMinutes = 30,
        LotteryEnabled = lotteryEnabled,
        CreateDateTime = DateTime.UtcNow,
        UpdateDateTime = DateTime.UtcNow
    };

    private static AspNetUser CreateUser(bool preferred = false, bool preferredPlus = false) => new()
    {
        Id = "user1",
        FirstName = "Test",
        LastName = "User",
        Email = "test@example.com",
        Active = true,
        Preferred = preferred,
        PreferredPlus = preferredPlus
    };

    private static SessionLotteryEntrant Entrant(LotteryClass lotteryClass, LotteryEntrantStatus status) => new()
    {
        LotteryEntrantId = 5,
        SessionId = 1,
        UserId = "user1",
        LotteryClass = lotteryClass,
        Status = status
    };

    // Scenario 1: PreferredPlus player at 02/18 9:40 -> own tier window open -> EnterLottery PreferredPlus.
    [Fact]
    public void Resolve_PreferredPlus_OwnWindowOpen_EntersOwnTier()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(preferredPlus: true), null, new DateTime(2026, 2, 18, 9, 40, 0));

        result.State.Should().Be(BuyActionState.EnterLottery);
        result.ChosenClass.Should().Be(LotteryClass.PreferredPlus);
        result.TimeUntilDraw.Should().Be(new DateTime(2026, 2, 18, 9, 55, 0) - new DateTime(2026, 2, 18, 9, 40, 0));
    }

    // Scenario 2: PreferredPlus player at 02/18 9:57 -> PP drawn, Preferred still open -> EnterLottery Preferred.
    [Fact]
    public void Resolve_PreferredPlus_Latecomer_EntersLowerPreferredTier()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(preferredPlus: true), null, new DateTime(2026, 2, 18, 9, 57, 0));

        result.State.Should().Be(BuyActionState.EnterLottery);
        result.ChosenClass.Should().Be(LotteryClass.Preferred);
    }

    // Scenario 3: Preferred player at 02/18 9:40 -> own tier window open -> EnterLottery Preferred.
    [Fact]
    public void Resolve_Preferred_OwnWindowOpen_EntersOwnTier()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(preferred: true), null, new DateTime(2026, 2, 18, 9, 40, 0));

        result.State.Should().Be(BuyActionState.EnterLottery);
        result.ChosenClass.Should().Be(LotteryClass.Preferred);
    }

    // Scenario 4: Preferred player at 02/18 3:00pm -> all relevant windows closed, own window open -> direct buy.
    [Fact]
    public void Resolve_Preferred_NoWindowOpen_DirectBuy()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(preferred: true), null, new DateTime(2026, 2, 18, 15, 0, 0));

        result.State.Should().Be(BuyActionState.BuyNow);
        result.AllowDirectBuy.Should().BeTrue();
    }

    // Scenario 5: PreferredPlus player at 02/18 3:00pm -> direct buy.
    [Fact]
    public void Resolve_PreferredPlus_NoWindowOpen_DirectBuy()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(preferredPlus: true), null, new DateTime(2026, 2, 18, 15, 0, 0));

        result.State.Should().Be(BuyActionState.BuyNow);
        result.AllowDirectBuy.Should().BeTrue();
    }

    // Scenario 6: Standard player at 02/18 9:45 -> own window not open (opens 02/19), no lower tier -> denied.
    [Fact]
    public void Resolve_Standard_BeforeOwnWindow_WindowNotOpen()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(), null, new DateTime(2026, 2, 18, 9, 45, 0));

        result.State.Should().Be(BuyActionState.WindowNotOpen);
        result.TimeUntilDraw.Should().Be(new DateTime(2026, 2, 19, 9, 30, 0) - new DateTime(2026, 2, 18, 9, 45, 0));
    }

    // Scenario 7: Standard player at 02/19 9:45 -> own tier window open -> EnterLottery Standard.
    [Fact]
    public void Resolve_Standard_OwnWindowOpen_EntersOwnTier()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(), null, new DateTime(2026, 2, 19, 9, 45, 0));

        result.State.Should().Be(BuyActionState.EnterLottery);
        result.ChosenClass.Should().Be(LotteryClass.Standard);
    }

    // Scenario 8: Preferred player at 02/19 9:45 -> Preferred drawn, Standard open -> EnterLottery Standard.
    [Fact]
    public void Resolve_Preferred_Latecomer_EntersStandard()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(preferred: true), null, new DateTime(2026, 2, 19, 9, 45, 0));

        result.State.Should().Be(BuyActionState.EnterLottery);
        result.ChosenClass.Should().Be(LotteryClass.Standard);
    }

    // Scenario 9: PreferredPlus player at 02/19 10:30 -> all tiers drawn -> direct buy.
    [Fact]
    public void Resolve_PreferredPlus_AllTiersDrawn_DirectBuy()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(preferredPlus: true), null, new DateTime(2026, 2, 19, 10, 30, 0));

        result.State.Should().Be(BuyActionState.BuyNow);
        result.AllowDirectBuy.Should().BeTrue();
    }

    // Scenario 11: Preferred player at exactly 10:00:00 (draw instant, exclusive) -> entry window closed -> direct buy.
    [Fact]
    public void Resolve_Preferred_AtExactDrawInstant_DirectBuy()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(preferred: true), null, new DateTime(2026, 2, 18, 10, 0, 0));

        result.State.Should().Be(BuyActionState.BuyNow);
        result.AllowDirectBuy.Should().BeTrue();
    }

    // Boundary: T == entry open instant is inclusive -> EnterLottery.
    [Fact]
    public void Resolve_Standard_AtExactEntryOpenInstant_EntersLottery()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(), null, new DateTime(2026, 2, 19, 9, 30, 0));

        result.State.Should().Be(BuyActionState.EnterLottery);
        result.ChosenClass.Should().Be(LotteryClass.Standard);
    }

    [Fact]
    public void Resolve_OwnTier_AlreadyEntered_InLottery()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(preferredPlus: true), Entrant(LotteryClass.PreferredPlus, LotteryEntrantStatus.Entered), new DateTime(2026, 2, 18, 9, 40, 0));

        result.State.Should().Be(BuyActionState.InLottery);
        result.ChosenClass.Should().Be(LotteryClass.PreferredPlus);
        result.TimeUntilDraw.Should().Be(new DateTime(2026, 2, 18, 9, 55, 0) - new DateTime(2026, 2, 18, 9, 40, 0));
    }

    [Fact]
    public void Resolve_Entered_AfterDrawTime_InLotteryZeroTime()
    {
        // Still Entered but the draw time already passed (draw lagging) -> InLottery, TimeUntilDraw clamped to zero.
        var result = _service.Resolve(CreateSession(), CreateUser(preferredPlus: true), Entrant(LotteryClass.PreferredPlus, LotteryEntrantStatus.Entered), new DateTime(2026, 2, 18, 10, 0, 0));

        result.State.Should().Be(BuyActionState.InLottery);
        result.TimeUntilDraw.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Resolve_Drawing_InLottery()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(preferred: true), Entrant(LotteryClass.Preferred, LotteryEntrantStatus.Drawing), new DateTime(2026, 2, 18, 9, 40, 0));

        result.State.Should().Be(BuyActionState.InLottery);
        result.ChosenClass.Should().Be(LotteryClass.Preferred);
    }

    [Fact]
    public void Resolve_Withdrawn_CanReenter_EntersLottery()
    {
        var result = _service.Resolve(CreateSession(), CreateUser(preferred: true), Entrant(LotteryClass.Preferred, LotteryEntrantStatus.Withdrawn), new DateTime(2026, 2, 18, 9, 40, 0));

        result.State.Should().Be(BuyActionState.EnterLottery);
        result.ChosenClass.Should().Be(LotteryClass.Preferred);
    }

    [Fact]
    public void Resolve_Failed_PreferredPlus_CanEnterLowerTier()
    {
        // A PP entrant who lost the PP draw (Failed) may still enter the Preferred draw as a latecomer.
        var result = _service.Resolve(CreateSession(), CreateUser(preferredPlus: true), Entrant(LotteryClass.PreferredPlus, LotteryEntrantStatus.Failed), new DateTime(2026, 2, 18, 9, 57, 0));

        result.State.Should().Be(BuyActionState.EnterLottery);
        result.ChosenClass.Should().Be(LotteryClass.Preferred);
    }

    [Fact]
    public void Resolve_LotteryDisabled_WindowOpen_DirectBuy()
    {
        var result = _service.Resolve(CreateSession(lotteryEnabled: false), CreateUser(preferred: true), null, new DateTime(2026, 2, 18, 9, 40, 0));

        result.State.Should().Be(BuyActionState.BuyNow);
        result.AllowDirectBuy.Should().BeTrue();
    }

    [Fact]
    public void Resolve_LotteryDisabled_WindowNotOpen_WindowNotOpen()
    {
        var result = _service.Resolve(CreateSession(lotteryEnabled: false), CreateUser(), null, new DateTime(2026, 2, 18, 9, 45, 0));

        result.State.Should().Be(BuyActionState.WindowNotOpen);
        result.TimeUntilDraw.Should().NotBeNull();
    }
}
