using Microsoft.Extensions.Caching.Distributed;

namespace HockeyPickup.Api.Services;

public interface ITokenBlacklistService
{
    Task InvalidateTokenAsync(string token);
    Task<bool> IsTokenBlacklistedAsync(string token);
}

public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IDistributedCache _cache;
    private readonly IJwtService _jwtService;

    public TokenBlacklistService(IDistributedCache cache, IJwtService jwtService)
    {
        _cache = cache;
        _jwtService = jwtService;
    }

    public async Task InvalidateTokenAsync(string token)
    {
        // Get the token's expiration from our existing JWT service
        var tokenData = _jwtService.ValidateToken(token);
        var expirationTime = tokenData?.ValidTo ?? DateTime.UtcNow.AddMinutes(15);
        var timeUntilExpiration = expirationTime - DateTime.UtcNow;

        // Add token to blacklist with expiry matching the token's expiry
        await _cache.SetStringAsync($"blacklist_{token}", "invalid", new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.Add(timeUntilExpiration)
            }
        );
    }

    public async Task<bool> IsTokenBlacklistedAsync(string token)
    {
        var blacklistedToken = await _cache.GetStringAsync($"blacklist_{token}");
        return blacklistedToken != null;
    }
}
