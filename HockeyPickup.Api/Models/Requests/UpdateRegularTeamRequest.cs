using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;

namespace HockeyPickup.Api.Models.Requests;

public class UpdateRegularTeamRequest
{
    [Required]
    [Description("Regular Set Id")]
    [JsonPropertyName("RegularSetId")]
    [JsonProperty(nameof(RegularSetId), Required = Required.Always)]
    public required int RegularSetId { get; set; }

    [Required]
    [Description("User Id")]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId), Required = Required.Always)]
    public required string UserId { get; set; } = string.Empty;

    [Required]
    [Description("New team assignment (1: Light, 2: Dark)")]
    [Range(1, 2)]
    [JsonPropertyName("NewTeamAssignment")]
    [JsonProperty(nameof(NewTeamAssignment), Required = Required.Always)]
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<TeamAssignment>))]
    public required TeamAssignment NewTeamAssignment { get; set; }
}
