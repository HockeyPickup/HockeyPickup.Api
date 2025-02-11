using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace HockeyPickup.Api.Tests.ExtensionTests;

public partial class RatingSecurityExtensionsTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RatingSecurityExtensionsTests()
    {
        _httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        RatingSecurityExtensions.Initialize(_httpContextAccessor);
    }

    [Fact]
    public void GetSecureRating_NullUser_ReturnsZero()
    {
        // Arrange
        AspNetUser? user = null;

        // Act
        var result = user.GetSecureRating();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void GetSecureRating_UserWithZeroRating_ReturnsZero()
    {
        // Arrange
        var user = new AspNetUser { Rating = 0 };

        // Act
        var result = user.GetSecureRating();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void GetSecureRating_NonAdminUser_ReturnsZero()
    {
        // Arrange
        SetupUserContext("RegularUser", Array.Empty<string>());
        var user = new AspNetUser { Rating = 5.0m };

        // Act
        var result = user.GetSecureRating();

        // Assert
        result.Should().Be(0);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SubAdmin")]
    public void GetSecureRating_AdminUser_ReturnsActualRating(string role)
    {
        // Arrange
        SetupUserContext("AdminUser", new[] { role });
        var user = new AspNetUser { Rating = 5.0m };

        // Act
        var result = user.GetSecureRating();

        // Assert
        result.Should().Be(5.0m);
    }

    [Fact]
    public void GetSecureRating_NoHttpContext_ReturnsZero()
    {
        // Arrange
        _httpContextAccessor.HttpContext = null;
        var user = new AspNetUser { Rating = 5.0m };

        // Act
        var result = user.GetSecureRating();

        // Assert
        result.Should().Be(0);
    }

    private void SetupUserContext(string userName, string[] roles)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, userName)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _httpContextAccessor.HttpContext!.User = principal;
    }

    [Fact]
    public void GetSecureRating_RosterPlayer_NullOrZeroRating_ReturnsZero()
    {
        // Arrange
        CurrentSessionRoster? player = null;
        var playerWithZero = new CurrentSessionRoster { Rating = 0 };

        // Act
        var resultNull = player.GetSecureRating();
        var resultZero = playerWithZero.GetSecureRating();

        // Assert
        resultNull.Should().Be(0);
        resultZero.Should().Be(0);
    }

    [Fact]
    public void GetSecureRating_RosterPlayer_AdminUser_ReturnsRating()
    {
        // Arrange
        SetupUserContext("AdminUser", new[] { "Admin" });
        var player = new CurrentSessionRoster { Rating = 5.0m };

        // Act
        var result = player.GetSecureRating();

        // Assert
        result.Should().Be(5.0m);
    }

    [Fact]
    public void GetSecureRating_CacheHit_ReturnsCachedValue()
    {
        // Arrange
        SetupUserContext("AdminUser", new[] { "Admin" });
        var user = new AspNetUser { Rating = 5.0m };

        // Act - First call to prime the cache
        var result1 = user.GetSecureRating();
        // Second call should hit cache
        var result2 = user.GetSecureRating();

        // Assert
        result1.Should().Be(5.0m);
        result2.Should().Be(5.0m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("User")]
    [InlineData("Member")]
    public void GetSecureRating_NonAdminRoles_ReturnsZero(string role)
    {
        // Arrange
        var roles = string.IsNullOrEmpty(role) ? Array.Empty<string>() : new[] { role };
        SetupUserContext("User", roles);
        var user = new AspNetUser { Rating = 5.0m };

        // Act
        var result = user.GetSecureRating();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void GetSecureRating_NullClaims_ReturnsZero()
    {
        // Arrange
        SetupUserContext("User", Array.Empty<string>());
        var user = new AspNetUser { Rating = 5.0m };
        _httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(); // No claims

        // Act
        var result = user.GetSecureRating();

        // Assert
        result.Should().Be(0);
    }

    [Theory]
    [InlineData(true, 5.0, 5.0)]
    [InlineData(false, 5.0, 0)]
    public void GetSecureRatingInternal_ReturnsBothPaths(bool isAdmin, decimal inputRating, decimal expectedRating)
    {
        // Arrange
        SetupUserContext("TestUser", isAdmin ? new[] { "Admin" } : Array.Empty<string>());
        var user = new AspNetUser { Rating = inputRating };

        // Act
        var result = user.GetSecureRating();

        // Assert
        result.Should().Be(expectedRating);
    }

    [Fact]
    public void GetSecureRating_BothAdminAndNonAdmin_CoversBothPaths()
    {
        // Arrange
        var user = new AspNetUser { Rating = 5.0m };

        // Act & Assert - First as non-admin
        SetupUserContext("TestUser", Array.Empty<string>());
        RatingSecurityExtensions.ClearCache();  // Clear before first check
        user.GetSecureRating().Should().Be(0);

        // Act & Assert - Then as admin
        SetupUserContext("TestUser", new[] { "Admin" });
        RatingSecurityExtensions.ClearCache();  // Clear before second check
        user.GetSecureRating().Should().Be(5.0m);
    }

    [Fact]
    public void GetSecureRating_NullHttpContextAccessor_ReturnsZero()
    {
        // Arrange
        RatingSecurityExtensions.Initialize(null); // Set the static accessor to null
        var user = new AspNetUser { Rating = 5.0m };

        // Act
        var result = user.GetSecureRating();

        // Assert
        result.Should().Be(0);

        // Cleanup - restore the accessor for other tests
        RatingSecurityExtensions.Initialize(new HttpContextAccessor());
    }
}
