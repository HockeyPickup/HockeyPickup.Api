using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Responses;
using System.Globalization;
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
            _logger.LogDebug($"Current User Id: {currentUserId}");

            if (string.IsNullOrEmpty(currentUserId))
                return Task.FromResult(ServiceResult<ImpersonationStatusResponse>.CreateFailure("User not found"));

            // Check for impersonation by looking for OriginalAdmin role
            var originalAdminClaim = user.FindFirst(c => c.Type == ClaimTypes.Role && c.Value.StartsWith("OriginalAdmin:"));
            var startTimeClaim = user.FindFirst(c => c.Type == ClaimTypes.Role && c.Value.StartsWith("ImpersonationStartTime|"));

            _logger.LogDebug($"Original Admin Claim: {originalAdminClaim?.Value}");
            _logger.LogDebug($"Start Time Claim: {startTimeClaim?.Value}");

            if (originalAdminClaim == null)
            {
                _logger.LogDebug("No original admin claim found - not impersonating");
                return Task.FromResult(ServiceResult<ImpersonationStatusResponse>.CreateSuccess(new ImpersonationStatusResponse
                {
                    IsImpersonating = false,
                    ImpersonatedUserId = null,
                    OriginalUserId = null,
                    StartTime = null
                }));
            }

            // Get the original admin Id from the claim
            var originalAdminId = originalAdminClaim.Value.Split(':')[1];
            _logger.LogDebug($"Parsed Admin Id: {originalAdminId}");

            // Parse start time if available
            // In GetStatusAsync method:
            DateTime? startTime = null;
            if (startTimeClaim != null)
            {
                var startTimeStr = startTimeClaim.Value.Split('|')[1];
                _logger.LogDebug($"Parsed Start Time String: {startTimeStr}");

                // Parse explicitly as UTC
                if (DateTime.TryParse(startTimeStr, null, DateTimeStyles.RoundtripKind, out var parsedTime))
                {
                    startTime = parsedTime;
                    _logger.LogDebug($"Successfully parsed time: {startTime}");
                }
                else
                {
                    _logger.LogWarning($"Failed to parse time from string: {startTimeStr}");
                }
            }

            var response = new ImpersonationStatusResponse
            {
                IsImpersonating = true,
                ImpersonatedUserId = currentUserId,
                OriginalUserId = originalAdminId,
                StartTime = startTime
            };

            _logger.LogDebug($"Created response: IsImpersonating={response.IsImpersonating}, " +
                            $"ImpersonatedUserId={response.ImpersonatedUserId}, " +
                            $"OriginalUserId={response.OriginalUserId}, " +
                            $"StartTime={response.StartTime}");

            return Task.FromResult(ServiceResult<ImpersonationStatusResponse>.CreateSuccess(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting impersonation status");
            return Task.FromResult(ServiceResult<ImpersonationStatusResponse>.CreateFailure(
                $"An error occurred while getting impersonation status: {ex.Message}"));
        }
    }
}
