using HockeyPickup.Api.Data.Entities;

namespace HockeyPickup.Api.Data.Repositories;

public interface ILotteryRepository
{
    Task<SessionLotteryEntrant?> GetEntrantAsync(int sessionId, string userId);
    Task<SessionLotteryEntrant> CreateOrReactivateEntrantAsync(int sessionId, string userId, LotteryClass lotteryClass, decimal weight, string activityMessage);
    Task<SessionLotteryEntrant?> WithdrawEntrantAsync(int sessionId, string userId, string activityMessage);
    Task<List<SessionLotteryEntrant>> GetEntrantsAsync(int sessionId, LotteryClass lotteryClass, LotteryEntrantStatus status);

    // Atomic claim: a single UPDATE that flips Entered -> Drawing. Returns the number of rows claimed (0 = nothing to draw).
    Task<int> ClaimForDrawingAsync(int sessionId, LotteryClass lotteryClass);

    // Persists the shuffled draw order + draw time for the claimed rows, committed before any buy is processed.
    Task PersistDrawOrderAsync(int sessionId, LotteryClass lotteryClass, IReadOnlyList<(int LotteryEntrantId, int DrawOrder)> ordered, DateTime drawDateTimeUtc);

    Task MarkDrawnAsync(int lotteryEntrantId);
    Task MarkFailedAsync(int lotteryEntrantId, string failureReason);

    // Safety-net sweep helpers.
    Task<List<(int SessionId, LotteryClass LotteryClass)>> GetDueUndrawnTiersAsync(DateTime nowPacific);
    Task<List<SessionLotteryEntrant>> GetStuckDrawingAsync(DateTime olderThanUtc);
    Task<List<SessionLotteryEntrant>> GetDrawingOrderedAsync(int sessionId, LotteryClass lotteryClass);
}
