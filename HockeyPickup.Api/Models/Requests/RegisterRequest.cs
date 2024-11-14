using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public record RegisterRequest
{
    [Required]
    [Description("Frontend URL for email confirmation link")]
    [MaxLength(2048)]
    [Url]
    [DataType(DataType.Url)]
    [JsonPropertyName("FrontendUrl")]
    [JsonProperty(nameof(FrontendUrl), Required = Required.Always)]
    public required string FrontendUrl { get; init; } = string.Empty;

    [Required]
    [Description("Email address for registration")]
    [MaxLength(256)]
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [JsonPropertyName("Email")]
    [JsonProperty(nameof(Email), Required = Required.Always)]
    public required string Email { get; init; } = string.Empty;

    [Required]
    [Description("User's password")]
    [MinLength(8)]
    [MaxLength(100)]
    [DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number and one special character")]
    [JsonPropertyName("Password")]
    [JsonProperty(nameof(Password), Required = Required.Always)]
    public required string Password { get; init; } = string.Empty;

    [Required]
    [Description("Confirmation of password")]
    [MaxLength(100)]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords don't match")]
    [JsonPropertyName("ConfirmPassword")]
    [JsonProperty(nameof(ConfirmPassword), Required = Required.Always)]
    public required string ConfirmPassword { get; init; } = string.Empty;

    [Required]
    [Description("User's first name")]
    [StringLength(50)]
    [DataType(DataType.Text)]
    [JsonPropertyName("FirstName")]
    [JsonProperty(nameof(FirstName), Required = Required.Always)]
    public required string FirstName { get; init; } = string.Empty;

    [Required]
    [Description("User's last name")]
    [StringLength(50)]
    [DataType(DataType.Text)]
    [JsonPropertyName("LastName")]
    [JsonProperty(nameof(LastName), Required = Required.Always)]
    public required string LastName { get; init; } = string.Empty;
}

public record ConfirmEmailRequest
{
    [Required]
    [Description("Email confirmation token")]
    [MaxLength(1024)]
    [DataType(DataType.Text)]
    [JsonPropertyName("Token")]
    [JsonProperty(nameof(Token), Required = Required.Always)]
    public required string Token { get; init; } = string.Empty;

    [Required]
    [Description("Email address to confirm")]
    [MaxLength(256)]
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [JsonPropertyName("Email")]
    [JsonProperty(nameof(Email), Required = Required.Always)]
    public required string Email { get; init; } = string.Empty;
}
