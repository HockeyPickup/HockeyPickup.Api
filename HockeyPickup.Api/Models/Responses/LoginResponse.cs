using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Responses;

public record LoginResponse
{
    [Required]
    [Description("JWT authentication token")]
    [MaxLength(2048)]
    [DataType(DataType.Text)]
    [JsonPropertyName("Token")]
    [JsonProperty(nameof(Token), Required = Required.Always)]
    public required string Token { get; init; }

    [Required]
    [Description("Token expiration date and time")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("Expiration")]
    [JsonProperty(nameof(Expiration), Required = Required.Always)]
    public required DateTime Expiration { get; init; }
}
