using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public class UpdateRegularPositionRequest
{
    [Required]
    [Description("Regular Set ID")]
    [JsonPropertyName("RegularSetId")]
    [JsonProperty(nameof(RegularSetId), Required = Required.Always)]
    public required int RegularSetId { get; set; }

    [Required]
    [Description("User ID")]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId), Required = Required.Always)]
    public required string UserId { get; set; } = string.Empty;

    [Required]
    [Description("New position (0: TBD, 1: Forward, 2: Defense)")]
    [Range(0, 2)]
    [JsonPropertyName("NewPosition")]
    [JsonProperty(nameof(NewPosition), Required = Required.Always)]
    public required int NewPosition { get; set; }
}
