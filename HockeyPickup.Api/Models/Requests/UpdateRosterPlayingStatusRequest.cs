using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using JsonIgnoreAttribute = System.Text.Json.Serialization.JsonIgnoreAttribute;

namespace HockeyPickup.Api.Models.Requests;

public class UpdateRosterPlayingStatusRequest
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
    [Description("Playing status true | false")]
    [JsonPropertyName("IsPlaying")]
    [JsonProperty(nameof(IsPlaying), Required = Required.Always)]
    public required bool IsPlaying { get; set; }

    [Description("Notes about the session")]
    [DataType(DataType.MultilineText)]
    [MaxLength(4000)]
    [JsonPropertyName("Note")]
    [JsonProperty(nameof(Note), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; set; }
}
