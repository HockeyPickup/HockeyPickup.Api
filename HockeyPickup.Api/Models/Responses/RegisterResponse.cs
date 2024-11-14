using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Responses;

public record RegisterResponse
{
    [Required]
    [Description("Indicates if registration was successful")]
    [JsonPropertyName("Success")]
    [JsonProperty(nameof(Success), Required = Required.Always)]
    public required bool Success { get; init; }

    [Required]
    [Description("Response message describing the registration result")]
    [MaxLength(1024)]
    [DataType(DataType.Text)]
    [JsonPropertyName("Message")]
    [JsonProperty(nameof(Message), Required = Required.Always)]
    public required string Message { get; init; } = string.Empty;

    [Description("Collection of error messages if registration failed")]
    [DataType("IEnumerable<string>")]
    [JsonPropertyName("Errors")]
    [JsonProperty(nameof(Errors), Required = Required.Default)]
    public IEnumerable<string> Errors { get; init; } = Enumerable.Empty<string>();
}

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
