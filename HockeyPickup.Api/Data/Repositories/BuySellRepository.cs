using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HockeyPickup.Api.Data.Repositories;

public class BuySellRepository : IBuySellRepository
{
    private readonly HockeyPickupContext _context;
    private readonly ILogger<BuySellRepository> _logger;
    private readonly ISessionRepository _sessionRepository;

    public BuySellRepository(HockeyPickupContext context, ILogger<BuySellRepository> logger, ISessionRepository sessionRepository)
    {
        _context = context;
        _logger = logger;
        _sessionRepository = sessionRepository;
    }

    public async Task<BuySell> CreateBuySellAsync(BuySell buySell, string message)
    {
        try
        {
            await _context.BuySells.AddAsync(buySell);
            await _context.SaveChangesAsync();

            await _sessionRepository.AddActivityAsync(buySell.SessionId, message);

            return buySell;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating BuySell {buySell.BuySellId}: {ex.GetRelevantMessage}");
            throw;
        }
    }

    public async Task<BuySell> UpdateBuySellAsync(BuySell buySell, string message)
    {
        try
        {
            _context.BuySells.Update(buySell);
            await _context.SaveChangesAsync();

            await _sessionRepository.AddActivityAsync(buySell.SessionId, message);

            return buySell;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating BuySell {buySell.BuySellId}: {ex.GetRelevantMessage}");
            throw;
        }
    }

    public async Task<bool> DeleteBuySellAsync(int buySellId, string message)
    {
        try
        {
            var buySell = await GetBuySellAsync(buySellId);
            if (buySell == null)
                return false;

            _context.BuySells.Remove(buySell);
            await _context.SaveChangesAsync();

            await _sessionRepository.AddActivityAsync(buySell.SessionId, message);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting BuySell {buySellId}: {ex.GetRelevantMessage}");
            throw;
        }
    }

    public async Task<BuySell?> GetBuySellAsync(int buySellId)
    {
        try
        {
            return await _context.BuySells
                        .Include(b => b.Session)
                        .Include(b => b.Buyer)
                        .Include(b => b.Seller)
                        .FirstOrDefaultAsync(t => t.BuySellId == buySellId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting BuySell {buySellId}: {ex.GetRelevantMessage}");
            throw;
        }
    }

    public async Task<IEnumerable<BuySell>> GetSessionBuySellsAsync(int sessionId)
    {
        try
        {
            return await _context.BuySells
                        .Include(b => b.Session)
                        .Include(b => b.Buyer)
                        .Include(b => b.Seller)
                        .Where(t => t.SessionId == sessionId).OrderByDescending(t => t.CreateDateTime).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting BuySells for Session {sessionId}: {ex.GetRelevantMessage}");
            throw;
        }
    }

    public async Task<IEnumerable<BuySell>> GetUserBuySellsAsync(string userId)
    {
        try
        {
            return await _context.BuySells
                        .Include(b => b.Session)
                        .Include(b => b.Buyer)
                        .Include(b => b.Seller)
                        .Where(t => t.BuyerUserId == userId || t.SellerUserId == userId).OrderByDescending(t => t.CreateDateTime).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting BuySells for User {userId}: {ex.GetRelevantMessage}");
            throw;
        }
    }

    public async Task<IEnumerable<BuySell>> GetUserBuySellsAsync(int sessionId, string userId)
    {
        try
        {
            return await _context.BuySells
                        .Include(b => b.Session)
                        .Include(b => b.Buyer)
                        .Include(b => b.Seller)
                        .Where(t => t.SessionId == sessionId && (t.BuyerUserId == userId || t.SellerUserId == userId)).OrderByDescending(t => t.CreateDateTime).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting BuySells for User {userId} and Session {sessionId}: {ex.GetRelevantMessage}");
            throw;
        }
    }

    public async Task<BuySell?> FindMatchingSellBuySellAsync(int sessionId)
    {
        try
        {
            return await _context.BuySells
                        .Include(b => b.Session)
                        .Include(b => b.Buyer)
                        .Include(b => b.Seller)
                        .Where(t => t.SessionId == sessionId && t.SellerUserId != null && t.BuyerUserId == null).OrderBy(t => t.CreateDateTime).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error finding matching Sell BuySells for Session {sessionId}: {ex.GetRelevantMessage}");
            throw;
        }
    }

    public async Task<BuySell?> FindMatchingBuyBuySellAsync(int sessionId)
    {
        try
        {
            return await _context.BuySells
                        .Include(b => b.Session)
                        .Include(b => b.Buyer)
                        .Include(b => b.Seller)
                        .Where(t => t.SessionId == sessionId && t.BuyerUserId != null && t.SellerUserId == null).OrderBy(t => t.CreateDateTime).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error finding matching Buy BuySells for Session {sessionId}: {ex.GetRelevantMessage}");
            throw;
        }
    }

    public async Task<int?> GetQueuePositionAsync(int buySellId)
    {
        try
        {
            var buySell = await GetBuySellAsync(buySellId);
            if (buySell == null)
                return null;

            // Get queue position based on creation time of pending BuySells
            var position = await _context.BuySells.Where(t => t.SessionId == buySell.SessionId && t.BuyerUserId == null).CountAsync();

            return position + 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting queue position for BuySell {buySellId}: {ex.GetRelevantMessage}");
            throw;
        }
    }
}
