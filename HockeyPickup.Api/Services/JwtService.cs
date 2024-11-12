using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HockeyPickup.Api.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly string? _secretKey;
    private readonly string? _issuer;
    private readonly string? _audience;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
        _secretKey = _configuration["JwtSecretKey"];
        _issuer = _configuration["JwtIssuer"];
        _audience = _configuration["JwtAudience"];
    }

    public (string token, DateTime expiration) GenerateToken(string userId, string username, IEnumerable<string> roles)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var expiration = DateTime.UtcNow.AddMonths(1);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
        };

        // Add role claims
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiration,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiration);
    }

    public bool ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public ClaimsPrincipal GetPrincipalFromToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = false // We set this to false to allow token renewal
        };

        return tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
    }

    public DateTime GetTokenExpiration(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        return jwtToken.ValidTo;
    }
}

public class TokenRenewalMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenRenewalMiddleware> _logger;
    private const int RenewalThresholdDays = 7; // Renew if token expires within 7 days

    public TokenRenewalMiddleware(RequestDelegate next, ILogger<TokenRenewalMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IJwtService jwtService)
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                var principal = jwtService.GetPrincipalFromToken(token);
                var expirationClaim = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);
                if (expirationClaim != null)
                {
                    var expiration = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expirationClaim.Value));
                    var timeUntilExpiration = expiration.UtcDateTime - DateTime.UtcNow;
                    if (timeUntilExpiration.TotalDays < RenewalThresholdDays)
                    {
                        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        var username = principal.FindFirst(ClaimTypes.Name)?.Value;

                        // Get roles from existing token
                        var roles = principal.Claims
                            .Where(c => c.Type == ClaimTypes.Role)
                            .Select(c => c.Value)
                            .ToArray();

                        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(username))
                        {
                            var (newToken, newExpiration) = jwtService.GenerateToken(userId, username, roles);

                            // Add the new token to the response headers
                            context.Response.Headers.Append("X-New-Token", newToken);
                            context.Response.Headers.Append("X-New-Token-Expiration", newExpiration.ToString("O"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing token renewal");
                // Continue with the request even if renewal fails
            }
        }
        await _next(context);
    }
}

// Extension method for cleaner startup configuration
public static class TokenRenewalMiddlewareExtensions
{
    public static IApplicationBuilder UseTokenRenewal(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TokenRenewalMiddleware>();
    }
}
