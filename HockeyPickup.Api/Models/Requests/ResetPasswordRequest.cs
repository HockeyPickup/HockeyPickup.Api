using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public static class RequestConstants
{
    public const string PasswordRegEx = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d])[^\s]{8,}$";
}

public class ResetPasswordRequest
{
    [Required]
    [Description("Email address associated with reset request")]
    [MaxLength(256)]
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [JsonPropertyName("Email")]
    [JsonProperty(nameof(Email), Required = Required.Always)]
    public required string Email { get; set; } = string.Empty;

    [Required]
    [Description("Password reset token from email")]
    [MaxLength(1024)]
    [DataType(DataType.Text)]
    [JsonPropertyName("Token")]
    [JsonProperty(nameof(Token), Required = Required.Always)]
    public required string Token { get; set; } = string.Empty;

    [Required]
    [Description("New password to set")]
    [MinLength(8)]
    [MaxLength(100)]
    [DataType(DataType.Password)]
    [RegularExpression(RequestConstants.PasswordRegEx,
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number and one special character")]
    [JsonPropertyName("NewPassword")]
    [JsonProperty(nameof(NewPassword), Required = Required.Always)]
    public required string NewPassword { get; set; } = string.Empty;

    [Required]
    [Description("Confirmation of new password")]
    [MaxLength(100)]
    [DataType(DataType.Password)]
    [Compare("NewPassword")]
    [JsonPropertyName("ConfirmPassword")]
    [JsonProperty(nameof(ConfirmPassword), Required = Required.Always)]
    public required string ConfirmPassword { get; set; } = string.Empty;
}
