using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public class UpdateRosterTeamRequest
{
    [Required]
    [Description("Session Id")]
    [JsonPropertyName("SessionId")]
    [JsonProperty(nameof(SessionId), Required = Required.Always)]
    public required int SessionId { get; set; }

    [Required]
    [Description("User Id")]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId), Required = Required.Always)]
    public required string UserId { get; set; } = string.Empty;

    [Required]
    [Description("New team assignment (0 for TBD, 1 for Light, 2 for Dark)")]
    [Range(0, 2)]
    [JsonPropertyName("NewTeamAssignment")]
    [JsonProperty(nameof(NewTeamAssignment), Required = Required.Always)]
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<TeamAssignment>))]
    public required TeamAssignment NewTeamAssignment { get; set; }
}
