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
        var existingSession = await _context.Sessions!
            .FirstOrDefaultAsync(s => s.SessionId == session.SessionId);

        if (existingSession == null)
        {
            throw new KeyNotFoundException($"Session not found with ID: {session.SessionId}");
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

    public async Task<SessionDetailedResponse> UpdatePlayerPositionAsync(int sessionId, string userId, int position)
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

    public async Task<SessionDetailedResponse> UpdatePlayerTeamAsync(int sessionId, string userId, int team)
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
            .Include(s => s.CurrentRosters.OrderByDescending(r => r.IsRegular).ThenByDescending(r => r.Position).ThenBy(r => r.JoinedDateTime).ThenBy(r => r.FirstName))
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
            .Include(s => s.BuySells)
                .ThenInclude(b => b.Seller)
            .Include(s => s.ActivityLogs)
                .ThenInclude(a => a.User)
            .Include(s => s.RegularSet)
                .ThenInclude(r => r.Regulars)
                    .ThenInclude(reg => reg.User)
            // Views are already denormalized, so include directly
            .Include(s => s.CurrentRosters.OrderByDescending(r => r.IsRegular).ThenByDescending(r => r.Position).ThenBy(r => r.JoinedDateTime).ThenBy(r => r.FirstName))
            .Include(s => s.BuyingQueues.OrderBy(q => q.BuySellId))
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
            CurrentRosters = MapCurrentRoster(session.CurrentRosters),
            BuyingQueues = MapBuyingQueue(session.BuyingQueues)
        };
    }

    private static List<Models.Responses.RosterPlayer> MapCurrentRoster(ICollection<Entities.RosterPlayer> roster)
    {
        if (roster == null) return new List<Models.Responses.RosterPlayer>();

        return roster.Select(r => new Models.Responses.RosterPlayer
        {
            SessionRosterId = r.SessionRosterId,
            SessionId = r.SessionId,
            UserId = r.UserId,
            Email = r.Email,
            FirstName = r.FirstName,
            LastName = r.LastName,
            TeamAssignment = r.TeamAssignment,
            IsPlaying = r.IsPlaying,
            IsRegular = r.IsRegular,
            PlayerStatus = ParsePlayerStatus(r.PlayerStatus),
            Rating = r.GetSecureRating(),
            Preferred = r.Preferred,
            PreferredPlus = r.PreferredPlus,
            LastBuySellId = r.LastBuySellId,
            Position = r.Position,
            CurrentPosition = r.CurrentPosition,
            JoinedDateTime = r.JoinedDateTime
        }).ToList();
    }

    private static PlayerStatus ParsePlayerStatus(string status) => status switch
    {
        "Regular" => PlayerStatus.Regular,
        "Substitute" => PlayerStatus.Substitute,
        "Not Playing" => PlayerStatus.NotPlaying,
        _ => throw new ArgumentException($"Invalid player status: {status}")
    };

    private static List<BuyingQueueItem> MapBuyingQueue(ICollection<Entities.BuyingQueue> queue)
    {
        if (queue == null) return new List<BuyingQueueItem>();

        return queue.Select(q => new BuyingQueueItem
        {
            BuySellId = q.BuySellId,
            SessionId = q.SessionId,
            BuyerName = q.BuyerName,
            SellerName = q.SellerName,
            TeamAssignment = q.TeamAssignment,
            TransactionStatus = q.TransactionStatus,
            QueueStatus = q.QueueStatus,
            PaymentSent = q.PaymentSent,
            PaymentReceived = q.PaymentReceived,
            BuyerNote = q.BuyerNote,
            SellerNote = q.SellerNote
        }).ToList();
    }

    private static List<BuySellResponse> MapBuySells(ICollection<BuySell> buySells)
    {
        if (buySells == null) return new List<BuySellResponse>();

        return buySells.Select(b => new BuySellResponse
        {
            BuySellId = b.BuySellId,
            BuyerUserId = b.BuyerUserId,
            SellerUserId = b.SellerUserId,
            SellerNote = b.SellerNote,
            BuyerNote = b.BuyerNote,
            PaymentSent = b.PaymentSent,
            PaymentReceived = b.PaymentReceived,
            CreateDateTime = b.CreateDateTime,
            TeamAssignment = b.TeamAssignment,
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
            MobileLast4 = user.MobileLast4,
            VenmoAccount = user.VenmoAccount,
            PayPalEmail = user.PayPalEmail,
            NotificationPreference = (NotificationPreference) user.NotificationPreference,
            DateCreated = user.DateCreated,
            Roles = user.Roles.ToRoleNames(),
        };
    }
}
