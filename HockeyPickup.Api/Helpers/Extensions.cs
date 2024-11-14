#pragma warning disable IDE0057 // Use range operator

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
#pragma warning restore IDE0057 // Use range operator
