using HockeyPickup.Api.Models.Domain;

namespace HockeyPickup.Api.Services;

public interface IUserService
{
    Task<(User user, string[] roles)?> ValidateCredentialsAsync(string username, string password);
}