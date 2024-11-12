using System.Security.Claims;

namespace HockeyPickup.Api.Services;

public interface IJwtService
{
    (string token, DateTime expiration) GenerateToken(string userId, string username, IEnumerable<string> roles);
    bool ValidateToken(string token);
    ClaimsPrincipal GetPrincipalFromToken(string token);
    DateTime GetTokenExpiration(string token);
}
