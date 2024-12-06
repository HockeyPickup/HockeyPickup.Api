using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Data.Repositories;

public interface IUserRepository
{
    Task<IEnumerable<UserDetailedResponse>> GetDetailedUsersAsync();
    Task<UserDetailedResponse> GetUserAsync(string userId);
}
