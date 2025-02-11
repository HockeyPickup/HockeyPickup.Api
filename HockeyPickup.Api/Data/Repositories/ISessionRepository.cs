using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Data.Repositories;

public interface ISessionRepository
{
    Task<IEnumerable<SessionBasicResponse>> GetBasicSessionsAsync();
    Task<IEnumerable<SessionDetailedResponse>> GetDetailedSessionsAsync();
    Task<SessionDetailedResponse> GetSessionAsync(int sessionId);
    Task<SessionDetailedResponse> CreateSessionAsync(Session session);
    Task<SessionDetailedResponse> UpdateSessionAsync(Session session);
    Task<SessionDetailedResponse> AddActivityAsync(int sessionId, string activity);
    Task<SessionDetailedResponse> UpdatePlayerPositionAsync(int sessionId, string userId, PositionPreference position);
    Task<SessionDetailedResponse> UpdatePlayerTeamAsync(int sessionId, string userId, TeamAssignment team);
    Task<bool> DeleteSessionAsync(int sessionId);
    Task<SessionDetailedResponse> DeletePlayerFromRosterAsync(int sessionId, string userId);
    Task<SessionDetailedResponse> UpdatePlayerStatusAsync(int sessionId, string userId, bool isPlaying, DateTime? leftDateTime, int? lastBuySellId);
    Task<SessionDetailedResponse> AddOrUpdatePlayerToRosterAsync(int sessionId, string userId, TeamAssignment teamAssignment, PositionPreference positionPreference, int? lastBuySellId);
}
