using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public class UpdateRosterPositionRequest
{
    [Required]
    [Description("Session ID")]
    [JsonPropertyName("SessionId")]
    [JsonProperty(nameof(SessionId), Required = Required.Always)]
    public required int SessionId { get; set; }

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
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<PositionPreference>))]
    public required PositionPreference NewPosition { get; set; }
}
