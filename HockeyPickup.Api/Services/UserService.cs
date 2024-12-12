using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Requests;
using Microsoft.AspNetCore.Identity;
using System.Net;

namespace HockeyPickup.Api.Services;

public interface IUserService
{
    Task<ServiceResult<(User user, string[] roles)>> ValidateCredentialsAsync(string username, string password);
    Task<ServiceResult<AspNetUser>> RegisterUserAsync(RegisterRequest request);
    Task<ServiceResult> ConfirmEmailAsync(string email, string token);
    Task<ServiceResult> ChangePasswordAsync(string userId, ChangePasswordRequest request);
    Task<ServiceResult> InitiateForgotPasswordAsync(string email, string frontendurl);
    Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request);
    Task<ServiceResult> SaveUserAsync(string userId, SaveUserRequest request);
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

    public async Task<ServiceResult> SaveUserAsync(string userId, SaveUserRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ServiceResult.CreateFailure("User not found");

            UpdateUserProperties(user, request);

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return ServiceResult.CreateFailure(result.Errors.FirstOrDefault()?.Description ?? "Failed to save user");

            return ServiceResult.CreateSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user {UserId}", userId);
            return ServiceResult.CreateFailure($"An error occurred while saving user. Error: {ex.Message}");
        }
    }

    private void UpdateUserProperties(AspNetUser user, SaveUserRequest request)
    {
        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.PayPalEmail != null) user.PayPalEmail = request.PayPalEmail;
        if (request.VenmoAccount != null) user.VenmoAccount = request.VenmoAccount;
        if (request.MobileLast4 != null) user.MobileLast4 = request.MobileLast4;
        if (request.EmergencyName != null) user.EmergencyName = request.EmergencyName;
        if (request.EmergencyPhone != null) user.EmergencyPhone = request.EmergencyPhone;
        if (request.NotificationPreference.HasValue) user.NotificationPreference = (int) request.NotificationPreference.Value;
    }

    public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return ServiceResult.CreateFailure("Invalid reset attempt");

            if (!request.NewPassword.IsPasswordComplex())
                return ServiceResult.CreateFailure("Password does not meet complexity requirements");

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

            if (!result.Succeeded)
                return ServiceResult.CreateFailure(result.Errors.FirstOrDefault()?.Description ?? "Failed to reset password");

            // Since we're resetting the password, we might want to ensure the email is confirmed
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            return ServiceResult.CreateSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for {Email}", request.Email);
            return ServiceResult.CreateFailure($"An error occurred while resetting the password. Error: {ex.Message}");
        }
    }

    public async Task<ServiceResult> InitiateForgotPasswordAsync(string email, string frontendUrl)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return ServiceResult.CreateSuccess(); // Silent failure for security

            // Generate password reset token
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(resetToken);
            var resetUrl = $"{frontendUrl.TrimEnd('/')}/reset-password?token={encodedToken}&email={WebUtility.UrlEncode(user.Email)}";

            // Send forgot password email via service bus
            await _serviceBus.SendAsync(new ServiceBusCommsMessage
            {
                Metadata = new Dictionary<string, string>
            {
                { "Type", "ForgotPassword" },
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
                { "ResetUrl", resetUrl }
            }
            },
            subject: "ForgotPassword",
            correlationId: Guid.NewGuid().ToString(),
            queueName: _configuration["ServiceBusCommsQueueName"]);

            return ServiceResult.CreateSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating forgot password for {Email}", email);
            return ServiceResult.CreateFailure($"An error occurred while initiating forgot password. Error: {ex.Message}");
        }
    }

    public async Task<ServiceResult> ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ServiceResult.CreateFailure("User not found");

            // Note: With UserManager we can use its built-in password verification
            var checkPassword = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
            if (!checkPassword)
                return ServiceResult.CreateFailure("Current password is incorrect");

            if (request.NewPassword == request.CurrentPassword)
                return ServiceResult.CreateFailure("New password must be different from current password");

            if (!request.NewPassword.IsPasswordComplex())
                return ServiceResult.CreateFailure("Password does not meet complexity requirements");

            // Use UserManager's password change method
            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
                return ServiceResult.CreateFailure(result.Errors.FirstOrDefault()?.Description ?? "Failed to change password");

            await _userManager.UpdateAsync(user);

            return ServiceResult.CreateSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            return ServiceResult.CreateFailure($"An error occurred while changing the password. Error: {ex.Message}");
        }
    }

    public async Task<ServiceResult> ConfirmEmailAsync(string email, string token)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return ServiceResult.CreateFailure("Invalid verification link");

            if (user.EmailConfirmed)
                return ServiceResult.CreateFailure("Email is already confirmed");

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Email confirmation failed for user {Email}. Errors: {Errors}",
                    email, string.Join(", ", result.Errors.Select(e => e.Description)));

                return ServiceResult.CreateFailure("Email confirmation failed. The link may have expired.");
            }

            return ServiceResult.CreateSuccess("Email confirmed successfully. You can now log in.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming email for {Email}", email);
            return ServiceResult.CreateFailure($"An error occurred while confirming email. Error: {ex.Message}");
        }
    }

    public async Task<ServiceResult<AspNetUser>> RegisterUserAsync(RegisterRequest request)
    {
        try
        {
            var inviteCode = _configuration["RegistrationInviteCode"];
            if (string.IsNullOrEmpty(inviteCode) || request.InviteCode != inviteCode)
            {
                _logger.LogWarning("Invalid registration invite code: {InviteCode}", request.InviteCode);
                return ServiceResult<AspNetUser>.CreateFailure("Invalid registration invite code");
            }

            // Check if user exists
            var existingUser = await _userManager.FindByEmailAsync(request.Email);

            if (existingUser != null)
            {
                // If email is already confirmed, don't allow re-registration
                if (existingUser.EmailConfirmed)
                    return ServiceResult<AspNetUser>.CreateFailure("User with this email already exists");

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
                return ServiceResult<AspNetUser>.CreateFailure(string.Join(", ", result.Errors.Select(e => e.Description)));

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

                return ServiceResult<AspNetUser>.CreateSuccess(user, "Registration successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send registration confirmation message for {Email}", user.Email);
                return ServiceResult<AspNetUser>.CreateSuccess(user, "Registration successful but confirmation email could not be sent");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user with email {Email}", request.Email);
            return ServiceResult<AspNetUser>.CreateFailure($"An error occurred during registration. Error: {ex.Message}");
        }
    }

    public async Task<ServiceResult<(User user, string[] roles)>> ValidateCredentialsAsync(string username, string password)
    {
        try
        {
            var aspNetUser = await _userManager.FindByNameAsync(username);
            if (aspNetUser == null)
                return ServiceResult<(User user, string[] roles)>.CreateFailure("Invalid credentials");

            var result = await _signInManager.CheckPasswordSignInAsync(aspNetUser, password, false);
            if (!result.Succeeded)
                return ServiceResult<(User user, string[] roles)>.CreateFailure("Invalid credentials");

            if (!await _userManager.IsEmailConfirmedAsync(aspNetUser))
                return ServiceResult<(User user, string[] roles)>.CreateFailure("Email not confirmed");

            // Get user roles
            var roles = await _userManager.GetRolesAsync(aspNetUser);

            // Map to domain User
            var user = new User
            {
                Id = aspNetUser.Id,
                UserName = aspNetUser.UserName,
                Email = aspNetUser.Email,
                FirstName = aspNetUser.FirstName,
                LastName = aspNetUser.LastName,
                Preferred = aspNetUser.Preferred,
                PreferredPlus = aspNetUser.PreferredPlus,
                NotificationPreference = aspNetUser.NotificationPreference,
                Active = aspNetUser.Active,
                EmergencyName = aspNetUser.EmergencyName,
                EmergencyPhone = aspNetUser.EmergencyPhone,
                LockerRoom13 = aspNetUser.LockerRoom13,
                MobileLast4 = aspNetUser.MobileLast4,
                PayPalEmail = aspNetUser.PayPalEmail,
                VenmoAccount = aspNetUser.VenmoAccount,
                Rating = aspNetUser.GetSecureRating(),
                DateCreated = aspNetUser.DateCreated
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

            return ServiceResult<(User user, string[] roles)>.CreateSuccess((user, roles.ToArray()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating credentials for username {UserName}", username);
            return ServiceResult<(User user, string[] roles)>.CreateFailure($"An error occurred while validating credentials. Error: {ex.Message}");
        }
    }
}
