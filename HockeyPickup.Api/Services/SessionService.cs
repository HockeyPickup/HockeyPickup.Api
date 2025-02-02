using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using Microsoft.AspNetCore.Identity;

namespace HockeyPickup.Api.Services;

public interface ISessionService
{
    Task<ServiceResult<SessionDetailedResponse>> CreateSession(CreateSessionRequest request);
    Task<ServiceResult<SessionDetailedResponse>> UpdateSession(UpdateSessionRequest request);
    Task<ServiceResult<SessionDetailedResponse>> UpdateRosterPosition(int sessionId, string userId, PositionPreference newPosition);
    Task<ServiceResult<SessionDetailedResponse>> UpdateRosterTeam(int sessionId, string userId, TeamAssignment newTeamAssignment);
    Task<ServiceResult<SessionDetailedResponse>> DeleteRosterPlayer(int sessionId, string userId);
    Task<ServiceResult<bool>> DeleteSessionAsync(int sessionId);
}

public class SessionService : ISessionService
{
    private readonly UserManager<AspNetUser> _userManager;
    private readonly ISessionRepository _sessionRepository;
    private readonly IServiceBus _serviceBus;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserService> _logger;
    private readonly ISubscriptionHandler _subscriptionHandler;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserRepository _userRepository;

    public SessionService(UserManager<AspNetUser> userManager, ISessionRepository sessionRepository, IServiceBus serviceBus, IConfiguration configuration, ILogger<UserService> logger, ISubscriptionHandler subscriptionHandler, IHttpContextAccessor httpContextAccessor, IUserRepository userRepository)
    {
        _userManager = userManager;
        _sessionRepository = sessionRepository;
        _serviceBus = serviceBus;
        _configuration = configuration;
        _logger = logger;
        _subscriptionHandler = subscriptionHandler;
        _httpContextAccessor = httpContextAccessor;
        _userRepository = userRepository;
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

            // Get the creating user's info
            var userId = _httpContextAccessor.GetUserId();
            var user = await _userManager.FindByIdAsync(userId);

            // Send a message to Service Bus that a session was created
            await SendSessionServiceBusCommsMessageAsync("CreateSession", new Dictionary<string, string>
            {
                { "Note", session.Note },
                { "CreatedByName", $"{user.FirstName} {user.LastName}" }
            }, session.SessionId, session.SessionDate, user, true);

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
            await _subscriptionHandler.HandleUpdate(updatedSession);
            return ServiceResult<SessionDetailedResponse>.CreateSuccess(updatedSession, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating session: {request.SessionId}");
            return ServiceResult<SessionDetailedResponse>.CreateFailure($"An error occurred updating the session: {ex.Message}");
        }
    }

    public async Task<ServiceResult<SessionDetailedResponse>> UpdateRosterPosition(int sessionId, string userId, PositionPreference newPosition)
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
            await _subscriptionHandler.HandleUpdate(updatedSession);

