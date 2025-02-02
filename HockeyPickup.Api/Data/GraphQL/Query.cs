using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Data.Repositories;
using HotChocolate.Authorization;

namespace HockeyPickup.Api.Data.GraphQL;

public class Query
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<Query> _logger;

    public Query(IHttpContextAccessor httpContextAccessor, ILogger<Query> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    [Authorize]
    [GraphQLDescription("Retrieves a list of active users.")]
    [GraphQLType(typeof(IEnumerable<UserDetailedResponse>))]
    [GraphQLName("UsersEx")]
    public async Task<IEnumerable<UserDetailedResponse>> UsersEx([Service] IUserRepository userRepository)
    {
        return await userRepository.GetDetailedUsersAsync();
    }

    [GraphQLDescription("Retrieves a list of LockerRoom13 status for each upcoming session.")]
    [GraphQLType(typeof(IEnumerable<LockerRoom13Response>))]
    [GraphQLName("LockerRoom13")]
    public async Task<IEnumerable<LockerRoom13Response>> LockerRoom13([Service] IUserRepository userRepository)
    {
        return await userRepository.GetLockerRoom13SessionsAsync();
    }

    [Authorize]
    [GraphQLDescription("Retrieves a list of sessions.")]
    [GraphQLType(typeof(IEnumerable<SessionBasicResponse>))]
    [GraphQLName("Sessions")]
    public async Task<IEnumerable<SessionBasicResponse>> GetSessions([Service] ISessionRepository sessionRepository)
    {
        return await sessionRepository.GetBasicSessionsAsync();
    }

    [Authorize]
    [GraphQLDescription("Retrieves a specific session by Id with all details.")]
    [GraphQLType(typeof(SessionDetailedResponse))]
    [GraphQLName("Session")]
    public async Task<SessionDetailedResponse> GetSession([GraphQLName("SessionId")][GraphQLDescription("The Id of the session to retrieve")] int SessionId, [Service] ISessionRepository sessionRepository)
    {
        return await sessionRepository.GetSessionAsync(SessionId);
    }

    [Authorize]
    [GraphQLDescription("Retrieves all regular sets with their regular players")]
    [GraphQLType(typeof(IEnumerable<RegularSetDetailedResponse>))]
    [GraphQLName("RegularSets")]
    public async Task<IEnumerable<RegularSetDetailedResponse>> GetRegularSets([Service] IRegularRepository regularRepository)
    {
        return await regularRepository.GetRegularSetsAsync();
    }

    [Authorize]
    [GraphQLDescription("Retrieves a specific regular set by Id with all regular players")]
    [GraphQLType(typeof(RegularSetDetailedResponse))]
    [GraphQLName("RegularSet")]
    public async Task<RegularSetDetailedResponse> GetRegularSet([GraphQLName("RegularSetId")] [GraphQLDescription("The Id of the regular set to retrieve")] int regularSetId, [Service] IRegularRepository regularRepository)
    {
        return await regularRepository.GetRegularSetAsync(regularSetId);
    }

    [Authorize]
    [GraphQLDescription("Retrieves user statistics")]
    [GraphQLType(typeof(UserStatsResponse))]
    [GraphQLName("UserStats")]
    public async Task<UserStatsResponse> GetUserStats([GraphQLName("UserId")][GraphQLDescription("The Id of the user")] string userId, [Service] IUserRepository userRepository)
    {
        return await userRepository.GetUserStatsAsync(userId);
    }

    [Authorize]
    [GraphQLDescription("Retrieves user payment methods")]
    [GraphQLType(typeof(UserPaymentMethodResponse))]
    [GraphQLName("UserPaymentMethod")]
    public async Task<IEnumerable<UserPaymentMethodResponse>> GetPaymentMethods([GraphQLName("UserId")][GraphQLDescription("The Id of the user")] string userId, [Service] IUserRepository userRepository)
    {
        return await userRepository.GetUserPaymentMethodsAsync(userId);
    }
}
