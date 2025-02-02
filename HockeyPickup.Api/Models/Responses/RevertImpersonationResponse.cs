using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Responses;

public record RevertImpersonationResponse
{
    [Required]
    [Description("New JWT token for original admin user")]
    [MaxLength(2048)]
    [DataType(DataType.Text)]
    [JsonPropertyName("Token")]
    [JsonProperty(nameof(Token), Required = Required.Always)]
    public required string Token { get; init; }

    [Required]
    [Description("User Id of the original admin user")]
    [MaxLength(128)]
    [DataType(DataType.Text)]
    [JsonPropertyName("OriginalUserId")]
    [JsonProperty(nameof(OriginalUserId), Required = Required.Always)]
    public required string OriginalUserId { get; init; }

    [Required]
    [Description("Timestamp when impersonation ended")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("EndTime")]
    [JsonProperty(nameof(EndTime), Required = Required.Always)]
    public required DateTime EndTime { get; init; }
}
