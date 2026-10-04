using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Responses;

[GraphQLName("SessionGoalie")]
public class SessionGoalie
{
    [Required]
    [Description("User Id of the goalie")]
    [MaxLength(128)]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId), Required = Required.Always)]
    [GraphQLName("UserId")]
    [GraphQLDescription("User Id of the goalie")]
    public required string UserId { get; set; }

    [Required]
    [Description("First name of the goalie")]
    [MaxLength(256)]
    [JsonPropertyName("FirstName")]
    [JsonProperty(nameof(FirstName), Required = Required.Always)]
    [GraphQLName("FirstName")]
    [GraphQLDescription("First name of the goalie")]
    public required string FirstName { get; set; }

    [Required]
    [Description("Last name of the goalie")]
    [MaxLength(256)]
    [JsonPropertyName("LastName")]
    [JsonProperty(nameof(LastName), Required = Required.Always)]
    [GraphQLName("LastName")]
    [GraphQLDescription("Last name of the goalie")]
    public required string LastName { get; set; }

    [Description("Profile photo url of the goalie")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("PhotoUrl")]
    [JsonProperty(nameof(PhotoUrl), Required = Required.Default)]
    [GraphQLName("PhotoUrl")]
    [GraphQLDescription("Profile photo url of the goalie")]
    public string? PhotoUrl { get; set; }

    [Required]
    [Description("Whether the goalie is currently playing in the session")]
    [JsonPropertyName("IsPlaying")]
    [JsonProperty(nameof(IsPlaying), Required = Required.Always)]
    [GraphQLName("IsPlaying")]
    [GraphQLDescription("Whether the goalie is currently playing in the session")]
    public required bool IsPlaying { get; set; }

    [Required]
    [Description("Date and time when the goalie joined the roster")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("JoinedDateTime")]
    [JsonProperty(nameof(JoinedDateTime), Required = Required.Always)]
    [GraphQLName("JoinedDateTime")]
    [GraphQLDescription("Date and time when the goalie joined the roster")]
    public required DateTime JoinedDateTime { get; set; }
}
