using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public class UpdateRegularTeamRequest
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
    [Description("New team assignment (1: Light, 2: Dark)")]
    [Range(1, 2)]
    [JsonPropertyName("NewTeamAssignment")]
    [JsonProperty(nameof(NewTeamAssignment), Required = Required.Always)]
    public required int NewTeamAssignment { get; set; }
}
