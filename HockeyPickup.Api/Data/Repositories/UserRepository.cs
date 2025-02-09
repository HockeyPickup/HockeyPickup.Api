using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
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
                JerseyNumber = u.JerseyNumber,
                NotificationPreference = u.NotificationPreference,
                PositionPreference = u.PositionPreference,
                PhotoUrl = u.PhotoUrl,
                Shoots = u.Shoots,
                DateCreated = u.DateCreated,
                Roles = u.Roles.ToRoleNames(),
                PaymentMethods = u.PaymentMethods
                    .OrderBy(p => p.PreferenceOrder)
                    .Select(p => new UserPaymentMethodResponse
                    {
                        UserPaymentMethodId = p.UserPaymentMethodId,
                        MethodType = p.MethodType,
                        Identifier = p.Identifier,
                        PreferenceOrder = p.PreferenceOrder,
                        IsActive = p.IsActive
                    })
                    .ToList()
            })
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();
    }

    public async Task<UserDetailedResponse> GetUserAsync(string userId)
    {
        // First get basic user info
        var user = await _context.Users
            .AsNoTracking()
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
                JerseyNumber = u.JerseyNumber,
                NotificationPreference = u.NotificationPreference,
                PositionPreference = u.PositionPreference,
                PhotoUrl = u.PhotoUrl,
                Shoots = u.Shoots,
                DateCreated = u.DateCreated,
                Roles = u.Roles.ToRoleNames(),
                PaymentMethods = u.PaymentMethods
                    .OrderBy(p => p.PreferenceOrder)
                    .Select(p => new UserPaymentMethodResponse
                    {
                        UserPaymentMethodId = p.UserPaymentMethodId,
                        MethodType = p.MethodType,
                        Identifier = p.Identifier,
                        PreferenceOrder = p.PreferenceOrder,
                        IsActive = p.IsActive
                    })
                    .ToList(),
                BuyerTransactions = new List<BuySellResponse>(),
                SellerTransactions = new List<BuySellResponse>()
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return null;

        // Then get transactions separately
        var buyerTransactions = await _context.BuySells
            .AsNoTracking()
            .Where(b => b.BuyerUserId == userId)
            .Select(b => new BuySellResponse
            {
                SessionId = b.SessionId,
                SessionDate = b.Session != null ? b.Session.SessionDate : DateTime.MinValue,
                BuySellId = b.BuySellId,
                SellerUserId = b.SellerUserId,
                BuyerUserId = b.BuyerUserId,
                CreateDateTime = b.CreateDateTime,
                UpdateDateTime = b.UpdateDateTime,
                PaymentReceived = b.PaymentReceived,
                PaymentSent = b.PaymentSent,
                TeamAssignment = b.TeamAssignment,
                TransactionStatus = b.TransactionStatus ?? string.Empty,
                Price = b.Price ?? 0m,
            })
            .ToListAsync();

        var sellerTransactions = await _context.BuySells
            .AsNoTracking()
            .Where(b => b.SellerUserId == userId)
            .Select(b => new BuySellResponse
            {
                SessionId = b.SessionId,
                SessionDate = b.Session != null ? b.Session.SessionDate : DateTime.MinValue,
                BuySellId = b.BuySellId,
                SellerUserId = b.SellerUserId,
                BuyerUserId = b.BuyerUserId,
                CreateDateTime = b.CreateDateTime,
                UpdateDateTime = b.UpdateDateTime,
                PaymentReceived = b.PaymentReceived,
                PaymentSent = b.PaymentSent,
                TeamAssignment = b.TeamAssignment,
                TransactionStatus = b.TransactionStatus ?? string.Empty,
                Price = b.Price ?? 0m,
            })
            .ToListAsync();

        user.BuyerTransactions = buyerTransactions.OrderByDescending(b => b.BuySellId).ToList();
        user.SellerTransactions = sellerTransactions.OrderByDescending(b => b.BuySellId).ToList();

        return user!;
    }

    public async Task<IEnumerable<LockerRoom13Response>> GetLockerRoom13SessionsAsync()
    {
        var currentDate = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")
        );

        var sessionsQuery = await _context.Sessions
            .Where(s => s.SessionDate > currentDate && !s.Note.Contains("cancelled"))
            .OrderBy(s => s.SessionDate)
            .ToListAsync();

        var result = new List<LockerRoom13Response>();

        foreach (var session in sessionsQuery)
        {
            var sessionRosters = await _context.SessionRosters
                .Where(r => r.SessionId == session.SessionId)
                .ToListAsync();

            var sessionBuySells = await _context.BuySells
                .Where(b => b.SessionId == session.SessionId &&
                           b.SellerUserId == null &&
                           b.BuyerUserId != null)
                .ToListAsync();

            var lr13Players = await _context.Users
                .Where(u => u.LockerRoom13)
                .Select(user => new LockerRoom13Players
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    Active = user.Active,
                    Preferred = user.Preferred,
                    PreferredPlus = user.PreferredPlus,
                    LockerRoom13 = user.LockerRoom13,
                    PlayerStatus = GetPlayerStatus(user.Id, sessionRosters, sessionBuySells)
                })
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .Distinct()
                .ToListAsync();

            result.Add(new LockerRoom13Response
            {
                SessionId = session.SessionId,
                SessionDate = session.SessionDate,
                LockerRoom13Players = lr13Players
            });
        }

        return result;
    }

    private static PlayerStatus GetPlayerStatus(string userId,
        List<SessionRoster> rosters,
        List<BuySell> buySells)
    {
        if (buySells.Any(b => b.BuyerUserId == userId))
            return PlayerStatus.InQueue;

        var roster = rosters.FirstOrDefault(r => r.UserId == userId);
        if (roster == null)
            return PlayerStatus.NotPlaying;

        return roster.IsPlaying
            ? (roster.IsRegular ? PlayerStatus.Regular : PlayerStatus.Substitute)
            : PlayerStatus.NotPlaying;
    }

    [ExcludeFromCodeCoverage]
    public async Task<UserStatsResponse?> GetUserStatsAsync(string userId)
    {
        return (await _context.Database
            .SqlQuery<UserStatsResponse>($"EXEC GetUserStats @UserId={userId}")
            .ToListAsync())
            .FirstOrDefault();
    }

    public async Task<IEnumerable<UserPaymentMethodResponse>> GetUserPaymentMethodsAsync(string userId)
    {
        return await _context.UserPaymentMethods
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.PreferenceOrder)
            .Select(p => new UserPaymentMethodResponse
            {
                UserPaymentMethodId = p.UserPaymentMethodId,
                MethodType = p.MethodType,
                Identifier = p.Identifier,
                PreferenceOrder = p.PreferenceOrder,
                IsActive = p.IsActive
            })
            .ToListAsync();
    }

    public async Task<UserPaymentMethodResponse?> GetUserPaymentMethodAsync(string userId, int paymentMethodId)
    {
        return await _context.UserPaymentMethods
            .Where(p => p.UserId == userId && p.UserPaymentMethodId == paymentMethodId)
            .Select(p => new UserPaymentMethodResponse
            {
                UserPaymentMethodId = p.UserPaymentMethodId,
                MethodType = p.MethodType,
                Identifier = p.Identifier,
                PreferenceOrder = p.PreferenceOrder,
                IsActive = p.IsActive
            })
            .FirstOrDefaultAsync();
    }

    public async Task<UserPaymentMethodResponse> AddUserPaymentMethodAsync(string userId, UserPaymentMethod paymentMethod)
    {
        paymentMethod.UserId = userId;
        paymentMethod.CreatedAt = DateTime.UtcNow;

        _context.UserPaymentMethods.Add(paymentMethod);
        await _context.SaveChangesAsync();

        return new UserPaymentMethodResponse
        {
            UserPaymentMethodId = paymentMethod.UserPaymentMethodId,
            MethodType = paymentMethod.MethodType,
            Identifier = paymentMethod.Identifier,
            PreferenceOrder = paymentMethod.PreferenceOrder,
            IsActive = paymentMethod.IsActive
        };
    }

    public async Task<UserPaymentMethodResponse?> UpdateUserPaymentMethodAsync(string userId, UserPaymentMethod paymentMethod)
    {
        var existing = await _context.UserPaymentMethods
            .FirstOrDefaultAsync(p => p.UserId == userId && p.UserPaymentMethodId == paymentMethod.UserPaymentMethodId);

        if (existing == null)
            return null;

        existing.MethodType = paymentMethod.MethodType;
        existing.Identifier = paymentMethod.Identifier;
        existing.PreferenceOrder = paymentMethod.PreferenceOrder;
        existing.IsActive = paymentMethod.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new UserPaymentMethodResponse
        {
            UserPaymentMethodId = existing.UserPaymentMethodId,
            MethodType = existing.MethodType,
            Identifier = existing.Identifier,
            PreferenceOrder = existing.PreferenceOrder,
            IsActive = existing.IsActive
        };
    }

    public async Task<bool> DeleteUserPaymentMethodAsync(string userId, int paymentMethodId)
    {
        var paymentMethod = await _context.UserPaymentMethods
            .FirstOrDefaultAsync(p => p.UserId == userId && p.UserPaymentMethodId == paymentMethodId);

        if (paymentMethod == null)
            return false;

        _context.UserPaymentMethods.Remove(paymentMethod);
        await _context.SaveChangesAsync();
        return true;
    }
}
