using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public class ChangePasswordRequest
{
    [Required]
    [Description("User's current password")]
    [MinLength(8)]
    [MaxLength(100)]
    [DataType(DataType.Password)]
    [JsonPropertyName("CurrentPassword")]
    [JsonProperty(nameof(CurrentPassword), Required = Required.Always)]
    public required string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [Description("New password to set")]
    [MinLength(8)]
    [MaxLength(100)]
    [DataType(DataType.Password)]
    [JsonPropertyName("NewPassword")]
    [JsonProperty(nameof(NewPassword), Required = Required.Always)]
    public required string NewPassword { get; set; } = string.Empty;

    [Required]
    [Description("Confirmation of new password")]
    [MinLength(8)]
    [MaxLength(100)]
    [DataType(DataType.Password)]
    [Compare("NewPassword")]
    [JsonPropertyName("ConfirmNewPassword")]
    [JsonProperty(nameof(ConfirmNewPassword), Required = Required.Always)]
    public required string ConfirmNewPassword { get; set; } = string.Empty;
}
