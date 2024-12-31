using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Responses;

public class PhotoResponse
{
    [Required]
    [Description("URL of the uploaded profile photo")]
    [DataType(DataType.Url)]
    [JsonPropertyName("PhotoUrl")]
    [JsonProperty(nameof(PhotoUrl), Required = Required.Always)]
    public required string PhotoUrl { get; set; }

    [Description("Date and time when the photo was last updated")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("UpdateDateTime")]
    [JsonProperty(nameof(UpdateDateTime), Required = Required.Always)]
    public required DateTime UpdateDateTime { get; set; }
}
