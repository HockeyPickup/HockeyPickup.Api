using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public record ImpersonationRequest
{
    [Required]
    [Description("User Id of the target user to impersonate")]
    [MaxLength(128)]
    [DataType(DataType.Text)]
    [JsonPropertyName("TargetUserId")]
    [JsonProperty(nameof(TargetUserId), Required = Required.Always)]
    public required string TargetUserId { get; init; }
}
