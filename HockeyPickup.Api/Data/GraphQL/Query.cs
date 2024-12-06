using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Data.Repositories;

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

    [GraphQLDescription("Retrieves a list of active users.")]
    [GraphQLType(typeof(IEnumerable<UserDetailedResponse>))]
    [GraphQLName("UsersEx")]
    public async Task<IEnumerable<UserDetailedResponse>> UsersEx([Service] IUserRepository userRepository)
    {
        var detailedUsers = await userRepository.GetDetailedUsersAsync();
        return detailedUsers;
    }
}
