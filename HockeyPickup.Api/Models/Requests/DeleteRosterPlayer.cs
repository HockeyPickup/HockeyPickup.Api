using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public class DeleteRosterPlayer
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
}
