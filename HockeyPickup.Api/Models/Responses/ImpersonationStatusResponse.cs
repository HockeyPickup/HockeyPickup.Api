using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Responses;

public record ImpersonationStatusResponse
{
    [Required]
    [Description("Indicates if user is currently impersonating another user")]
    [DataType(DataType.Text)]
    [JsonPropertyName("IsImpersonating")]
    [JsonProperty(nameof(IsImpersonating), Required = Required.Always)]
    public required bool IsImpersonating { get; init; }

    [Description("Original admin user Id if impersonating")]
    [MaxLength(128)]
    [DataType(DataType.Text)]
    [JsonPropertyName("OriginalUserId")]
    [JsonProperty(nameof(OriginalUserId))]
    public string? OriginalUserId { get; init; }

    [Description("Currently impersonated user Id")]
    [MaxLength(128)]
    [DataType(DataType.Text)]
    [JsonPropertyName("ImpersonatedUserId")]
    [JsonProperty(nameof(ImpersonatedUserId))]
    public string? ImpersonatedUserId { get; init; }

    [Description("Start time of current impersonation session")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("StartTime")]
    [JsonProperty(nameof(StartTime))]
    public DateTime? StartTime { get; init; }
}
