using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Requests;
using Microsoft.AspNetCore.Identity;
using System.Net;

namespace HockeyPickup.Api.Services;

public interface IUserService
{
    Task<(User user, string[] roles)?> ValidateCredentialsAsync(string username, string password);
    Task<(bool success, string[] errors)> RegisterUserAsync(RegisterRequest request);
    Task<(bool success, string message)> ConfirmEmailAsync(string email, string token);
}

public class UserService : IUserService
{
    private readonly UserManager<AspNetUser> _userManager;
    private readonly SignInManager<AspNetUser> _signInManager;
    private readonly IServiceBus _serviceBus;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserService> _logger;

    public UserService(UserManager<AspNetUser> userManager, SignInManager<AspNetUser> signInManager, IServiceBus serviceBus, IConfiguration configuration, ILogger<UserService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _serviceBus = serviceBus;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(bool success, string message)> ConfirmEmailAsync(string email, string token)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return (false, "Invalid verification link");
        }

        if (user.EmailConfirmed)
        {
            return (false, "Email is already confirmed");
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Email confirmation failed for user {Email}. Errors: {Errors}",
                email, string.Join(", ", result.Errors.Select(e => e.Description)));

            return (false, "Email confirmation failed. The link may have expired.");
        }

        return (true, "Email confirmed successfully. You can now log in.");
    }

    public async Task<(bool success, string[] errors)> RegisterUserAsync(RegisterRequest request)
    {
        // Check if user exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);

        if (existingUser != null)
        {
            // If email is already confirmed, don't allow re-registration
            if (existingUser.EmailConfirmed)
            {
                return (false, new[] { "User with this email already exists" });
            }

            // Email exists but isn't confirmed - delete the old registration
            _logger.LogInformation("Removing unconfirmed registration for {Email} to allow re-registration", request.Email);
            await _userManager.DeleteAsync(existingUser);
        }

        // Create new user
        var user = new AspNetUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = false,
            LockoutEnabled = false,
            PayPalEmail = request.Email,
            NotificationPreference = (int) NotificationPreference.OnlyMyBuySell
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return (false, result.Errors.Select(e => e.Description).ToArray());
        }

        // Generate email confirmation token
        var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebUtility.UrlEncode(confirmationToken);
        var confirmationUrl = $"{request.FrontendUrl.TrimEnd('/')}/confirm-email?token={encodedToken}&email={WebUtility.UrlEncode(user.Email)}";

        try
        {
            // Send confirmation email via service bus
            await _serviceBus.SendAsync(new ServiceBusCommsMessage
            {
                Metadata = new Dictionary<string, string>
                {
                    { "Type", "RegisterConfirmation" },
                    { "CommunicationEventId", Guid.NewGuid().ToString() }
                },
                CommunicationMethod = new Dictionary<string, string>
                {
                    { "Email", user.Email }
                },
                RelatedEntities = new Dictionary<string, string>
                {
                    { "UserId", user.Id },
                    { "FirstName", user.FirstName },
                    { "LastName", user.LastName }
                },
                MessageData = new Dictionary<string, string>
                {
                    { "ConfirmationUrl", confirmationUrl }
                }
            },
            subject: "RegisterConfirmation",
            correlationId: Guid.NewGuid().ToString(),
            queueName: _configuration["ServiceBusCommsQueueName"]);

            return (true, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration confirmation message for {Email}", user.Email);
            return (true, new[] { "Registration successful but confirmation email could not be sent" });
        }
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
