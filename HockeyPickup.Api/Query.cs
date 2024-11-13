using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Data.Repositories;

namespace HockeyPickup.Api.GraphQL;

// Type classes for GraphQL
public class UserBasicType : ObjectType<UserBasicResponse>
{
    protected override void Configure(IObjectTypeDescriptor<UserBasicResponse> descriptor)
    {
        descriptor.Name("User");
        // Fields will be automatically mapped
    }
}

public class UserDetailedType : ObjectType<UserDetailedResponse>
{
    protected override void Configure(IObjectTypeDescriptor<UserDetailedResponse> descriptor)
    {
        descriptor.Name("UserDetailed");
        // Fields will be automatically mapped
    }
}

public class UserResponseType : UnionType
{
    protected override void Configure(IUnionTypeDescriptor descriptor)
    {
        descriptor
            .Name("UserResponse")
            .Description("Represents either a basic or detailed user response")
            .Type<UserBasicType>()
            .Type<UserDetailedType>();
    }
}

public class Query
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public Query(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    [GraphQLDescription("Retrieves a list of active users. Returns detailed information for admins.")]
    [GraphQLType(typeof(ListType<UserResponseType>))]
    public async Task<IEnumerable<object>> Users([Service] IUserRepository userRepository)
    {
        var isAdmin = _httpContextAccessor.HttpContext?.User?.IsInRole("Admin") ?? false;

        if (isAdmin)
        {
            return await userRepository.GetDetailedUsersAsync();
        }

        return await userRepository.GetBasicUsersAsync();
    }
}
