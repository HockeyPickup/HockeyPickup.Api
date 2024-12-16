using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public class DuplicateRegularSetRequest
{
    [Required]
    [Description("Regular Set identifier")]
    [JsonPropertyName("RegularSetId")]
    [JsonProperty(nameof(RegularSetId), Required = Required.Always)]
    public required int RegularSetId { get; set; }

    [Required]
    [Description("New Regular Set description")]
    [JsonPropertyName("Description")]
    [JsonProperty(nameof(Description), Required = Required.Always)]
    public required string Description { get; set; }
}
