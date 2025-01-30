using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
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

    public async Task<BuySell> CreateBuySellAsync(BuySell buySell)
    {
        try
        {
            _context.Set<BuySell>().Add(buySell);
            await _context.SaveChangesAsync();

            await _sessionRepository.AddActivityAsync(buySell.SessionId, $"{buySell.Buyer.FirstName} {buySell.Buyer.LastName} added to BUYING queue");

            return buySell;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating BuySell");
            throw;
        }
    }

    public async Task<BuySell> UpdateBuySellAsync(BuySell buySell)
    {
        try
        {
            _context.Set<BuySell>().Update(buySell);
            await _context.SaveChangesAsync();

            await _sessionRepository.AddActivityAsync(buySell.SessionId, $"Buyer: {buySell.Buyer.FirstName} {buySell.Buyer.LastName}, Seller {buySell.Seller.FirstName} {buySell.Seller.LastName} updated Buy/Sell record");

            return buySell;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating BuySell {BuySellId}", buySell.BuySellId);
            throw;
        }
    }

    public async Task<BuySell?> GetBuySellAsync(int buySellId)
    {
        try
        {
            return await _context.Set<BuySell>()
                .FirstOrDefaultAsync(t => t.BuySellId == buySellId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting BuySell {BuySellId}", buySellId);
            throw;
        }
    }

    public async Task<IEnumerable<BuySell>> GetSessionBuySellsAsync(int sessionId)
    {
        try
        {
            return await _context.Set<BuySell>()
                .Where(t => t.SessionId == sessionId)
                .OrderByDescending(t => t.CreateDateTime)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting BuySells for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<IEnumerable<BuySell>> GetUserBuySellsAsync(string userId)
    {
        try
        {
            return await _context.Set<BuySell>()
                .Where(t => t.BuyerUserId == userId || t.SellerUserId == userId)
                .OrderByDescending(t => t.CreateDateTime)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting BuySells for user {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<BuySell>> GetUserBuySellsAsync(int sessionId, string userId)
    {
        try
        {
            return await _context.Set<BuySell>()
                .Where(t => t.SessionId == sessionId && (t.BuyerUserId == userId || t.SellerUserId == userId))
                .OrderByDescending(t => t.CreateDateTime)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting BuySells for user {UserId} and session {SessionId}", userId, sessionId);
            throw;
        }
    }

    public async Task<BuySell?> FindMatchingSellBuySellAsync(int sessionId)
    {
        try
        {
            return await _context.Set<BuySell>()
                .Where(t => t.SessionId == sessionId
                    && t.SellerUserId != null
                    && t.BuyerUserId == null)
                .OrderBy(t => t.CreateDateTime)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matching sell BuySell for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<BuySell?> FindMatchingBuyBuySellAsync(int sessionId)
    {
        try
        {
            return await _context.Set<BuySell>()
                .Where(t => t.SessionId == sessionId
                    && t.BuyerUserId != null
                    && t.SellerUserId == null)
                .OrderBy(t => t.CreateDateTime)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matching buy BuySell for session {SessionId}", sessionId);
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
            var position = await _context.Set<BuySell>()
                .Where(t => t.SessionId == buySell.SessionId
                    && t.BuyerUserId == null)
                .CountAsync();

            return position + 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue position for BuySell {BuySellId}", buySellId);
            throw;
        }
    }

    public async Task<bool> DeleteBuySellAsync(int buySellId)
    {
        try
        {
            var buySell = await GetBuySellAsync(buySellId);
            if (buySell == null)
                return false;

            _context.BuySells.Remove(buySell);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting BuySell");
            throw;
        }
    }
}
