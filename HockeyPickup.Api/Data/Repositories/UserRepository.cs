using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace HockeyPickup.Api.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly HockeyPickupContext _context;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(HockeyPickupContext context, ILogger<UserRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<UserDetailedResponse>> GetDetailedUsersAsync()
    {
        return await _context.Users
            .Select(u => new UserDetailedResponse
            {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Rating = u.GetSecureRating(),
                Preferred = u.Preferred,
                PreferredPlus = u.PreferredPlus,
                Active = u.Active,
                LockerRoom13 = u.LockerRoom13,
                EmergencyName = u.EmergencyName,
                EmergencyPhone = u.EmergencyPhone,
                MobileLast4 = u.MobileLast4,
                VenmoAccount = u.VenmoAccount,
                PayPalEmail = u.PayPalEmail,
                NotificationPreference = (NotificationPreference) u.NotificationPreference,
                PositionPreference = (PositionPreference) u.PositionPreference,
                PhotoUrl = u.PhotoUrl,
                DateCreated = u.DateCreated,
                Roles = u.Roles.ToRoleNames(),
            })
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();
    }

    public async Task<UserDetailedResponse> GetUserAsync(string userId)
    {
        return await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserDetailedResponse
            {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Preferred = u.Preferred,
                PreferredPlus = u.PreferredPlus,
                Rating = u.GetSecureRating(),
                Active = u.Active,
                LockerRoom13 = u.LockerRoom13,
                EmergencyName = u.EmergencyName,
                EmergencyPhone = u.EmergencyPhone,
                MobileLast4 = u.MobileLast4,
                VenmoAccount = u.VenmoAccount,
                PayPalEmail = u.PayPalEmail,
                NotificationPreference = (NotificationPreference) u.NotificationPreference,
                PositionPreference = (PositionPreference) u.PositionPreference,
                PhotoUrl = u.PhotoUrl,
                DateCreated = u.DateCreated,
                Roles = u.Roles.ToRoleNames(),
            })
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<LockerRoom13Response>> GetLockerRoom13SessionsAsync()
    {
        var currentDate = DateTime.UtcNow;

        var query =
            from session in _context.Sessions
            where session.SessionDate > currentDate
                && !session.Note.Contains("cancelled")
            select new LockerRoom13Response
            {
                SessionId = session.SessionId,
                SessionDate = session.SessionDate,
                LockerRoom13Players = (
                    from user in _context.Users
                    where user.LockerRoom13
                    join roster in _context.SessionRosters
                        on new { UserId = user.Id, session.SessionId }
                        equals new { roster.UserId, roster.SessionId }
                        into userRosters
                    from roster in userRosters.DefaultIfEmpty()
                        // Left join with BuySells to check queue status
                    join buySell in _context.BuySells
                        on new { UserId = user.Id, session.SessionId }
                        equals new { UserId = buySell.BuyerUserId, buySell.SessionId }
                        into buySells
                    from buySell in buySells.DefaultIfEmpty()
                    select new LockerRoom13Players
                    {
                        Id = user.Id,
                        UserName = user.UserName,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Active = user.Active,
                        Preferred = user.Preferred,
                        PreferredPlus = user.PreferredPlus,
                        LockerRoom13 = user.LockerRoom13,
                        PlayerStatus =
                            // If they're a buyer with no seller, they're InQueue
                            (buySell != null && buySell.SellerUserId == null) ?
                                PlayerStatus.InQueue :
                            // Otherwise use roster status
                            roster == null ? PlayerStatus.NotPlaying :
                                roster.IsPlaying ?
                                    (roster.IsRegular ? PlayerStatus.Regular : PlayerStatus.Substitute)
                                    : PlayerStatus.NotPlaying
                    })
                    .OrderBy(p => p.LastName)
                    .ThenBy(p => p.FirstName)
                    .ToList()
            };

        return await query
            .OrderBy(r => r.SessionDate)
            .ToListAsync();
    }

    [ExcludeFromCodeCoverage]
    public async Task<UserStatsResponse?> GetUserStatsAsync(string userId)
    {
        return (await _context.Database
            .SqlQuery<UserStatsResponse>($"EXEC GetUserStats @UserId={userId}")
            .ToListAsync())
            .FirstOrDefault();
    }
}
