using HockeyPickup.Api.Data.Entities;

namespace HockeyPickup.Api.Data.Repositories;

public interface IBuySellRepository
{
    Task<BuySell> CreateBuySellAsync(BuySell BuySell);
    Task<BuySell> UpdateBuySellAsync(BuySell BuySell);
    Task<BuySell?> GetBuySellAsync(int BuySellId);
    Task<IEnumerable<BuySell>> GetSessionBuySellsAsync(int sessionId);
    Task<IEnumerable<BuySell>> GetUserBuySellsAsync(string userId);
    Task<IEnumerable<BuySell>> GetUserBuySellsAsync(int sessionId, string userId);
    Task<BuySell?> FindMatchingSellBuySellAsync(int sessionId);
    Task<BuySell?> FindMatchingBuyBuySellAsync(int sessionId);
    Task<int?> GetQueuePositionAsync(int BuySellId);
    Task<bool> DeleteBuySellAsync(int buySellId);
}