using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Data.Repositories;

public interface IUserRepository
{
    Task<IEnumerable<UserBasicResponse>> GetBasicUsersAsync();
    Task<IEnumerable<UserDetailedResponse>> GetDetailedUsersAsync();
}