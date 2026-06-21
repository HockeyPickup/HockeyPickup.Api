using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Services;

// Pure result of evaluating the lottery tier/latecomer rule (spec §4) for one user at one moment.
public sealed class LotteryEligibility
{
    public required BuyActionState State { get; init; }
    public LotteryClass? ChosenClass { get; init; }
    public TimeSpan? TimeUntilDraw { get; init; }
    public required string Reason { get; init; }
    // True only for the "direct buy" path (rule 3) - the caller falls through to the legacy window check.
    public bool AllowDirectBuy { get; init; }
}

public interface ILotteryEligibilityService
{
    LotteryEligibility Resolve(SessionDetailedResponse session, AspNetUser buyer, SessionLotteryEntrant? existingEntrant, DateTime nowPacific);
}

// "Undrawn" is defined purely by the clock: a tier is undrawn at time T iff T < that tier's computed draw time.
// Entry eligibility never inspects entrant Status to decide whether a tier has drawn; the entrant row is consulted
// only to distinguish EnterLottery (no row / Withdrawn / lost a prior draw) from InLottery (currently Entered/Drawing).
public class LotteryEligibilityService : ILotteryEligibilityService
{
    public LotteryEligibility Resolve(SessionDetailedResponse session, AspNetUser buyer, SessionLotteryEntrant? existingEntrant, DateTime nowPacific)
    {
        var ownClass = buyer.TierOf();
        var ownEntryOpen = session.LotteryEntryOpenFor(ownClass);
        var ownDraw = session.LotteryDrawFor(ownClass);
        var ownBuyWindowOpen = nowPacific >= ownEntryOpen;

        // Lottery disabled - no lottery applies; defer to a direct buy when the window is open, else not open.
        if (!session.LotteryEnabled)
        {
            return ownBuyWindowOpen
                ? new LotteryEligibility { State = BuyActionState.BuyNow, Reason = "You can buy a spot for this session", AllowDirectBuy = true }
                : new LotteryEligibility { State = BuyActionState.WindowNotOpen, Reason = "Your buy window is not open yet", TimeUntilDraw = ownEntryOpen - nowPacific };
        }

        // Already entered/being drawn - the user is in a lottery regardless of which rule would otherwise apply.
        if (existingEntrant != null && (existingEntrant.Status == LotteryEntrantStatus.Entered || existingEntrant.Status == LotteryEntrantStatus.Drawing))
        {
            var enteredDraw = session.LotteryDrawFor(existingEntrant.LotteryClass);
            return new LotteryEligibility
            {
                State = BuyActionState.InLottery,
                ChosenClass = existingEntrant.LotteryClass,
                TimeUntilDraw = enteredDraw > nowPacific ? enteredDraw - nowPacific : TimeSpan.Zero,
                Reason = $"You are entered in the {existingEntrant.LotteryClass} lottery"
            };
        }

        // Rule 1: own tier's entry window [entryOpen, draw) contains T and the tier is undrawn (T < draw).
        if (nowPacific >= ownEntryOpen && nowPacific < ownDraw)
        {
            return new LotteryEligibility
            {
                State = BuyActionState.EnterLottery,
                ChosenClass = ownClass,
                TimeUntilDraw = ownDraw - nowPacific,
                Reason = $"You can enter the {ownClass} lottery"
            };
        }

        // Rule 2: a lower tier's entry window contains T and is undrawn, AND the user's own buy window has opened.
        if (ownBuyWindowOpen)
        {
            foreach (var lower in ownClass.TiersBelow())
            {
                var lowerEntryOpen = session.LotteryEntryOpenFor(lower);
                var lowerDraw = session.LotteryDrawFor(lower);
                if (nowPacific >= lowerEntryOpen && nowPacific < lowerDraw)
                {
                    return new LotteryEligibility
                    {
                        State = BuyActionState.EnterLottery,
                        ChosenClass = lower,
                        TimeUntilDraw = lowerDraw - nowPacific,
                        Reason = $"You can enter the {lower} lottery"
                    };
                }
            }
        }

        // Rule 3: own buy window opened and no applicable entry window is open -> normal direct buy.
        if (ownBuyWindowOpen)
        {
            return new LotteryEligibility { State = BuyActionState.BuyNow, Reason = "You can buy a spot for this session", AllowDirectBuy = true };
        }

        // Rule 4: own buy window not open -> denied.
        return new LotteryEligibility
        {
            State = BuyActionState.WindowNotOpen,
            Reason = "Your buy window is not open yet",
            TimeUntilDraw = ownEntryOpen - nowPacific
        };
    }
}
