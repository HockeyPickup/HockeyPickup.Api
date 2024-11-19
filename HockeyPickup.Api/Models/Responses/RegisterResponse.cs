using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Responses;

public record ConfirmEmailResponse
{
    [Required]
    [Description("Indicates if email confirmation was successful")]
    [JsonPropertyName("Success")]
    [JsonProperty(nameof(Success), Required = Required.Always)]
    public required bool Success { get; init; }

    [Required]
    [Description("Response message describing the confirmation result")]
    [MaxLength(1024)]
    [DataType(DataType.Text)]
    [JsonPropertyName("Message")]
    [JsonProperty(nameof(Message), Required = Required.Always)]
    public required string Message { get; init; } = string.Empty;
}
