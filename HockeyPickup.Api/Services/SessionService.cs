using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using Microsoft.AspNetCore.Identity;

namespace HockeyPickup.Api.Services;

public interface ISessionService
{
    Task<ServiceResult<SessionDetailedResponse>> CreateSession(CreateSessionRequest request);
    Task<ServiceResult<SessionDetailedResponse>> UpdateSession(UpdateSessionRequest request);
    Task<ServiceResult<SessionDetailedResponse>> UpdateRosterPosition(int sessionId, string userId, int newPosition);
    Task<ServiceResult<SessionDetailedResponse>> UpdateRosterTeam(int sessionId, string userId, int newTeamAssignment);
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

    public async Task<ServiceResult<SessionDetailedResponse>> CreateSession(CreateSessionRequest request)
    {
        try
        {
            var session = new Session
            {
                SessionDate = request.SessionDate,
                Note = request.Note,
                RegularSetId = request.RegularSetId,
                BuyDayMinimum = request.BuyDayMinimum,
                Cost = request.Cost,
                CreateDateTime = DateTime.UtcNow,
                UpdateDateTime = DateTime.UtcNow
            };

            var createdSession = await _sessionRepository.CreateSessionAsync(session);
            var msg = $"Session created for {request.SessionDate:MM/dd/yyyy}";

            var updatedSession = await _sessionRepository.AddActivityAsync(createdSession.SessionId, msg);
            return ServiceResult<SessionDetailedResponse>.CreateSuccess(updatedSession, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session");
            return ServiceResult<SessionDetailedResponse>.CreateFailure($"An error occurred creating the session: {ex.Message}");
        }
    }

    public async Task<ServiceResult<SessionDetailedResponse>> UpdateSession(UpdateSessionRequest request)
    {
        try
        {
            var existingSession = await _sessionRepository.GetSessionAsync(request.SessionId);
            if (existingSession == null)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("Session not found");
            }

            var session = new Session
            {
                SessionId = request.SessionId,
                SessionDate = request.SessionDate,
                Note = request.Note,
                RegularSetId = request.RegularSetId,
                BuyDayMinimum = request.BuyDayMinimum,
                Cost = request.Cost,
                UpdateDateTime = DateTime.UtcNow
            };

            var updatedSession = await _sessionRepository.UpdateSessionAsync(session);
            var msg = $"Edited Session";

            updatedSession = await _sessionRepository.AddActivityAsync(updatedSession.SessionId, msg);
            await WebSocketMiddleware.NotifySessionUpdate(request.SessionId, updatedSession);
            return ServiceResult<SessionDetailedResponse>.CreateSuccess(updatedSession, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating session: {request.SessionId}");
            return ServiceResult<SessionDetailedResponse>.CreateFailure($"An error occurred updating the session: {ex.Message}");
        }
    }

    public async Task<ServiceResult<SessionDetailedResponse>> UpdateRosterPosition(int sessionId, string userId, int newPosition)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("User not found");
            }

            var session = await _sessionRepository.GetSessionAsync(sessionId);
            if (session == null)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("Session not found");
            }

            var currentRoster = session.CurrentRosters.Where(u => u.UserId == userId).FirstOrDefault();
            if (currentRoster == null)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("User is not part of this session's current roster");
            }

            if (currentRoster.Position == newPosition)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("New position is the same as the current position");
            }

            await _sessionRepository.UpdatePlayerPositionAsync(sessionId, userId, newPosition);

            var msg = $"{user.FirstName} {user.LastName} changed position from {currentRoster.Position.ParsePositionName()} to {newPosition.ParsePositionName()}";

            var updatedSession = await _sessionRepository.AddActivityAsync(sessionId, msg);
            await WebSocketMiddleware.NotifySessionUpdate(sessionId, updatedSession);

            return ServiceResult<SessionDetailedResponse>.CreateSuccess(updatedSession, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating player position for session: {sessionId}, user: {userId}");
            return ServiceResult<SessionDetailedResponse>.CreateFailure($"An error occurred updating player position: {ex.Message}");
        }
    }

    public async Task<ServiceResult<SessionDetailedResponse>> UpdateRosterTeam(int sessionId, string userId, int newTeamAssignment)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("User not found");
            }

            var session = await _sessionRepository.GetSessionAsync(sessionId);
            if (session == null)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("Session not found");
            }

            var currentRoster = session.CurrentRosters.Where(u => u.UserId == userId).FirstOrDefault();
            if (currentRoster == null)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("User is not part of this session's current roster");
            }

            if (currentRoster.TeamAssignment == newTeamAssignment)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("New team assignment is the same as the current team assignment");
            }

            await _sessionRepository.UpdatePlayerTeamAsync(sessionId, userId, newTeamAssignment);

            var msg = $"{user.FirstName} {user.LastName} changed team assignment from {currentRoster.TeamAssignment.ParseTeamName()} to {newTeamAssignment.ParseTeamName()}";

            var updatedSession = await _sessionRepository.AddActivityAsync(sessionId, msg);
            await WebSocketMiddleware.NotifySessionUpdate(sessionId, updatedSession);

            return ServiceResult<SessionDetailedResponse>.CreateSuccess(updatedSession, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating player team assignment for session: {sessionId}, user: {userId}");
            return ServiceResult<SessionDetailedResponse>.CreateFailure($"An error occurred updating player team assignment: {ex.Message}");
        }
    }
}
