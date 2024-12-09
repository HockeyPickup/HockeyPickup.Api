using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Data.Repositories;

public interface ISessionRepository
{
    Task<IEnumerable<SessionBasicResponse>> GetBasicSessionsAsync();
    Task<IEnumerable<SessionDetailedResponse>> GetDetailedSessionsAsync();
    Task<SessionDetailedResponse> GetSessionAsync(int sessionId);
    Task<SessionDetailedResponse> UpdatePlayerPositionAsync(int sessionId, string userId, int position);
    Task<SessionDetailedResponse> AddActivityAsync(int sessionId, string activity);
}
