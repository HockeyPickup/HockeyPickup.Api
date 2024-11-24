using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace HockeyPickup.Api.Data.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly HockeyPickupContext _context;
    private readonly ILogger<SessionRepository> _logger;

    public SessionRepository(HockeyPickupContext context, ILogger<SessionRepository> logger)
    {
        _context = context;
        _logger = logger;
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
                BuyDayMinimum = s.BuyDayMinimum
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
            .OrderByDescending(s => s.SessionDate).ToListAsync();

        return sessions.Select(MapToDetailedResponse);
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
            .FirstOrDefaultAsync();

        return session != null ? MapToDetailedResponse(session) : null;
    }

    private static SessionDetailedResponse MapToDetailedResponse(Session session)
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
            BuySells = MapBuySells(session.BuySells),
            ActivityLogs = MapActivityLogs(session.ActivityLogs),
            RegularSet = MapRegularSet(session.RegularSet)
        };
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
            Buyer = MapToUserBasicResponse(b.Buyer),
            Seller = MapToUserBasicResponse(b.Seller)
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
            User = MapToUserBasicResponse(a.User)
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
            User = MapToUserBasicResponse(r.User)
        }).ToList();
    }

    private static UserBasicResponse? MapToUserBasicResponse(AspNetUser? user)
    {
        if (user == null) return null;

        return new UserBasicResponse
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Preferred = user.Preferred,
            PreferredPlus = user.PreferredPlus,
            Active = user.Active,
            LockerRoom13 = user.LockerRoom13,
            EmergencyName = user.EmergencyName,
            EmergencyPhone = user.EmergencyPhone,
            MobileLast4 = user.MobileLast4,
            VenmoAccount = user.VenmoAccount,
            PayPalEmail = user.PayPalEmail,
            NotificationPreference = (NotificationPreference) user.NotificationPreference,
            DateCreated = user.DateCreated
        };
    }
}
