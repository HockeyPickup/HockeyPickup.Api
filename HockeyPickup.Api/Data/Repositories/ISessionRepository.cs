using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Data.Repositories;

public interface ISessionRepository
{
    Task<IEnumerable<SessionBasicResponse>> GetBasicSessionsAsync();
    Task<IEnumerable<SessionDetailedResponse>> GetDetailedSessionsAsync();
    Task<SessionDetailedResponse> GetSessionAsync(int sessionId);
}
