using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Domain;
using Microsoft.AspNetCore.Identity;

namespace HockeyPickup.Api.Services;

public interface IUserService
{
    Task<(User user, string[] roles)?> ValidateCredentialsAsync(string username, string password);
}

public class UserService : IUserService
{
    private readonly UserManager<AspNetUser> _userManager;
    private readonly SignInManager<AspNetUser> _signInManager;
    private readonly IServiceBus _serviceBus;
    private readonly IConfiguration _configuration;

    public UserService(UserManager<AspNetUser> userManager, SignInManager<AspNetUser> signInManager, IServiceBus serviceBus, IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _serviceBus = serviceBus;
        _configuration = configuration;
    }

    public async Task<(User user, string[] roles)?> ValidateCredentialsAsync(string username, string password)
    {
        var aspNetUser = await _userManager.FindByNameAsync(username);
        if (aspNetUser == null)
            return null;

        var result = await _signInManager.CheckPasswordSignInAsync(aspNetUser, password, false);
        if (result.Succeeded)
        {
            if (!await _userManager.IsEmailConfirmedAsync(aspNetUser))
                throw new InvalidOperationException("Email not confirmed");

            // Get user roles
            var roles = await _userManager.GetRolesAsync(aspNetUser);

            // Map to domain User
            var user = new User
            {
                Id = aspNetUser.Id,
                Username = aspNetUser.UserName,
                Email = aspNetUser.Email
            };

            // Post a ServiceBus message
            await _serviceBus.SendAsync(new ServiceBusCommsMessage
            {
                Metadata = new Dictionary<string, string>
                    {
                        { "Type", "SignedIn" },
                        { "CommunicationEventId", Guid.NewGuid().ToString() }
                    },
                CommunicationMethod = new Dictionary<string, string>
                    {
                        { "Email", user.Email }
                    },
                RelatedEntities = [],
                MessageData = [],
            },
            subject: "SignedIn",
            correlationId: Guid.NewGuid().ToString(),
            queueName: _configuration["ServiceBusCommsQueueName"]);

            return (user, roles.ToArray());
        }

        return null;
    }
}
