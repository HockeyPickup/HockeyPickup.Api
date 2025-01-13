using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public class AddRegularRequest
{
    [Required]
    [Description("Regular set identifier")]
    [Range(1, int.MaxValue)]
    [JsonPropertyName("RegularSetId")]
    [JsonProperty(nameof(RegularSetId), Required = Required.Always)]
    public required int RegularSetId { get; set; }

    [Required]
    [Description("User identifier")]
    [MaxLength(128)]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId), Required = Required.Always)]
    public required string UserId { get; set; }

    [Required]
    [Description("Team assignment (1 for Light, 2 for Dark)")]
    [Range(1, 2)]
    [JsonPropertyName("TeamAssignment")]
    [JsonProperty(nameof(TeamAssignment), Required = Required.Always)]
    public required int TeamAssignment { get; set; }

    [Required]
    [Description("Position preference (0 for TBD, 1 for Forward, 2 for Defense)")]
    [Range(0, 2)]
    [JsonPropertyName("PositionPreference")]
    [JsonProperty(nameof(PositionPreference), Required = Required.Always)]
    public required int PositionPreference { get; set; }
}
