using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public record LoginRequest
{
    [Required]
    [Description("Username for login (email address)")]
    [MaxLength(256)]
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [JsonPropertyName("Username")]
    [JsonProperty(nameof(Username), Required = Required.Always)]
    public required string Username { get; init; }

    [Required]
    [Description("User's password")]
    [MinLength(8)]
    [MaxLength(100)]
    [DataType(DataType.Password)]
    [JsonPropertyName("Password")]
    [JsonProperty(nameof(Password), Required = Required.Always)]
    public required string Password { get; init; }
}
