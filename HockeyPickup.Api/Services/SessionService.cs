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
    Task<ServiceResult<SessionDetailedResponse>> UpdateRosterPlayingStatus(int sessionId, string userId, bool isPlaying, string note);
    Task<ServiceResult<SessionDetailedResponse>> DeleteRosterPlayer(int sessionId, string userId);
    Task<ServiceResult<SessionDetailedResponse>> AddRosterPlayer(AddRosterPlayerRequest request);
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
    private readonly ILotteryService _lotteryService;

    public SessionService(UserManager<AspNetUser> userManager, ISessionRepository sessionRepository, IServiceBus serviceBus, IConfiguration configuration, ILogger<UserService> logger, ISubscriptionHandler subscriptionHandler, IHttpContextAccessor httpContextAccessor, IUserRepository userRepository, ILotteryService lotteryService)
    {
        _userManager = userManager;
        _sessionRepository = sessionRepository;
        _serviceBus = serviceBus;
        _configuration = configuration;
        _logger = logger;
        _subscriptionHandler = subscriptionHandler;
        _httpContextAccessor = httpContextAccessor;
        _userRepository = userRepository;
        _lotteryService = lotteryService;
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
                LotteryEnabled = request.LotteryEnabled,
                LotteryEntryWindowMinutes = request.LotteryEntryWindowMinutes,
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

            // Schedule the per-tier lottery draws when the lottery is enabled
            if (updatedSession.LotteryEnabled)
                await _lotteryService.EnqueueDrawMessagesAsync(updatedSession);

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
                LotteryEnabled = request.LotteryEnabled,
                LotteryEntryWindowMinutes = request.LotteryEntryWindowMinutes,
                UpdateDateTime = DateTime.UtcNow
            };

            // Re-enqueue lottery draws only when something that moves the draw times changed.
            var lotteryScheduleChanged =
                existingSession.SessionDate != request.SessionDate ||
                existingSession.BuyDayMinimum != request.BuyDayMinimum ||
                existingSession.LotteryEnabled != request.LotteryEnabled ||
                existingSession.LotteryEntryWindowMinutes != request.LotteryEntryWindowMinutes;

            var updatedSession = await _sessionRepository.UpdateSessionAsync(session);
            var msg = $"Edited Session";

            updatedSession = await _sessionRepository.AddActivityAsync(updatedSession.SessionId, msg);
            await _subscriptionHandler.HandleUpdate(updatedSession);

            if (updatedSession.LotteryEnabled && lotteryScheduleChanged)
                await _lotteryService.EnqueueDrawMessagesAsync(updatedSession);

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

            if (newPosition == PositionPreference.Goalie)
            {
                // Goalies never carry a team: set the position and clear the team in one repository call
                await _sessionRepository.UpdatePlayerPositionAndTeamAsync(sessionId, userId, newPosition, TeamAssignment.TBD);
            }
            else
            {
                await _sessionRepository.UpdatePlayerPositionAsync(sessionId, userId, newPosition);
            }

            var msg = $"{user.FirstName} {user.LastName} changed position from {currentRoster.Position.ParsePositionName()} to {newPosition.ParsePositionName()}";
            if (currentRoster.Position == PositionPreference.Goalie)
            {
                // A former goalie stays TBD until an admin puts them on Light or Dark
                msg += " (assign a team)";
            }

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

            if (currentRoster.Position == PositionPreference.Goalie)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("Goalies are not assigned to a team");
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

    public async Task<ServiceResult<SessionDetailedResponse>> UpdateRosterPlayingStatus(int sessionId, string userId, bool isPlaying, string note)
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

            if (currentRoster.IsPlaying == isPlaying)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure($"Playing status is already {(isPlaying ? "playing" : "not playing")}");
            }

            await _sessionRepository.UpdatePlayerStatusAsync(sessionId, userId, isPlaying, isPlaying == false ? DateTime.UtcNow : null, null);

            var msg = $"{user.FirstName} {user.LastName} playing status changed from {(currentRoster.IsPlaying ? "playing" : "not playing")} to {(isPlaying ? "playing" : "not playing")}";
            if (!string.IsNullOrEmpty(note))
            {
                msg += $" - {note}";
            }

            var updatedSession = await _sessionRepository.AddActivityAsync(sessionId, msg);
            await _subscriptionHandler.HandleUpdate(updatedSession);

            // Send a message to Service Bus that a players playing status was updated
            await SendSessionServiceBusCommsMessageAsync("PlayingStatusChange", new Dictionary<string, string>
            {
                { "PreviousPlayingStatus", currentRoster.IsPlaying.ToString() },
                { "UpdatedPlayingStatus", isPlaying.ToString() },
                { "Note", note },
            }, sessionId, session.SessionDate, user);

            return ServiceResult<SessionDetailedResponse>.CreateSuccess(updatedSession, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating playing status for session: {sessionId}, user: {userId}");
            return ServiceResult<SessionDetailedResponse>.CreateFailure($"An error occurred updating playing status: {ex.Message}");
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

    public async Task<ServiceResult<SessionDetailedResponse>> AddRosterPlayer(AddRosterPlayerRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("User not found");
            }

            var session = await _sessionRepository.GetSessionAsync(request.SessionId);
            if (session == null)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("Session not found");
            }

            var name = $"{user.FirstName} {user.LastName}";
            var existingRoster = session.CurrentRosters?.FirstOrDefault(r => r.UserId == request.UserId);
            if (existingRoster != null)
            {
                // Never reactivate a not-playing row here: it is linked to the BuySell that took them off
                return ServiceResult<SessionDetailedResponse>.CreateFailure(existingRoster.IsPlaying
                    ? $"{name} is already on the roster for this session"
                    : $"{name} has a not-playing roster entry for this session; use playing status instead");
            }

            var isGoalie = request.Position == PositionPreference.Goalie;
            if (!isGoalie && request.TeamAssignment != TeamAssignment.Light && request.TeamAssignment != TeamAssignment.Dark)
            {
                return ServiceResult<SessionDetailedResponse>.CreateFailure("A skater must be assigned to Light or Dark");
            }

            // Goalies are never on a team, whatever was requested
            var team = isGoalie ? TeamAssignment.TBD : request.TeamAssignment;

            if (!user.Active)
            {
                // Allowed: legacy goalies and lapsed players still show up at the rink
                _logger.LogInformation($"Adding inactive user {request.UserId} to roster for session {request.SessionId}");
            }

            await _sessionRepository.AddRosterPlayerAsync(request.SessionId, request.UserId, team, request.Position);

            var msg = isGoalie
                ? $"{name} added to roster as Goalie"
                : $"{name} added to roster as {request.Position.ParsePositionName()} on {team.GetDisplayName()}";

            var updatedSession = await _sessionRepository.AddActivityAsync(request.SessionId, msg);
            await _subscriptionHandler.HandleUpdate(updatedSession);

            // Send a message to Service Bus that a player was added to the roster (same message for skaters and goalies)
            await SendSessionServiceBusCommsMessageAsync("AddedToRoster", null, request.SessionId, session.SessionDate, user);

            return ServiceResult<SessionDetailedResponse>.CreateSuccess(updatedSession, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error adding player to roster for session: {request.SessionId}, user: {request.UserId}");
            return ServiceResult<SessionDetailedResponse>.CreateFailure($"An error occurred adding player to roster: {ex.GetRelevantMessage()}");
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
        if (messageDataAdditions != null)
        {
            foreach (var mda in messageDataAdditions)
            {
                commsMessage.MessageData.Add(mda.Key, mda.Value);
            }
        }

        await _serviceBus.SendAsync(commsMessage, subject: type, correlationId: Guid.NewGuid().ToString(), queueName: _configuration["ServiceBusCommsQueueName"]);
    }
}
