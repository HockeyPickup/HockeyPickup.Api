using Microsoft.Extensions.Caching.Distributed;
using Moq;
using FluentAssertions;
using System.Text;
using HockeyPickup.Api.Services;

namespace HockeyPickup.Api.Tests.ServicesTests;

public class TokenBlacklistServiceTest
{
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly TokenBlacklistService _service;

    public TokenBlacklistServiceTest()
    {
        _mockCache = new Mock<IDistributedCache>();
        _mockJwtService = new Mock<IJwtService>();
        _service = new TokenBlacklistService(_mockCache.Object, _mockJwtService.Object);
    }

    [Fact]
    public async Task InvalidateTokenAsync_ValidToken_AddsToBlacklist()
    {
        // Arrange
        var token = "valid.jwt.token";
        var validTo = DateTime.UtcNow.AddHours(1);
        var tokenInfo = new TokenInfo
        {
            IsValid = true,
            ValidTo = validTo
        };

        _mockJwtService
            .Setup(x => x.ValidateToken(token))
            .Returns(tokenInfo);

        // Act
        await _service.InvalidateTokenAsync(token);

        // Assert
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(s => s == $"blacklist_{token}"),
            It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "invalid"),
            It.Is<DistributedCacheEntryOptions>(opts =>
                opts.AbsoluteExpiration.HasValue &&
                opts.AbsoluteExpiration.Value.DateTime.Ticks.IsCloseTo(validTo.Ticks, TimeSpan.FromSeconds(1).Ticks)),
            default),
            Times.Once);
    }

    [Fact]
    public async Task InvalidateTokenAsync_InvalidToken_UsesDefaultExpiration()
    {
        // Arrange
        var token = "invalid.jwt.token";
        _mockJwtService
            .Setup(x => x.ValidateToken(token))
            .Returns((TokenInfo) null!);

        var expectedExpiration = DateTime.UtcNow.AddMinutes(15);

        // Act
        await _service.InvalidateTokenAsync(token);

        // Assert
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(s => s == $"blacklist_{token}"),
            It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "invalid"),
            It.Is<DistributedCacheEntryOptions>(opts =>
                opts.AbsoluteExpiration.HasValue &&
                opts.AbsoluteExpiration.Value.DateTime.Ticks.IsCloseTo(expectedExpiration.Ticks, TimeSpan.FromSeconds(1).Ticks)),
            default),
            Times.Once);
    }

    [Fact]
    public async Task InvalidateTokenAsync_NullToken_UsesDefaultExpiration()
    {
        // Arrange
        string token = null;
        var expectedExpiration = DateTime.UtcNow.AddMinutes(15);

        // Act
        await _service.InvalidateTokenAsync(token);

        // Assert
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(s => s == "blacklist_"),
            It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "invalid"),
            It.Is<DistributedCacheEntryOptions>(opts =>
                opts.AbsoluteExpiration.HasValue &&
                opts.AbsoluteExpiration.Value.DateTime.Ticks.IsCloseTo(expectedExpiration.Ticks, TimeSpan.FromSeconds(1).Ticks)),
            default),
            Times.Once);
    }

    [Fact]
    public async Task IsTokenBlacklistedAsync_BlacklistedToken_ReturnsTrue()
    {
        // Arrange
        var token = "blacklisted.jwt.token";
        _mockCache
            .Setup(x => x.GetAsync($"blacklist_{token}", default))
            .ReturnsAsync(Encoding.UTF8.GetBytes("invalid"));

        // Act
        var result = await _service.IsTokenBlacklistedAsync(token);

        // Assert
        result.Should().BeTrue();
        _mockCache.Verify(x => x.GetAsync($"blacklist_{token}", default), Times.Once);
    }

    [Fact]
    public async Task IsTokenBlacklistedAsync_NonBlacklistedToken_ReturnsFalse()
    {
        // Arrange
        var token = "valid.jwt.token";
        _mockCache
            .Setup(x => x.GetAsync($"blacklist_{token}", default))
            .ReturnsAsync((byte[]) null);

        // Act
        var result = await _service.IsTokenBlacklistedAsync(token);

        // Assert
        result.Should().BeFalse();
        _mockCache.Verify(x => x.GetAsync($"blacklist_{token}", default), Times.Once);
    }

    [Fact]
    public async Task IsTokenBlacklistedAsync_NullToken_ReturnsFalse()
    {
        // Arrange
        string token = null;
        _mockCache
            .Setup(x => x.GetAsync("blacklist_", default))
            .ReturnsAsync((byte[]) null);

        // Act
        var result = await _service.IsTokenBlacklistedAsync(token);

        // Assert
        result.Should().BeFalse();
        _mockCache.Verify(x => x.GetAsync("blacklist_", default), Times.Once);
    }
}

public static class TimeSpanExtensions
{
    public static bool IsCloseTo(this long value, long target, long allowedDifference)
    {
        return Math.Abs(value - target) <= allowedDifference;
    }
}
