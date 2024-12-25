using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace HockeyPickup.Api.Models.Requests;

public class UpdateRegularSetRequest
{
    [Required]
    [Description("Regular Set identifier")]
    [JsonPropertyName("RegularSetId")]
    [JsonProperty(nameof(RegularSetId), Required = Required.Always)]
    public required int RegularSetId { get; set; }

    [Required]
    [Description("Description of the regular set")]
    [JsonPropertyName("Description")]
    [JsonProperty(nameof(Description), Required = Required.Always)]
    public required string Description { get; set; }

    [Required]
    [Description("Day of the week (0 = Sunday, 6 = Saturday)")]
    [Range(0, 6)]
    [JsonPropertyName("DayOfWeek")]
    [JsonProperty(nameof(DayOfWeek), Required = Required.Always)]
    public required int DayOfWeek { get; set; }

    [Required]
    [Description("Indicates if the regular set is archived")]
    [JsonPropertyName("Archived")]
    [JsonProperty(nameof(Archived), Required = Required.Always)]
    public required bool Archived { get; set; }
}
