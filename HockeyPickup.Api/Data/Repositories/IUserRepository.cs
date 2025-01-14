using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Data.Repositories;

public interface IUserRepository
{
    Task<IEnumerable<UserDetailedResponse>> GetDetailedUsersAsync();
    Task<UserDetailedResponse> GetUserAsync(string userId);
    Task<IEnumerable<LockerRoom13Response>> GetLockerRoom13SessionsAsync();
    Task<UserStatsResponse?> GetUserStatsAsync(string userId);
    Task<IEnumerable<UserPaymentMethodResponse>> GetUserPaymentMethodsAsync(string userId);
    Task<UserPaymentMethodResponse?> GetUserPaymentMethodAsync(string userId, int paymentMethodId);
    Task<UserPaymentMethodResponse> AddUserPaymentMethodAsync(string userId, UserPaymentMethod paymentMethod);
    Task<UserPaymentMethodResponse?> UpdateUserPaymentMethodAsync(string userId, UserPaymentMethod paymentMethod);
    Task<bool> DeleteUserPaymentMethodAsync(string userId, int paymentMethodId);
}
