using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Models.Responses;
using HotChocolate.Authorization;

namespace HockeyPickup.Api.GraphQL.Queries;

[ExtendObjectType("Query")]
public class SessionQueries
{
    [Authorize]
    [GraphQLDescription("Retrieves a list of sessions.")]
    [GraphQLType(typeof(IEnumerable<SessionBasicResponse>))]
    [GraphQLName("Sessions")]
    public async Task<IEnumerable<SessionBasicResponse>> GetSessions([Service] ISessionRepository sessionRepository)
    {
        return await sessionRepository.GetBasicSessionsAsync();
    }

    [Authorize]
    [GraphQLDescription("Retrieves a specific session by ID with all details.")]
    [GraphQLType(typeof(SessionDetailedResponse))]
    [GraphQLName("Session")]
    public async Task<SessionDetailedResponse> GetSession([GraphQLName("SessionId")] [GraphQLDescription("The ID of the session to retrieve")] int SessionId, [Service] ISessionRepository sessionRepository)
    {
        return await sessionRepository.GetSessionAsync(SessionId);
    }
}
