#pragma warning disable IDE0057 // Use range operator

using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HockeyPickup.Api.Helpers;

public static class HttpContextExtensions
{
    public static string? GetBearerToken(this HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        return authorization.Substring("Bearer ".Length);
    }

    public static string GetUserId(this IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User ID not found in context");
        }

        return userId;
    }
}

public static class StringExtensions
{
    public static bool IsPasswordComplex(this string password)
    {
        return password.Length >= 8 &&
               password.Any(char.IsUpper) &&
               password.Any(char.IsLower) &&
               password.Any(char.IsDigit) &&
               password.Any(c => !char.IsLetterOrDigit(c));
    }
}

public static class IntExtensions
{
    public static string ParsePositionName(this int position)
    {
        return position switch
        {
            0 => "TBD",
            1 => "Forward",
            2 => "Defense",
            _ => string.Empty,
        };
    }

    public static string ParseTeamName(this int position)
    {
        return position switch
        {
            0 => "TBD",
            1 => "Light",
            2 => "Dark",
            _ => string.Empty,
        };
    }
}

public static class RoleExtensions
{
    public static string[] ToRoleNames(this ICollection<AspNetRole> roles)
    {
        return roles.Where(role => role.Name != null)
                   .Select(role => role.Name!)
                   .ToArray();
    }
}

public static class RatingSecurityExtensions
{
    private static readonly string[] RatingAdminRoles = ["Admin", "SubAdmin"];
    private static readonly AsyncLocal<bool?> _isRatingAdminCache = new();
    private static IHttpContextAccessor? _httpContextAccessor;

    public static void Initialize(IHttpContextAccessor? httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public static decimal GetSecureRating(this AspNetUser? user)
    {
        if (user == null || user.Rating == 0) return 0;
        return GetSecureRatingInternal(user.Rating);
    }

    public static decimal GetSecureRating(this RosterPlayer? player)
    {
        if (player == null || player.Rating == 0) return 0;
        return GetSecureRatingInternal(player.Rating);
    }

    private static decimal GetSecureRatingInternal(decimal rating)
    {
        return IsCurrentUserRatingsAdmin() ? rating : 0;
    }

    private static bool IsCurrentUserRatingsAdmin()
    {
        // Check cached result first
        if (_isRatingAdminCache.Value.HasValue)
        {
            return _isRatingAdminCache.Value.Value;
        }

        var isRatingsAdmin = false;
        var context = _httpContextAccessor?.HttpContext;

        if (context?.User != null)
        {
            var userRoles = context.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            isRatingsAdmin = RatingAdminRoles.Any(role => userRoles.Contains(role));
        }

        _isRatingAdminCache.Value = isRatingsAdmin;
        return isRatingsAdmin;
    }

    public static void ClearCache()
    {
        _isRatingAdminCache.Value = null;
    }
}

public static class DbContextExtensions
{
    public static void DetachChangeTracker(this HockeyPickupContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries())
        {
            entry.State = EntityState.Detached;
        }
    }
}

#pragma warning restore IDE0057 // Use range operator
