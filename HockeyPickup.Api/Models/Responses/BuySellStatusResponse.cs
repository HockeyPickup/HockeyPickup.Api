using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace HockeyPickup.Api.Models.Responses;

[GraphQLName("BuySellStatusResponse")]
public class BuySellStatusResponse
{
    [Required]
    [Description("Indicates if the action is allowed")]
    [JsonPropertyName("IsAllowed")]
    [JsonProperty(nameof(IsAllowed), Required = Required.Always)]
    [GraphQLName("IsAllowed")]
    [GraphQLDescription("Indicates if the action is allowed")]
    public required bool IsAllowed { get; set; }

    [Required]
    [Description("Explanation of why action is/isn't allowed")]
    [JsonPropertyName("Reason")]
    [JsonProperty(nameof(Reason), Required = Required.Always)]
    [GraphQLName("Reason")]
    [GraphQLDescription("Explanation of why action is/isn't allowed")]
    public required string Reason { get; set; }

    [Description("Time until action is allowed (if applicable)")]
    [JsonPropertyName("TimeUntilAllowed")]
    [JsonProperty(nameof(TimeUntilAllowed))]
    [GraphQLName("TimeUntilAllowed")]
    [GraphQLDescription("Time until action is allowed (if applicable)")]
    public TimeSpan? TimeUntilAllowed { get; set; }
}
