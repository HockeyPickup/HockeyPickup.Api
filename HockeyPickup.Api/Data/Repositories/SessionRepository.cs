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
            .Where(s => s.Note == null || !EF.Functions.Like(s.Note, "%cancelled%"))
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
            .ToListAsync();
    }

    public async Task<IEnumerable<SessionDetailedResponse>> GetDetailedSessionsAsync()
    {
        var sessions = await _context.Sessions
            .Where(s => s.Note == null || !EF.Functions.Like(s.Note, "%cancelled%"))
            .Include(s => s.BuySells)
                .ThenInclude(b => b.Buyer)
            .Include(s => s.BuySells)
                .ThenInclude(b => b.Seller)
            .Include(s => s.ActivityLogs)
                .ThenInclude(a => a.User)
            .Include(s => s.RegularSet)
                .ThenInclude(r => r.Regulars)
                    .ThenInclude(reg => reg.User)
            .ToListAsync();

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
        return new SessionDetailedResponse
        {
            SessionId = session.SessionId,
            CreateDateTime = session.CreateDateTime,
            UpdateDateTime = session.UpdateDateTime,
            Note = session.Note,
            SessionDate = session.SessionDate,
            RegularSetId = session.RegularSetId,
            BuyDayMinimum = session.BuyDayMinimum,
            BuySells = session.BuySells.Select(b => new BuySellResponse
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
                Buyer = b.Buyer != null ? MapToUserBasicResponse(b.Buyer) : null,
                Seller = b.Seller != null ? MapToUserBasicResponse(b.Seller) : null
            }).ToList(),
            ActivityLogs = session.ActivityLogs.Select(a => new ActivityLogResponse
            {
                ActivityLogId = a.ActivityLogId,
                UserId = a.UserId,
                CreateDateTime = a.CreateDateTime,
                Activity = a.Activity,
                User = a.User != null ? MapToUserBasicResponse(a.User) : null
            }).ToList(),
            RegularSet = session.RegularSet != null ? new RegularSetResponse
            {
                RegularSetId = session.RegularSet.RegularSetId,
                Description = session.RegularSet.Description,
                DayOfWeek = session.RegularSet.DayOfWeek,
                CreateDateTime = session.RegularSet.CreateDateTime,
                Regulars = session.RegularSet.Regulars.Select(r => new RegularResponse
                {
                    RegularSetId = r.RegularSetId,
                    UserId = r.UserId,
                    TeamAssignment = r.TeamAssignment,
                    PositionPreference = r.PositionPreference,
                    User = r.User != null ? MapToUserBasicResponse(r.User) : null
                }).ToList()
            } : null
        };
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
            NotificationPreference = (NotificationPreference) user.NotificationPreference
        };
    }
}
