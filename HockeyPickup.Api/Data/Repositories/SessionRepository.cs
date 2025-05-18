using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Data;
using KeyNotFoundException = System.Collections.Generic.KeyNotFoundException;

namespace HockeyPickup.Api.Data.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly HockeyPickupContext _context;
    private readonly ILogger<SessionRepository> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly decimal _cost;

    public SessionRepository(HockeyPickupContext context, ILogger<SessionRepository> logger, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _cost = decimal.Parse(_configuration["SessionBuyPrice"]);
    }

    public async Task<SessionDetailedResponse> AddActivityAsync(int sessionId, string activity)
    {
        var activityLog = new ActivityLog
        {
            SessionId = sessionId,
            UserId = _httpContextAccessor.GetUserId(),
            CreateDateTime = DateTime.UtcNow,
            Activity = activity
        };

        await _context.ActivityLogs.AddAsync(activityLog);

        await _context.SaveChangesAsync();
        _context.DetachChangeTracker();

        // Fetch and return updated session details
        var session = await GetSessionAsync(activityLog.SessionId);

        return session;
    }

    public async Task<SessionDetailedResponse> CreateSessionAsync(Session session)
    {
        // Set default values for new session
        session.CreateDateTime = DateTime.UtcNow;
        session.UpdateDateTime = DateTime.UtcNow;

        await _context.Sessions!.AddAsync(session);
        await _context.SaveChangesAsync();

        // Clear tracking to ensure fresh data
        _context.DetachChangeTracker();

        // Fetch and return the created session with all related data
        var createdSession = await GetSessionAsync(session.SessionId);
        return createdSession!;
    }

    public async Task<SessionDetailedResponse> UpdateSessionAsync(Session session)
    {
        var existingSession = await _context.Sessions!.FirstOrDefaultAsync(s => s.SessionId == session.SessionId);
        if (existingSession == null)
        {
            throw new KeyNotFoundException($"Session not found with Id: {session.SessionId}");
        }

        // Update only the fields that can be modified
        existingSession.SessionDate = session.SessionDate;
        existingSession.Note = session.Note;
        existingSession.RegularSetId = session.RegularSetId;
        existingSession.BuyDayMinimum = session.BuyDayMinimum;
        existingSession.Cost = session.Cost;
        existingSession.UpdateDateTime = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Clear tracking to ensure fresh data
        _context.DetachChangeTracker();

        // Fetch and return the updated session with all related data
        var updatedSession = await GetSessionAsync(session.SessionId);
        return updatedSession!;
    }

    public async Task<SessionDetailedResponse> UpdatePlayerPositionAsync(int sessionId, string userId, PositionPreference position)
    {
        // Find and update the roster entry
        var rosterEntry = await _context.SessionRosters.FirstOrDefaultAsync(sr => sr.SessionId == sessionId && sr.UserId == userId);
        if (rosterEntry == null)
        {
            throw new KeyNotFoundException($"Player not found in session roster");
        }

        rosterEntry.Position = position;

        await _context.SaveChangesAsync();
        _context.DetachChangeTracker();

        // Fetch and return updated session details
        var session = await GetSessionAsync(sessionId);

        return session;
    }

    public async Task<SessionDetailedResponse> UpdatePlayerTeamAsync(int sessionId, string userId, TeamAssignment team)
    {
        // Find and update the roster entry
        var rosterEntry = await _context.SessionRosters.FirstOrDefaultAsync(sr => sr.SessionId == sessionId && sr.UserId == userId);
        if (rosterEntry == null)
        {
            throw new KeyNotFoundException($"Player not found in session roster");
        }

        rosterEntry.TeamAssignment = team;

        await _context.SaveChangesAsync();
        _context.DetachChangeTracker();

        // Fetch and return updated session details
        var session = await GetSessionAsync(sessionId);

        return session;
    }

    public async Task<SessionDetailedResponse> UpdatePlayerStatusAsync(int sessionId, string userId, bool isPlaying, DateTime? leftDateTime, int? lastBuySellId)
    {
        // Find and update the roster entry
        var rosterEntry = await _context.SessionRosters!.FirstOrDefaultAsync(r => r.SessionId == sessionId && r.UserId == userId);
        if (rosterEntry == null)
        {
            throw new KeyNotFoundException($"Player not found in session roster");
        }

        // Update the roster entry
        rosterEntry.IsPlaying = isPlaying;
        rosterEntry.LeftDateTime = leftDateTime;
        rosterEntry.LastBuySellId = lastBuySellId;

        await _context.SaveChangesAsync();
        _context.DetachChangeTracker();

        // Fetch and return updated session details
        var session = await GetSessionAsync(sessionId);

        return session;
    }

    public async Task<SessionDetailedResponse> AddOrUpdatePlayerToRosterAsync(int sessionId, string userId, TeamAssignment teamAssignment, PositionPreference positionPreference, int? lastBuySellId)
    {
        // Check if player is already in the roster
        var existingRosterEntry = await _context.SessionRosters!.FirstOrDefaultAsync(sr => sr.SessionId == sessionId && sr.UserId == userId);
        if (existingRosterEntry != null)
        {
            // Update existing entry
            existingRosterEntry.TeamAssignment = teamAssignment;
            existingRosterEntry.IsPlaying = true;
            existingRosterEntry.IsRegular = false;
            existingRosterEntry.Position = positionPreference;
            existingRosterEntry.JoinedDateTime = DateTime.UtcNow;
            existingRosterEntry.LastBuySellId = lastBuySellId;
            existingRosterEntry.LeftDateTime = null;

            await _context.SaveChangesAsync();
        }
        else
        {
            // Add the player to the roster
            var newRosterEntry = new SessionRoster
            {
                SessionId = sessionId,
                UserId = userId,
                TeamAssignment = teamAssignment,
                IsPlaying = true,
                IsRegular = false,
                Position = positionPreference,
                JoinedDateTime = DateTime.UtcNow,
                LastBuySellId = lastBuySellId,
                LeftDateTime = null
            };

            await _context.SessionRosters.AddAsync(newRosterEntry);
            await _context.SaveChangesAsync();
        }

        _context.DetachChangeTracker();

        // Return the updated session details
        return await GetSessionAsync(sessionId);
    }

    public async Task<SessionDetailedResponse> DeletePlayerFromRosterAsync(int sessionId, string userId)
    {
        // Find and update the roster entry
        var rosterEntry = await _context.SessionRosters.FirstOrDefaultAsync(sr => sr.SessionId == sessionId && sr.UserId == userId);
        if (rosterEntry == null)
        {
            throw new KeyNotFoundException($"Player not found in session roster");
        }

        _context.SessionRosters.Remove(rosterEntry);
        await _context.SaveChangesAsync();

        // Fetch and return updated session details
        var session = await GetSessionAsync(sessionId);

        return session;
    }

    public async Task<IEnumerable<SessionBasicResponse>> GetBasicSessionsAsync()
    {
        return await _context.Sessions
            .Select(s => new SessionBasicResponse
            {
                SessionId = s.SessionId,
                CreateDateTime = s.CreateDateTime,
                UpdateDateTime = s.UpdateDateTime,
                Note = s.Note,
                SessionDate = s.SessionDate,
                RegularSetId = s.RegularSetId,
                BuyDayMinimum = s.BuyDayMinimum,
                Cost = s.Cost != 0 ? s.Cost : _cost
            })
            .OrderByDescending(s => s.SessionDate).ToListAsync();
    }

    public async Task<IEnumerable<SessionDetailedResponse>> GetDetailedSessionsAsync()
    {
        var sessions = await _context.Sessions
            .Include(s => s.BuySells)
                .ThenInclude(b => b.Buyer)
            .Include(s => s.BuySells)
                .ThenInclude(b => b.Seller)
            .Include(s => s.ActivityLogs)
                .ThenInclude(a => a.User)
            .Include(s => s.RegularSet)
                .ThenInclude(r => r.Regulars)
                    .ThenInclude(reg => reg.User)
            // Views are already denormalized, so include directly
            .Include(s => s.CurrentSessionRoster.OrderByDescending(r => r.IsRegular).ThenByDescending(r => r.Position).ThenBy(r => r.JoinedDateTime).ThenBy(r => r.FirstName))
            .Include(s => s.BuyingQueues.OrderBy(q => q.BuySellId))
            .AsSplitQuery() // Added this as without it, it's very slow
            .OrderByDescending(s => s.SessionDate).ToListAsync();

        return sessions.Select(s => MapToDetailedResponse(s, _cost));
    }

    public async Task<SessionDetailedResponse> GetSessionAsync(int sessionId)
    {
        var session = await _context.Sessions
            .Where(s => s.SessionId == sessionId)
            .Include(s => s.BuySells)
                .ThenInclude(b => b.Buyer)
                .ThenInclude(u => u.PaymentMethods)
            .Include(s => s.BuySells)
                .ThenInclude(b => b.Seller)
                .ThenInclude(u => u.PaymentMethods)
            .Include(s => s.ActivityLogs)
                .ThenInclude(a => a.User)
            .Include(s => s.RegularSet)
                .ThenInclude(r => r.Regulars)
                    .ThenInclude(reg => reg.User)
            // Views are already denormalized, so include directly
            .Include(s => s.CurrentSessionRoster.OrderByDescending(r => r.IsRegular).ThenByDescending(r => r.Position).ThenBy(r => r.JoinedDateTime).ThenBy(r => r.FirstName))
            .Include(s => s.BuyingQueues
                .OrderBy(q => q.BuySellId))
                .ThenInclude(q => q.Buyer)
                .ThenInclude(b => b.PaymentMethods)
            .Include(s => s.BuyingQueues)
                .ThenInclude(q => q.Seller)
                .ThenInclude(s => s.PaymentMethods)
             .AsSplitQuery() // Added this as without it, it's very slow
            .FirstOrDefaultAsync();

        return session != null ? MapToDetailedResponse(session, _cost) : null;
    }

    private static SessionDetailedResponse MapToDetailedResponse(Session session, decimal cost)
    {
        if (session == null) return null;

        return new SessionDetailedResponse
        {
            SessionId = session.SessionId,
            CreateDateTime = session.CreateDateTime,
            UpdateDateTime = session.UpdateDateTime,
            Note = session.Note,
            SessionDate = session.SessionDate,
            RegularSetId = session.RegularSetId,
            BuyDayMinimum = session.BuyDayMinimum,
            Cost = session.Cost != 0 ? session.Cost : cost,
            BuySells = MapBuySells(session.BuySells),
            ActivityLogs = MapActivityLogs(session.ActivityLogs),
            RegularSet = MapRegularSet(session.RegularSet),
            CurrentRosters = MapCurrentRoster(session.CurrentSessionRoster),
            BuyingQueues = MapBuyingQueue(session.BuyingQueues)
        };
    }

    private static List<RosterPlayer> MapCurrentRoster(ICollection<CurrentSessionRoster> currentSessionRoster)
    {
        if (currentSessionRoster == null) return new List<RosterPlayer>();

        return currentSessionRoster.Select(r => new RosterPlayer
        {
            SessionRosterId = r.SessionRosterId,
            SessionId = r.SessionId,
            UserId = r.UserId,
            Email = r.Email,
            FirstName = r.FirstName,
            LastName = r.LastName,
            TeamAssignment = (TeamAssignment) r.TeamAssignment,
            IsPlaying = r.IsPlaying,
            IsRegular = r.IsRegular,
            PlayerStatus = ParsePlayerStatus(r.PlayerStatus),
            Rating = r.GetSecureRating(),
            Preferred = r.Preferred,
            PreferredPlus = r.PreferredPlus,
            LastBuySellId = r.LastBuySellId,
            Position = (PositionPreference) r.Position,
            CurrentPosition = r.CurrentPosition,
            JoinedDateTime = r.JoinedDateTime,
            PhotoUrl = r.PhotoUrl
        }).ToList();
    }

    private static PlayerStatus ParsePlayerStatus(string status) => status switch
    {
        "Regular" => PlayerStatus.Regular,
        "Substitute" => PlayerStatus.Substitute,
        "Not Playing" => PlayerStatus.NotPlaying,
        "In Queue" => PlayerStatus.InQueue,
        _ => throw new ArgumentException($"Invalid player status: {status}")
    };

    private static List<BuyingQueueItem> MapBuyingQueue(ICollection<BuyingQueue> queue)
    {
        if (queue == null) return new List<BuyingQueueItem>();

        return queue.Select(q => new BuyingQueueItem
        {
            BuySellId = q.BuySellId,
            SessionId = q.SessionId,
            BuyerUserId = q.BuyerUserId,
            BuyerName = q.BuyerName,
            SellerUserId = q.SellerUserId,
            SellerName = q.SellerName,
            TeamAssignment = q.TeamAssignment,
            TransactionStatus = q.TransactionStatus,
            QueueStatus = q.QueueStatus,
            PaymentSent = q.PaymentSent,
            PaymentReceived = q.PaymentReceived,
            BuyerNote = q.BuyerNote,
            SellerNote = q.SellerNote,
            BuyerNoteFlagged = q.BuyerNoteFlagged,
            SellerNoteFlagged = q.SellerNoteFlagged,
            Buyer = MapToUserDetailedResponse(q.Buyer),
            Seller = MapToUserDetailedResponse(q.Seller)
        }).ToList();
    }

    private static List<BuySellResponse> MapBuySells(ICollection<BuySell> buySells)
    {
        if (buySells == null) return new List<BuySellResponse>();

        return buySells.Select(b => new BuySellResponse
        {
            SessionId = b.SessionId,
            SessionDate = b.Session.SessionDate,
            BuySellId = b.BuySellId,
            BuyerUserId = b.BuyerUserId,
            SellerUserId = b.SellerUserId,
            SellerNote = b.SellerNote,
            BuyerNote = b.BuyerNote,
            PaymentSent = b.PaymentSent,
            PaymentReceived = b.PaymentReceived,
            CreateDateTime = b.CreateDateTime,
            TeamAssignment = b.TeamAssignment,
            UpdateDateTime = b.UpdateDateTime,
            Price = b.Price ?? 0m,
            CreateByUserId = b.CreateByUserId,
            UpdateByUserId = b.UpdateByUserId,
            PaymentMethod = b.PaymentMethod.HasValue ? b.PaymentMethod.Value : PaymentMethodType.Unknown,
            TransactionStatus = b.TransactionStatus,
            SellerNoteFlagged = b.SellerNoteFlagged,
            BuyerNoteFlagged = b.BuyerNoteFlagged,
            Buyer = MapToUserDetailedResponse(b.Buyer),
            Seller = MapToUserDetailedResponse(b.Seller)
        }).OrderBy(b => b.BuySellId).ToList();
    }

    private static List<ActivityLogResponse> MapActivityLogs(ICollection<ActivityLog> activityLogs)
    {
        if (activityLogs == null) return new List<ActivityLogResponse>();

        return activityLogs.Select(a => new ActivityLogResponse
        {
            ActivityLogId = a.ActivityLogId,
            UserId = a.UserId,
            CreateDateTime = a.CreateDateTime,
            Activity = a.Activity,
            User = MapToUserDetailedResponse(a.User)
        }).OrderByDescending(a => a.CreateDateTime).ToList();
    }

    private static RegularSetResponse MapRegularSet(RegularSet regularSet)
    {
        if (regularSet == null) return null;

        return new RegularSetResponse
        {
            RegularSetId = regularSet.RegularSetId,
            Description = regularSet.Description,
            DayOfWeek = regularSet.DayOfWeek,
            Archived = regularSet.Archived,
            CreateDateTime = regularSet.CreateDateTime,
            Regulars = MapRegulars(regularSet.Regulars)
        };
    }

    private static List<RegularResponse> MapRegulars(ICollection<Regular> regulars)
    {
        if (regulars == null) return new List<RegularResponse>();

        return regulars.Select(r => new RegularResponse
        {
            RegularSetId = r.RegularSetId,
            UserId = r.UserId,
            TeamAssignment = r.TeamAssignment,
            PositionPreference = r.PositionPreference,
            User = MapToUserDetailedResponse(r.User)
        }).ToList();
    }

    private static UserDetailedResponse? MapToUserDetailedResponse(AspNetUser? user)
    {
        if (user == null) return null;

        return new UserDetailedResponse
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Preferred = user.Preferred,
            PreferredPlus = user.PreferredPlus,
            Active = user.Active,
            Rating = user.GetSecureRating(),
            LockerRoom13 = user.LockerRoom13,
            EmergencyName = user.EmergencyName,
            EmergencyPhone = user.EmergencyPhone,
            JerseyNumber = user.JerseyNumber,
            NotificationPreference = user.NotificationPreference,
            PositionPreference = user.PositionPreference,
            PhotoUrl = user.PhotoUrl,
            Shoots = user.Shoots,
            DateCreated = user.DateCreated,
            Roles = user.Roles.ToRoleNames(),
            PaymentMethods = user.PaymentMethods.Select(pm => new UserPaymentMethodResponse
            {
                UserPaymentMethodId = pm.UserPaymentMethodId,
                MethodType = pm.MethodType,
                Identifier = pm.Identifier,
                PreferenceOrder = pm.PreferenceOrder,
                IsActive = pm.IsActive,
            }).ToList()
        };
    }

    public async Task<bool> DeleteSessionAsync(int sessionId)
    {
        // Get the execution strategy
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Verify session exists before attempting deletion
                var sessionExists = await _context.Sessions!
                    .AnyAsync(s => s.SessionId == sessionId);

                if (!sessionExists)
                {
                    throw new KeyNotFoundException($"Session not found with Id: {sessionId}");
                }

                // Delete in order of dependency (child tables first)
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM ActivityLogs WHERE SessionId = {0}", sessionId);

                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM BuySells WHERE SessionId = {0}", sessionId);

                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM SessionRosters WHERE SessionId = {0}", sessionId);

                // Finally delete the session itself
                var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM Sessions WHERE SessionId = {0}", sessionId);

                await transaction.CommitAsync();
                return rowsAffected > 0;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}
