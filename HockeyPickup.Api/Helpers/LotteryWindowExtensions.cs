using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Helpers;

// Maps a LotteryClass tier to the matching computed window on a session, and a user to their own tier.
// "Lower tier" ordering is PreferredPlus > Preferred > Standard.
public static class LotteryWindowExtensions
{
    public static DateTime LotteryEntryOpenFor(this SessionDetailedResponse session, LotteryClass lotteryClass) => lotteryClass switch
    {
        LotteryClass.PreferredPlus => session.LotteryEntryOpenPreferredPlus,
        LotteryClass.Preferred => session.LotteryEntryOpenPreferred,
        _ => session.LotteryEntryOpenStandard
    };

    public static DateTime LotteryDrawFor(this SessionDetailedResponse session, LotteryClass lotteryClass) => lotteryClass switch
    {
        LotteryClass.PreferredPlus => session.LotteryDrawPreferredPlus,
        LotteryClass.Preferred => session.LotteryDrawPreferred,
        _ => session.LotteryDrawStandard
    };

    public static LotteryClass TierOf(this AspNetUser user) =>
        user.PreferredPlus ? LotteryClass.PreferredPlus
        : user.Preferred ? LotteryClass.Preferred
        : LotteryClass.Standard;

    // Tiers strictly below the given tier, in descending priority (e.g. PreferredPlus -> [Preferred, Standard]).
    public static IEnumerable<LotteryClass> TiersBelow(this LotteryClass lotteryClass)
    {
        if (lotteryClass == LotteryClass.PreferredPlus)
        {
            yield return LotteryClass.Preferred;
            yield return LotteryClass.Standard;
        }
        else if (lotteryClass == LotteryClass.Preferred)
        {
            yield return LotteryClass.Standard;
        }
    }

    // Entity-side window math (Pacific) for callers that only have raw session values (e.g. the sweep).
    // Mirrors SessionDetailedResponse.BuyWindow* exactly; a drift-guard test asserts the two stay in sync.
    public static DateTime BuyWindowFor(DateTime sessionDate, int? buyDayMinimum, LotteryClass lotteryClass)
    {
        var bdm = buyDayMinimum.GetValueOrDefault();
        return lotteryClass switch
        {
            LotteryClass.PreferredPlus => sessionDate.AddDays(-bdm - 1).AddHours(2).AddMinutes(-5),
            LotteryClass.Preferred => sessionDate.AddDays(-bdm - 1).AddHours(2),
            _ => sessionDate.AddDays(-bdm).AddHours(2)
        };
    }

    public static DateTime DrawTimeFor(DateTime sessionDate, int? buyDayMinimum, int entryWindowMinutes, LotteryClass lotteryClass)
        => BuyWindowFor(sessionDate, buyDayMinimum, lotteryClass).AddMinutes(entryWindowMinutes);
}
