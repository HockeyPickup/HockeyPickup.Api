#pragma warning disable IDE0057 // Use range operator

using HockeyPickup.Api.Data.Entities;
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
#pragma warning restore IDE0057 // Use range operator