            return ServiceResult<SessionDetailedResponse>.CreateSuccess(updatedSession, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating player position for session: {sessionId}, user: {userId}");
            return ServiceResult<SessionDetailedResponse>.CreateFailure($"An error occurred updating player position: {ex.Message}");
        }
    }

    public async Task<ServiceResult<SessionDetailedResponse>> UpdateRosterTeam(int sessionId, string userId, TeamAssignment newTeamAssignment)
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

            if (currentRoster.TeamAssignment == (TeamAssignment) newTeamAssignment)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("New team assignment is the same as the current team assignment");
            }

            await _sessionRepository.UpdatePlayerTeamAsync(sessionId, userId, newTeamAssignment);

            var msg = $"{user.FirstName} {user.LastName} changed team assignment from {currentRoster.TeamAssignment.GetDisplayName()} to {newTeamAssignment.GetDisplayName()}";

            var updatedSession = await _sessionRepository.AddActivityAsync(sessionId, msg);
            await _subscriptionHandler.HandleUpdate(updatedSession);

            // Send a message to Service Bus that a players position was updated
            await SendSessionServiceBusCommsMessageAsync("TeamAssignmentChange", new Dictionary<string, string>
            {
                { "FormerTeamAssignment", currentRoster.TeamAssignment.GetDisplayName() },
                { "NewTeamAssignment", newTeamAssignment.GetDisplayName() }
            }, sessionId, session.SessionDate, user);

            return ServiceResult<SessionDetailedResponse>.CreateSuccess(updatedSession, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating player team assignment for session: {sessionId}, user: {userId}");
            return ServiceResult<SessionDetailedResponse>.CreateFailure($"An error occurred updating player team assignment: {ex.Message}");
        }
    }

    public async Task<ServiceResult<SessionDetailedResponse>> DeleteRosterPlayer(int sessionId, string userId)
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

            await _sessionRepository.DeletePlayerFromRosterAsync(sessionId, userId);

            var msg = $"{user.FirstName} {user.LastName} deleted from roster";

            var updatedSession = await _sessionRepository.AddActivityAsync(sessionId, msg);
            await _subscriptionHandler.HandleUpdate(updatedSession);

            // Send a message to Service Bus that a player was deleted from roster
            await SendSessionServiceBusCommsMessageAsync("DeletedFromRoster", null, sessionId, session.SessionDate, user);

            return ServiceResult<SessionDetailedResponse>.CreateSuccess(updatedSession, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting player from roster for session: {sessionId}, user: {userId}");
            return ServiceResult<SessionDetailedResponse>.CreateFailure($"An error occurred deleting player from roster: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> DeleteSessionAsync(int sessionId)
    {
        try
        {
            var existingSession = await _sessionRepository.GetSessionAsync(sessionId);
            if (existingSession == null)
            {
                return ServiceResult<bool>.CreateFailure("Session not found");
            }

            var result = await _sessionRepository.DeleteSessionAsync(sessionId);
            var msg = $"Deleted Session {sessionId}";

            if (result)
            {
                await _subscriptionHandler.HandleDelete(sessionId);
                return ServiceResult<bool>.CreateSuccess(true, msg);
            }

            return ServiceResult<bool>.CreateFailure("Failed to delete session");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting session: {sessionId}");
            return ServiceResult<bool>.CreateFailure($"An error occurred deleting the session: {ex.Message}");
        }
    }

    private async Task SendSessionServiceBusCommsMessageAsync(string type, Dictionary<string, string>? messageDataAdditions, int sessionId, DateTime sessionDate, AspNetUser user, bool sendToEveryone = false)
    {
        var baseUrl = _configuration["BaseUrl"];
        var sessionUrl = $"{baseUrl.TrimEnd('/')}/session/{sessionId}";

        var users = await _userRepository.GetDetailedUsersAsync();
        var userEmails = users.Where(u => u.Active && (u.NotificationPreference == NotificationPreference.All || (sendToEveryone && u.NotificationPreference == NotificationPreference.OnlyMyBuySell))).Select(u => u.Email).Where(email => !string.IsNullOrEmpty(email)).ToArray();

        var commsMessage = new ServiceBusCommsMessage
        {
            Metadata = new Dictionary<string, string>
            {
                { "Type", type },
                { "CommunicationEventId", Guid.NewGuid().ToString() }
            },
            CommunicationMethod = new Dictionary<string, string>
            {
                { "Email", user.Email },
                { "NotificationPreference", user.NotificationPreference.ToString() }
            },
            RelatedEntities = new Dictionary<string, string>
            {
                { "UserId", user.Id },
                { "FirstName", user.FirstName },
                { "LastName", user.LastName }
            },
            MessageData = new Dictionary<string, string>
            {
                { "SessionDate", sessionDate.ToString() },
                { "SessionUrl", sessionUrl },
            },
            NotificationEmails = userEmails!,
            NotificationDeviceIds = null
        };

        // Append the extra message data fields
        foreach (var mda in messageDataAdditions)
        {
            commsMessage.MessageData.Add(mda.Key, mda.Value);
        }

        await _serviceBus.SendAsync(commsMessage, subject: type, correlationId: Guid.NewGuid().ToString(), queueName: _configuration["ServiceBusCommsQueueName"]);
    }
}
