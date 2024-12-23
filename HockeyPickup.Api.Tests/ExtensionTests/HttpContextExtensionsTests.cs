
using FluentAssertions;
using HockeyPickup.Api.Helpers;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace HockeyPickup.Api.Tests.ExtensionTests;

public partial class HttpContextExtensionsTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextExtensionsTests()
    {
        _httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        RatingSecurityExtensions.Initialize(_httpContextAccessor);
    }

    [Fact]
    public void GetUserId_NullHttpContext_ThrowsUnauthorized()
    {
        // Arrange
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext) null!);

        // Act & Assert
        var action = () => httpContextAccessor.Object.GetUserId();
        action.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("User ID not found in context");
    }

    [Fact]
    public void GetUserId_NullUser_ThrowsUnauthorized()
    {
        // Arrange
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns((ClaimsPrincipal) null!);
        httpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        // Act & Assert
        var action = () => httpContextAccessor.Object.GetUserId();
        action.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("User ID not found in context");
    }

    [Fact]
    public void GetUserId_NoNameIdentifierClaim_ThrowsUnauthorized()
    {
        // Arrange
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new Mock<HttpContext>();
        var mockPrincipal = new Mock<ClaimsPrincipal>();

        mockPrincipal.Setup(x => x.FindFirst(ClaimTypes.NameIdentifier))
            .Returns((Claim) null!);
        mockHttpContext.Setup(x => x.User).Returns(mockPrincipal.Object);
        httpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        // Act & Assert
        var action = () => httpContextAccessor.Object.GetUserId();
        action.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("User ID not found in context");
    }
}
