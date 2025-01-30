using HockeyPickup.Api.Data.Entities;

namespace HockeyPickup.Api.Data.Repositories;

public interface IBuySellRepository
{
    Task<BuySell> CreateBuySellAsync(BuySell buySell, string message);
    Task<BuySell> UpdateBuySellAsync(BuySell buySell, string message);
    Task<bool> DeleteBuySellAsync(int buySellId, string message);
    Task<BuySell?> GetBuySellAsync(int buySellId);
    Task<IEnumerable<BuySell>> GetSessionBuySellsAsync(int sessionId);
    Task<IEnumerable<BuySell>> GetUserBuySellsAsync(string userId);
    Task<IEnumerable<BuySell>> GetUserBuySellsAsync(int sessionId, string userId);
    Task<BuySell?> FindMatchingSellBuySellAsync(int sessionId);
    Task<BuySell?> FindMatchingBuyBuySellAsync(int sessionId);
    Task<int?> GetQueuePositionAsync(int buySellId);
}