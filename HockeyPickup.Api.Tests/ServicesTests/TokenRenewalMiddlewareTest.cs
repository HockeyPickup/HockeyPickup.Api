using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using HockeyPickup.Api.Services;
using Microsoft.IdentityModel.Tokens;

namespace HockeyPickup.Api.Tests.ServicesTests;

public class TokenRenewalMiddlewareTests
{
    private readonly Mock<ILogger<TokenRenewalMiddleware>> _mockLogger;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly TokenRenewalMiddleware _middleware;
    private readonly DefaultHttpContext _httpContext;
    private readonly RequestDelegate _next;

    public TokenRenewalMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<TokenRenewalMiddleware>>();
        _mockJwtService = new Mock<IJwtService>();
        _next = (HttpContext context) => Task.CompletedTask;
        _middleware = new TokenRenewalMiddleware(_next, _mockLogger.Object);
        _httpContext = new DefaultHttpContext();
    }

    [Fact]
    public async Task InvokeAsync_NoAuthorizationHeader_SkipsRenewal()
    {
        // Arrange
        _httpContext.Request.Headers.Clear();

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockJwtService.Object);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("X-New-Token");
        _mockJwtService.Verify(x => x.GetPrincipalFromToken(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_TokenNotExpiringSoon_SkipsRenewal()
    {
        // Arrange
        var token = "valid.jwt.token";
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Exp,
                DateTimeOffset.UtcNow.AddDays(20).ToUnixTimeSeconds().ToString()),
            new Claim(ClaimTypes.NameIdentifier, "user-id"),
            new Claim(ClaimTypes.Name, "test@example.com"),
            new Claim(ClaimTypes.Role, "User")
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        _mockJwtService
            .Setup(x => x.GetPrincipalFromToken(token))
            .Returns(principal);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockJwtService.Object);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("X-New-Token");
        _mockJwtService.Verify(x => x.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_TokenExpiringSoon_RenewsToken()
    {
        // Arrange
        var token = "expiring.jwt.token";
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Exp,
                DateTimeOffset.UtcNow.AddDays(5).ToUnixTimeSeconds().ToString()),
            new Claim(ClaimTypes.NameIdentifier, "user-id"),
            new Claim(ClaimTypes.Name, "test@example.com"),
            new Claim(ClaimTypes.Role, "User")
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        _mockJwtService
            .Setup(x => x.GetPrincipalFromToken(token))
            .Returns(principal);

        var newToken = "new.jwt.token";
        var newExpiration = DateTime.UtcNow.AddMonths(1);
        _mockJwtService
            .Setup(x => x.GenerateToken("user-id", "test@example.com", new[] { "User" }))
            .Returns((newToken, newExpiration));

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockJwtService.Object);

        // Assert
        _httpContext.Response.Headers.Should().ContainKey("X-New-Token");
        _httpContext.Response.Headers["X-New-Token"].Should().Equal(newToken);
        _httpContext.Response.Headers["X-New-Token-Expiration"].Should().Equal(newExpiration.ToString("O"));
    }

    [Fact]
    public async Task InvokeAsync_TokenValidationError_ContinuesPipeline()
    {
        // Arrange
        var token = "invalid.jwt.token";
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";

        _mockJwtService
            .Setup(x => x.GetPrincipalFromToken(token))
            .Throws(new SecurityTokenException("Invalid token"));

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockJwtService.Object);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("X-New-Token");
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_MissingRequiredClaims_SkipsRenewal()
    {
        // Arrange
        var token = "valid.jwt.token";
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Exp,
                DateTimeOffset.UtcNow.AddDays(5).ToUnixTimeSeconds().ToString()),
            // Missing NameIdentifier or Name claims
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        _mockJwtService
            .Setup(x => x.GetPrincipalFromToken(token))
            .Returns(principal);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockJwtService.Object);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("X-New-Token");
        _mockJwtService.Verify(x => x.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_MultipleRoles_PreservesAllRoles()
    {
        // Arrange
        var token = "expiring.jwt.token";
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Exp,
                DateTimeOffset.UtcNow.AddDays(5).ToUnixTimeSeconds().ToString()),
            new Claim(ClaimTypes.NameIdentifier, "user-id"),
            new Claim(ClaimTypes.Name, "test@example.com"),
            new Claim(ClaimTypes.Role, "User"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        _mockJwtService
            .Setup(x => x.GetPrincipalFromToken(token))
            .Returns(principal);

        var newToken = "new.jwt.token";
        var newExpiration = DateTime.UtcNow.AddMonths(1);
        _mockJwtService
            .Setup(x => x.GenerateToken("user-id", "test@example.com", new[] { "User", "Admin" }))
            .Returns((newToken, newExpiration));

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockJwtService.Object);

        // Assert
        _httpContext.Response.Headers.Should().ContainKey("X-New-Token");
        _mockJwtService.Verify(x =>
            x.GenerateToken(
                "user-id",
                "test@example.com",
                It.Is<string[]>(roles => roles.Contains("User") && roles.Contains("Admin"))),
            Times.Once);
    }
}
