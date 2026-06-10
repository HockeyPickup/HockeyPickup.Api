using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HockeyPickup.Api.Data.Repositories;

public class LotteryRepository : ILotteryRepository
{
    private readonly HockeyPickupContext _context;
    private readonly ILogger<LotteryRepository> _logger;
    private readonly ISessionRepository _sessionRepository;
    private readonly IDbFacade _db;

    public LotteryRepository(HockeyPickupContext context, ILogger<LotteryRepository> logger, ISessionRepository sessionRepository, IDbFacade? db = null)
    {
        _context = context;
        _logger = logger;
        _sessionRepository = sessionRepository;
        _db = db ?? new DbFacade(context.Database);
    }

    public async Task<SessionLotteryEntrant?> GetEntrantAsync(int sessionId, string userId)
    {
        return await _context.SessionLotteryEntrants
            .FirstOrDefaultAsync(e => e.SessionId == sessionId && e.UserId == userId);
    }

    public async Task<SessionLotteryEntrant> CreateOrReactivateEntrantAsync(int sessionId, string userId, LotteryClass lotteryClass, decimal weight, string activityMessage)
    {
        try
        {
            var existing = await GetEntrantAsync(sessionId, userId);
            if (existing != null)
            {
                existing.Status = LotteryEntrantStatus.Entered;
                existing.LotteryClass = lotteryClass;
                existing.Weight = weight;
                existing.DrawOrder = null;
                existing.DrawDateTime = null;
                existing.FailureReason = null;
                existing.UpdateDateTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _sessionRepository.AddActivityAsync(sessionId, activityMessage);
                return existing;
            }

            var entrant = new SessionLotteryEntrant
            {
                SessionId = sessionId,
                UserId = userId,
                LotteryClass = lotteryClass,
                Weight = weight,
                Status = LotteryEntrantStatus.Entered,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow
            };

            await _context.SessionLotteryEntrants.AddAsync(entrant);
            await _context.SaveChangesAsync();

            await _sessionRepository.AddActivityAsync(sessionId, activityMessage);
            return entrant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating/reactivating lottery entrant for session {sessionId}, user {userId}: {ex.GetRelevantMessage()}");
            throw;
        }
    }

    public async Task<SessionLotteryEntrant?> WithdrawEntrantAsync(int sessionId, string userId, string activityMessage)
    {
        try
        {
            var existing = await GetEntrantAsync(sessionId, userId);
            if (existing == null || existing.Status != LotteryEntrantStatus.Entered)
                return null;

            existing.Status = LotteryEntrantStatus.Withdrawn;
            existing.UpdateDateTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _sessionRepository.AddActivityAsync(sessionId, activityMessage);
            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error withdrawing lottery entrant for session {sessionId}, user {userId}: {ex.GetRelevantMessage()}");
            throw;
        }
    }

    public async Task<List<SessionLotteryEntrant>> GetEntrantsAsync(int sessionId, LotteryClass lotteryClass, LotteryEntrantStatus status)
    {
        return await _context.SessionLotteryEntrants
            .Include(e => e.User)
            .Where(e => e.SessionId == sessionId && e.LotteryClass == lotteryClass && e.Status == status)
            .ToListAsync();
    }

    public async Task<int> ClaimForDrawingAsync(int sessionId, LotteryClass lotteryClass)
    {
        const string sql = "UPDATE SessionLotteryEntrants SET Status = @drawing, UpdateDateTime = @now " +
                           "WHERE SessionId = @sessionId AND LotteryClass = @lotteryClass AND Status = @entered";

        return await _db.ExecuteSqlRawAsync(sql, new[]
        {
            new SqlParameter("@drawing", (int) LotteryEntrantStatus.Drawing),
            new SqlParameter("@now", DateTime.UtcNow),
            new SqlParameter("@sessionId", sessionId),
            new SqlParameter("@lotteryClass", (int) lotteryClass),
            new SqlParameter("@entered", (int) LotteryEntrantStatus.Entered)
        });
    }

    public async Task PersistDrawOrderAsync(int sessionId, LotteryClass lotteryClass, IReadOnlyList<(int LotteryEntrantId, int DrawOrder)> ordered, DateTime drawDateTimeUtc)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ids = ordered.Select(o => o.LotteryEntrantId).ToList();
                var rows = await _context.SessionLotteryEntrants
                    .Where(e => ids.Contains(e.LotteryEntrantId))
                    .ToListAsync();

                foreach (var (lotteryEntrantId, drawOrder) in ordered)
                {
                    var row = rows.First(r => r.LotteryEntrantId == lotteryEntrantId);
                    row.DrawOrder = drawOrder;
                    row.DrawDateTime = drawDateTimeUtc;
                    row.UpdateDateTime = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task MarkDrawnAsync(int lotteryEntrantId)
    {
        var entrant = await _context.SessionLotteryEntrants.FirstOrDefaultAsync(e => e.LotteryEntrantId == lotteryEntrantId);
        if (entrant == null)
            return;

        entrant.Status = LotteryEntrantStatus.Drawn;
        entrant.FailureReason = null;
        entrant.UpdateDateTime = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task MarkFailedAsync(int lotteryEntrantId, string failureReason)
    {
        var entrant = await _context.SessionLotteryEntrants.FirstOrDefaultAsync(e => e.LotteryEntrantId == lotteryEntrantId);
        if (entrant == null)
            return;

        entrant.Status = LotteryEntrantStatus.Failed;
        entrant.FailureReason = failureReason.Length > 512 ? failureReason[..512] : failureReason;
        entrant.UpdateDateTime = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<List<(int SessionId, LotteryClass LotteryClass)>> GetDueUndrawnTiersAsync(DateTime nowPacific)
    {
        var candidates = await _context.SessionLotteryEntrants
            .Where(e => e.Status == LotteryEntrantStatus.Entered && e.Session!.LotteryEnabled)
            .Select(e => new
            {
                e.SessionId,
                e.LotteryClass,
                e.Session!.SessionDate,
                e.Session.BuyDayMinimum,
                e.Session.LotteryEntryWindowMinutes
            })
            .Distinct()
            .ToListAsync();

        return candidates
            .Where(c => LotteryWindowExtensions.DrawTimeFor(c.SessionDate, c.BuyDayMinimum, c.LotteryEntryWindowMinutes, c.LotteryClass) <= nowPacific)
            .Select(c => (c.SessionId, c.LotteryClass))
            .Distinct()
            .ToList();
    }

    public async Task<List<SessionLotteryEntrant>> GetStuckDrawingAsync(DateTime olderThanUtc)
    {
        return await _context.SessionLotteryEntrants
            .Where(e => e.Status == LotteryEntrantStatus.Drawing && e.UpdateDateTime < olderThanUtc)
            .ToListAsync();
    }

    public async Task<List<SessionLotteryEntrant>> GetDrawingOrderedAsync(int sessionId, LotteryClass lotteryClass)
    {
        return await _context.SessionLotteryEntrants
            .Include(e => e.User)
            .Where(e => e.SessionId == sessionId && e.LotteryClass == lotteryClass && e.Status == LotteryEntrantStatus.Drawing)
            .OrderBy(e => e.DrawOrder)
            .ToListAsync();
    }
}
