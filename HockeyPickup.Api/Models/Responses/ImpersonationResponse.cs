using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Responses;

public record ImpersonationResponse
{
    [Required]
    [Description("JWT token for impersonation session")]
    [MaxLength(2048)]
    [DataType(DataType.Text)]
    [JsonPropertyName("Token")]
    [JsonProperty(nameof(Token), Required = Required.Always)]
    public required string Token { get; init; }

    [Required]
    [Description("User ID of the impersonated user")]
    [MaxLength(128)]
    [DataType(DataType.Text)]
    [JsonPropertyName("ImpersonatedUserId")]
    [JsonProperty(nameof(ImpersonatedUserId), Required = Required.Always)]
    public required string ImpersonatedUserId { get; init; }

    [Required]
    [Description("User ID of the original admin user")]
    [MaxLength(128)]
    [DataType(DataType.Text)]
    [JsonPropertyName("OriginalUserId")]
    [JsonProperty(nameof(OriginalUserId), Required = Required.Always)]
    public required string OriginalUserId { get; init; }

    [Description("Details of the impersonated user")]
    [DataType("UserDetailedResponse")]
    [JsonPropertyName("ImpersonatedUser")]
    [JsonProperty(nameof(ImpersonatedUser))]
    public UserDetailedResponse? ImpersonatedUser { get; init; }

    [Required]
    [Description("Timestamp when impersonation started")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("StartTime")]
    [JsonProperty(nameof(StartTime), Required = Required.Always)]
    public required DateTime StartTime { get; init; }
}
