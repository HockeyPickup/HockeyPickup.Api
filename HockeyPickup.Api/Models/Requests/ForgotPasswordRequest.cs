using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public class ForgotPasswordRequest
{
    [Required]
    [Description("Frontend URL for password reset link")]
    [MaxLength(2048)]
    [Url]
    [DataType(DataType.Url)]
    [JsonPropertyName("FrontendUrl")]
    [JsonProperty(nameof(FrontendUrl), Required = Required.Always)]
    public required string FrontendUrl { get; init; } = string.Empty;

    [Required]
    [Description("Email address for password reset")]
    [MaxLength(256)]
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [JsonPropertyName("Email")]
    [JsonProperty(nameof(Email), Required = Required.Always)]
    public required string Email { get; set; } = string.Empty;
}
