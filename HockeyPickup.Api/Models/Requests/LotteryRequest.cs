using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

[GraphQLName("LotteryEnterRequest")]
public class LotteryEnterRequest
{
    [Required]
    [Description("Session identifier")]
    [JsonPropertyName("SessionId")]
    [JsonProperty(nameof(SessionId), Required = Required.Always)]
    [GraphQLName("SessionId")]
    [GraphQLDescription("Session identifier")]
    public required int SessionId { get; set; }
}

[GraphQLName("LotteryWithdrawRequest")]
public class LotteryWithdrawRequest
{
    [Required]
    [Description("Session identifier")]
    [JsonPropertyName("SessionId")]
    [JsonProperty(nameof(SessionId), Required = Required.Always)]
    [GraphQLName("SessionId")]
    [GraphQLDescription("Session identifier")]
    public required int SessionId { get; set; }
}
