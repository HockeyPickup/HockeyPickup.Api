using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace HockeyPickup.Api.Data.Repositories;

public class RegularRepository : IRegularRepository
{
    private readonly HockeyPickupContext _context;

    public RegularRepository(HockeyPickupContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<RegularSetDetailedResponse>> GetRegularSetsAsync()
    {
        var regularSets = await _context.RegularSets!
            .Include(rs => rs.Regulars)
                .ThenInclude(r => r.User)
            .OrderByDescending(rs => rs.CreateDateTime)
            .AsSplitQuery()
            .ToListAsync();

        return regularSets.Select(rs => MapToDetailedResponse(rs)).ToList();
    }

    public async Task<RegularSetDetailedResponse?> GetRegularSetAsync(int regularSetId)
    {
        var regularSet = await _context.RegularSets!
            .Include(rs => rs.Regulars)
                .ThenInclude(r => r.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(rs => rs.RegularSetId == regularSetId);

        return regularSet != null ? MapToDetailedResponse(regularSet) : null;
    }

    private static RegularSetDetailedResponse MapToDetailedResponse(RegularSet regularSet)
    {
        if (regularSet == null) return null;

        return new RegularSetDetailedResponse
        {
            RegularSetId = regularSet.RegularSetId,
            Description = regularSet.Description,
            DayOfWeek = regularSet.DayOfWeek,
            CreateDateTime = regularSet.CreateDateTime,
            Regulars = MapRegulars(regularSet.Regulars)
        };
    }

    private static List<RegularDetailedResponse> MapRegulars(ICollection<Regular> regulars)
    {
        if (regulars == null) return new List<RegularDetailedResponse>();

        return regulars.Select(r => new RegularDetailedResponse
        {
            RegularSetId = r.RegularSetId,
            UserId = r.UserId,
            TeamAssignment = r.TeamAssignment,
            PositionPreference = r.PositionPreference,
            User = MapUserResponse(r.User)
        }).ToList();
    }

    private static UserDetailedResponse MapUserResponse(AspNetUser user)
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
            Rating = user.Rating, // Don't use GetSecureRating in tests
            LockerRoom13 = user.LockerRoom13,
            EmergencyName = user.EmergencyName,
            EmergencyPhone = user.EmergencyPhone,
            MobileLast4 = user.MobileLast4,
            VenmoAccount = user.VenmoAccount,
            PayPalEmail = user.PayPalEmail,
            NotificationPreference = (NotificationPreference) user.NotificationPreference,
            DateCreated = user.DateCreated,
            Roles = []
        };
    }
}
