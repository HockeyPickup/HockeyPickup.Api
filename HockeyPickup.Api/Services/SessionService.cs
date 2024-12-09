using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Responses;
using Microsoft.AspNetCore.Identity;

namespace HockeyPickup.Api.Services;

public interface ISessionService
{
    Task<ServiceResult<SessionDetailedResponse>> UpdateRosterPosition(int sessionId, string userId, int newPosition);
}

public class SessionService : ISessionService
{
    private readonly UserManager<AspNetUser> _userManager;
    private readonly ISessionRepository _sessionRepository;
    private readonly IServiceBus _serviceBus;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserService> _logger;

    public SessionService(UserManager<AspNetUser> userManager, ISessionRepository sessionRepository, IServiceBus serviceBus, IConfiguration configuration, ILogger<UserService> logger)
    {
        _userManager = userManager;
        _sessionRepository = sessionRepository;
        _serviceBus = serviceBus;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ServiceResult<SessionDetailedResponse>> UpdateRosterPosition(int sessionId, string userId, int newPosition)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("Roster player not found");
            }

            var session = await _sessionRepository.GetSessionAsync(sessionId);
            if (session == null)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("Session not found");
            }

            var currentRoster = session.CurrentRosters.Where(u => u.UserId == userId).FirstOrDefault();
            if (currentRoster.Position == newPosition)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("New position is the same as the current position");
            }

            await _sessionRepository.UpdatePlayerPositionAsync(sessionId, userId, newPosition);

            var msg = $"{user.FirstName} {user.LastName} changed position from {currentRoster.Position.ParsePositionName()} to {newPosition.ParsePositionName()}";

            var updatedSession = await _sessionRepository.AddActivityAsync(sessionId, msg);

            return ServiceResult<SessionDetailedResponse>.CreateSuccess(updatedSession, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating player position for session: {sessionId}, user: {userId}");
            return ServiceResult<SessionDetailedResponse>.CreateFailure($"An error occurred updating player position: {ex.Message}");
        }
    }
}
