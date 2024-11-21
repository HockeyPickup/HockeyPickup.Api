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
    [GraphQLType(typeof(IEnumerable<UserBasicResponse>))]
    [GraphQLName("Users")]
    public async Task<IEnumerable<UserBasicResponse>> Users([Service] IUserRepository userRepository)
    {
        var basicUsers = await userRepository.GetBasicUsersAsync();
        return basicUsers;
    }

    [Authorize(Roles = ["Admin"])]
    [GraphQLDescription("Retrieves a list of active users with additional properties.")]
    [GraphQLType(typeof(IEnumerable<UserDetailedResponse>))]
    [GraphQLName("UsersEx")]
    public async Task<IEnumerable<UserDetailedResponse>> UsersEx([Service] IUserRepository userRepository)
    {
        var detailedUsers = await userRepository.GetDetailedUsersAsync();
        return detailedUsers;
    }
}
