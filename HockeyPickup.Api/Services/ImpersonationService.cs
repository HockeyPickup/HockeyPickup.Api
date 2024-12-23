using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HockeyPickup.Api.Services;

public interface IImpersonationService
{
    Task<ServiceResult<ImpersonationResponse>> ImpersonateUserAsync(string adminUserId, string targetUserId);
    Task<ServiceResult<RevertImpersonationResponse>> RevertImpersonationAsync(string currentUserId, ClaimsPrincipal user);
    Task<ServiceResult<ImpersonationStatusResponse>> GetStatusAsync(ClaimsPrincipal user);
}

public class ImpersonationService : IImpersonationService
{
    private readonly IUserService _userService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<ImpersonationService> _logger;
    private readonly HockeyPickupContext _context;

    public ImpersonationService(
        IUserService userService,
        IJwtService jwtService,
        ILogger<ImpersonationService> logger,
        HockeyPickupContext context)
    {
        _userService = userService;
        _jwtService = jwtService;
        _logger = logger;
        _context = context;
    }

    public async Task<ServiceResult<ImpersonationResponse>> ImpersonateUserAsync(string adminUserId, string targetUserId)
    {
        try
        {
            var admin = await _userService.GetUserByIdAsync(adminUserId)
                ?? throw new UnauthorizedAccessException("Admin user not found");

            if (!await _userService.IsInRoleAsync(admin, "Admin"))
                throw new UnauthorizedAccessException("User is not an admin");

            var targetUser = await _userService.GetUserByIdAsync(targetUserId)
                ?? throw new InvalidOperationException("Target user not found");

            // Get target user's roles
            var userRoles = await _userService.GetUserRolesAsync(targetUser);

            // Generate token with special claims
            var (token, expiration) = _jwtService.GenerateToken(
                targetUserId,
                targetUser.UserName ?? targetUser.Email!,
                userRoles.Concat(new[] {
                $"OriginalAdmin:{adminUserId}",
                $"ImpersonationStartTime:{DateTime.UtcNow:O}"
                })
            );

            var response = new ImpersonationResponse
            {
                Token = token,
                ImpersonatedUserId = targetUserId,
                OriginalUserId = adminUserId,
                StartTime = DateTime.UtcNow,
                ImpersonatedUser = targetUser.ToDetailedResponse()
            };

            return ServiceResult<ImpersonationResponse>.CreateSuccess(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during impersonation");
            return ServiceResult<ImpersonationResponse>.CreateFailure(ex.Message);
        }
    }

    public async Task<ServiceResult<RevertImpersonationResponse>> RevertImpersonationAsync(string currentUserId, ClaimsPrincipal user)
    {
        try
        {
            // Get claims from ClaimsPrincipal
            var originalAdminClaim = user.FindFirst(c => c.Type == ClaimTypes.Role && c.Value.StartsWith("OriginalAdmin:"))
                ?? throw new InvalidOperationException("No active impersonation found");

            var originalAdminId = originalAdminClaim.Value.Split(':')[1];

            // Get original admin user
            var admin = await _userService.GetUserByIdAsync(originalAdminId)
                ?? throw new InvalidOperationException("Original admin user not found");

            // Get admin's roles
            var adminRoles = await _userService.GetUserRolesAsync(admin);

            // Generate new token for original admin user
            var (token, expiration) = _jwtService.GenerateToken(
                originalAdminId,
                admin.UserName ?? admin.Email!,
                adminRoles
            );

            var response = new RevertImpersonationResponse
            {
                Token = token,
                OriginalUserId = originalAdminId,
                EndTime = DateTime.UtcNow
            };

            return ServiceResult<RevertImpersonationResponse>.CreateSuccess(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reverting impersonation");
            return ServiceResult<RevertImpersonationResponse>.CreateFailure(ex.Message);
        }
    }

    public Task<ServiceResult<ImpersonationStatusResponse>> GetStatusAsync(ClaimsPrincipal user)
    {
        try
        {
            var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
                return Task.FromResult(ServiceResult<ImpersonationStatusResponse>.CreateFailure("User not found"));

            // Check for impersonation by looking for OriginalAdmin role
            var originalAdminClaim = user.FindFirst(c => c.Type == ClaimTypes.Role && c.Value.StartsWith("OriginalAdmin:"));
            var startTimeClaim = user.FindFirst(c => c.Type == ClaimTypes.Role && c.Value.StartsWith("ImpersonationStartTime:"));

            if (originalAdminClaim == null)
            {
                // Not impersonating
                return Task.FromResult(ServiceResult<ImpersonationStatusResponse>.CreateSuccess(new ImpersonationStatusResponse
                {
                    IsImpersonating = false,
                    ImpersonatedUserId = null,
                    OriginalUserId = null,
                    StartTime = null
                }));
            }

            // Get the original admin ID from the claim
            var originalAdminId = originalAdminClaim.Value.Split(':')[1];

            // Parse start time if available
            DateTime? startTime = null;
            if (startTimeClaim != null)
            {
                var startTimeStr = startTimeClaim.Value.Split(':')[1];
                if (DateTime.TryParse(startTimeStr, out var parsedTime))
                {
                    startTime = parsedTime;
                }
            }

            return Task.FromResult(ServiceResult<ImpersonationStatusResponse>.CreateSuccess(new ImpersonationStatusResponse
            {
                IsImpersonating = true,
                ImpersonatedUserId = currentUserId,
                OriginalUserId = originalAdminId,
                StartTime = startTime
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting impersonation status");
            return Task.FromResult(ServiceResult<ImpersonationStatusResponse>.CreateFailure($"An error occurred while getting impersonation status: {ex.Message}"));
        }
    }
}
