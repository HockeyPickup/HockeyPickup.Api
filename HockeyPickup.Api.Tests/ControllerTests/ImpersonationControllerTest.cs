using Microsoft.AspNetCore.Mvc;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Controllers;
using HockeyPickup.Api.Services;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Models.Requests;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using HockeyPickup.Api.Helpers;

namespace HockeyPickup.Api.Tests.ControllerTests;

public class ImpersonationControllerTest
{
    private readonly Mock<IImpersonationService> _mockImpersonationService;
    private readonly ImpersonationController _controller;

    public ImpersonationControllerTest()
    {
        _mockImpersonationService = new Mock<IImpersonationService>();
        _controller = new ImpersonationController(_mockImpersonationService.Object);
    }

    private void SetupUser(string userId, string? role = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, "test@example.com")
        };

        if (role != null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task ImpersonateUser_ValidRequest_ReturnsOk()
    {
        // Arrange
        var adminUserId = "admin-id";
        var targetUserId = "target-id";
        SetupUser(adminUserId, "Admin");

        var request = new ImpersonationRequest { TargetUserId = targetUserId };
        var impersonationResponse = new ImpersonationResponse
        {
            Token = "new-token",
            ImpersonatedUserId = targetUserId,
            OriginalUserId = adminUserId,
            StartTime = DateTime.UtcNow,
            ImpersonatedUser = new UserDetailedResponse
            {
                Id = targetUserId,
                UserName = "target.user",
                Email = "target@example.com",
                FirstName = "Target",
                LastName = "User",
                Rating = 4.5m,
                Active = true,
                Preferred = true,
                PreferredPlus = false
            }
        };

        _mockImpersonationService
            .Setup(x => x.ImpersonateUserAsync(adminUserId, targetUserId))
            .ReturnsAsync(ServiceResult<ImpersonationResponse>.CreateSuccess(impersonationResponse));

        // Act
        var result = await _controller.ImpersonateUser(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<ImpersonationResponse>>().Subject;
        response.Data.Should().NotBeNull();
        response.Data.ImpersonatedUserId.Should().Be(targetUserId);
        response.Data.OriginalUserId.Should().Be(adminUserId);
        response.Data.ImpersonatedUser.Should().NotBeNull();
        response.Data.ImpersonatedUser!.Id.Should().Be(targetUserId);
        response.Data.ImpersonatedUser.UserName.Should().Be("target.user");
        response.Data.ImpersonatedUser.Email.Should().Be("target@example.com");
    }

    [Fact]
    public async Task ImpersonateUser_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var adminUserId = "admin-id";
        var targetUserId = "target-id";
        SetupUser(adminUserId, "Admin");

        var request = new ImpersonationRequest { TargetUserId = targetUserId };

        _mockImpersonationService
            .Setup(x => x.ImpersonateUserAsync(adminUserId, targetUserId))
            .ReturnsAsync(ServiceResult<ImpersonationResponse>.CreateFailure("Failed to impersonate user"));

        // Act
        var result = await _controller.ImpersonateUser(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<ImpersonationResponse>>().Subject;
        response.Message.Should().Be("Failed to impersonate user");
    }

    [Fact]
    public async Task RevertImpersonation_Success_ReturnsOk()
    {
        // Arrange
        var userId = "user-id";
        SetupUser(userId);

        var revertResponse = new RevertImpersonationResponse
        {
            Token = "original-token",
            OriginalUserId = userId,
            EndTime = DateTime.UtcNow
        };

        _mockImpersonationService
            .Setup(x => x.RevertImpersonationAsync(userId, It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(ServiceResult<RevertImpersonationResponse>.CreateSuccess(revertResponse));

        // Act
        var result = await _controller.RevertImpersonation();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<RevertImpersonationResponse>>().Subject;
        response.Data.Should().NotBeNull();
        response.Data.OriginalUserId.Should().Be(userId);
    }

    [Fact]
    public async Task RevertImpersonation_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var userId = "user-id";
        SetupUser(userId);

        _mockImpersonationService
            .Setup(x => x.RevertImpersonationAsync(userId, It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(ServiceResult<RevertImpersonationResponse>.CreateFailure("Failed to revert impersonation"));

        // Act
        var result = await _controller.RevertImpersonation();

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<RevertImpersonationResponse>>().Subject;
        response.Message.Should().Be("Failed to revert impersonation");
    }

    [Fact]
    public async Task GetStatus_ReturnsOk()
    {
        // Arrange
        var userId = "user-id";
        SetupUser(userId);

        var statusResponse = new ImpersonationStatusResponse
        {
            IsImpersonating = true,
            OriginalUserId = "admin-id",
            ImpersonatedUserId = userId,
            StartTime = DateTime.UtcNow
        };

        _mockImpersonationService
            .Setup(x => x.GetStatusAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(ServiceResult<ImpersonationStatusResponse>.CreateSuccess(statusResponse));

        // Act
        var result = await _controller.GetStatus();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<ImpersonationStatusResponse>>().Subject;
        response.Data.Should().NotBeNull();
        response.Data.IsImpersonating.Should().BeTrue();
    }
}
