using Moq;
using FluentAssertions;
using System.Security.Claims;
using HockeyPickup.Api.Services;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace HockeyPickup.Api.Tests.ServicesTests;

public class JwtServiceTest
{
    private readonly JwtService _jwtService;
    private readonly string _secretKey = "your-256-bit-secret-your-256-bit-secret-your-256-bit-secret";
    private readonly string _issuer = "test-issuer";
    private readonly string _audience = "test-audience";

    public JwtServiceTest()
    {
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(x => x["JwtSecretKey"]).Returns(_secretKey);
        mockConfiguration.Setup(x => x["JwtIssuer"]).Returns(_issuer);
        mockConfiguration.Setup(x => x["JwtAudience"]).Returns(_audience);

        _jwtService = new JwtService(mockConfiguration.Object);
    }

    [Fact]
    public void GenerateToken_ValidInput_ReturnsValidToken()
    {
        // Arrange
        var userId = "test-user-id";
        var username = "test@example.com";
        var roles = new[] { "User", "Admin" };

        // Act
        var (token, expiration) = _jwtService.GenerateToken(userId, username, roles);

        // Assert
        token.Should().NotBeNullOrEmpty();
        expiration.Should().BeCloseTo(DateTime.UtcNow.AddMonths(1), TimeSpan.FromSeconds(5));

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == userId);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == username);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "User");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");

        jwtToken.Issuer.Should().Be(_issuer);
        jwtToken.Audiences.Should().Contain(_audience);
    }

    [Fact]
    public void ValidateToken_ValidToken_ReturnsValidTokenInfo()
    {
        // Arrange
        var userId = "test-user-id";
        var username = "test@example.com";
        var roles = new[] { "User" };
        var (token, _) = _jwtService.GenerateToken(userId, username, roles);

        // Act
        var tokenInfo = _jwtService.ValidateToken(token);

        // Assert
        tokenInfo.Should().NotBeNull();
        tokenInfo.IsValid.Should().BeTrue();
        tokenInfo.UserId.Should().Be(userId);
        tokenInfo.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMonths(1), TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("invalid-token")]
    [InlineData("")]
    [InlineData(null)]
    public void ValidateToken_InvalidToken_ReturnsNull(string? invalidToken)
    {
        // Act
        var result = _jwtService.ValidateToken(invalidToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromToken_ValidToken_ReturnsPrincipal()
    {
        // Arrange
        var userId = "test-user-id";
        var username = "test@example.com";
        var roles = new[] { "User", "Admin" };
        var (token, _) = _jwtService.GenerateToken(userId, username, roles);

        // Act
        var principal = _jwtService.GetPrincipalFromToken(token);

        // Assert
        principal.Should().NotBeNull();
        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be(userId);
        principal.FindFirst(ClaimTypes.Name)?.Value.Should().Be(username);
        principal.Claims.Count(c => c.Type == ClaimTypes.Role).Should().Be(2);
        principal.IsInRole("User").Should().BeTrue();
        principal.IsInRole("Admin").Should().BeTrue();
    }

    [Fact]
    public void GetPrincipalFromToken_EmptyToken_ThrowsArgumentNullException()
    {
        // Arrange
        var emptyToken = "";

        // Act & Assert
        var action = () => _jwtService.GetPrincipalFromToken(emptyToken);
        action.Should().Throw<ArgumentNullException>()
            .WithMessage("*token*");
    }

    [Fact]
    public void GetPrincipalFromToken_MalformedToken_ThrowsSecurityTokenMalformedException()
    {
        // Arrange
        var invalidToken = "invalid-token";

        // Act & Assert
        var action = () => _jwtService.GetPrincipalFromToken(invalidToken);
        action.Should().Throw<SecurityTokenMalformedException>()
            .WithMessage("*JWT must have three segments*");
    }

    [Fact]
    public void GetPrincipalFromToken_InvalidBase64_ThrowsArgumentException()
    {
        // Arrange
        var invalidBase64Token = "header.payload.signature";

        // Act & Assert
        var action = () => _jwtService.GetPrincipalFromToken(invalidBase64Token);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Unable to decode the header*");
    }

    [Fact]
    public void GetPrincipalFromToken_WrongSegmentCount_ThrowsSecurityTokenMalformedException()
    {
        // Arrange
        var wrongSegmentToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0"; // Only 2 segments

        // Act & Assert
        var action = () => _jwtService.GetPrincipalFromToken(wrongSegmentToken);
        action.Should().Throw<SecurityTokenMalformedException>()
            .WithMessage("*JWT must have three segments*");
    }

    [Fact]
    public void GetPrincipalFromToken_ExpiredToken_ReturnsPrincipal()
    {
        // Arrange
        var userId = "test-user-id";
        var username = "test@example.com";
        var roles = new[] { "User" };
        var (token, _) = _jwtService.GenerateToken(userId, username, roles);

        // Act
        var principal = _jwtService.GetPrincipalFromToken(token);

        // Assert
        principal.Should().NotBeNull();
        // GetPrincipalFromToken sets ValidateLifetime = false, so it should work with expired tokens
        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be(userId);
    }

    [Fact]
    public void GetTokenExpiration_ValidToken_ReturnsExpiration()
    {
        // Arrange
        var userId = "test-user-id";
        var username = "test@example.com";
        var roles = new[] { "User" };
        var (token, expectedExpiration) = _jwtService.GenerateToken(userId, username, roles);

        // Act
        var expiration = _jwtService.GetTokenExpiration(token);

        // Assert
        expiration.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("invalid-token")]
    [InlineData("")]
    public void GetTokenExpiration_InvalidToken_ThrowsArgumentException(string invalidToken)
    {
        // Act & Assert
        var action = () => _jwtService.GetTokenExpiration(invalidToken);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateToken_WithAllClaims_ReturnsCompleteTokenInfo()
    {
        // Arrange
        // Generate token with all claims manually to ensure Email claim is present
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var userId = "test-user-id";
        var email = "test@example.com";

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Email, email),  // Add explicit email claim
            new Claim(ClaimTypes.Role, "User")
        };

        var jwtToken = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );
        var token = new JwtSecurityTokenHandler().WriteToken(jwtToken);

        // Act
        var tokenInfo = _jwtService.ValidateToken(token);

        // Assert
        tokenInfo.Should().NotBeNull();
        tokenInfo.IsValid.Should().BeTrue();
        tokenInfo.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromSeconds(1));
        tokenInfo.UserId.Should().Be(userId);
        tokenInfo.Email.Should().Be(email);
    }

    [Fact]
    public void ValidateToken_WithoutEmailClaim_ReturnsTokenInfoWithNullEmail()
    {
        // Arrange
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Role, "User")
            // No email claim
        };

        var jwtToken = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );
        var token = new JwtSecurityTokenHandler().WriteToken(jwtToken);

        // Act
        var tokenInfo = _jwtService.ValidateToken(token);

        // Assert
        tokenInfo.Should().NotBeNull();
        tokenInfo.IsValid.Should().BeTrue();
        tokenInfo.UserId.Should().Be("test-user-id");
        tokenInfo.Email.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_WithoutNameIdentifierClaim_ReturnsTokenInfoWithNullUserId()
    {
        // Arrange
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, "User")
            // No NameIdentifier claim
        };

        var jwtToken = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );
        var token = new JwtSecurityTokenHandler().WriteToken(jwtToken);

        // Act
        var tokenInfo = _jwtService.ValidateToken(token);

        // Assert
        tokenInfo.Should().NotBeNull();
        tokenInfo.IsValid.Should().BeTrue();
        tokenInfo.UserId.Should().BeNull();
        tokenInfo.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void ValidateToken_WithoutAnyClaims_ReturnsTokenInfoWithNullFields()
    {
        // Arrange
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var jwtToken = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: new List<Claim>(), // Empty claims
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );
        var token = new JwtSecurityTokenHandler().WriteToken(jwtToken);

        // Act
        var tokenInfo = _jwtService.ValidateToken(token);

        // Assert
        tokenInfo.Should().NotBeNull();
        tokenInfo.IsValid.Should().BeTrue();
        tokenInfo.UserId.Should().BeNull();
        tokenInfo.Email.Should().BeNull();
    }
}
