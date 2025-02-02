using HockeyPickup.Api.Data.Entities;
using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

[GraphQLName("BuyRequest")]
public class BuyRequest
{
    [Required]
    [Description("Session identifier")]
    [JsonPropertyName("SessionId")]
    [JsonProperty(nameof(SessionId), Required = Required.Always)]
    [GraphQLName("SessionId")]
    [GraphQLDescription("Session identifier")]
    public required int SessionId { get; set; }

    [Description("Buyer's note for the BuySell")]
    [MaxLength(4000)]
    [DataType(DataType.MultilineText)]
    [JsonPropertyName("Note")]
    [JsonProperty(nameof(Note))]
    [GraphQLName("Note")]
    [GraphQLDescription("Buyer's note for the BuySell")]
    public string? Note { get; set; }
}

[GraphQLName("SellRequest")]
public class SellRequest
{
    [Required]
    [Description("Session identifier")]
    [JsonPropertyName("SessionId")]
    [JsonProperty(nameof(SessionId), Required = Required.Always)]
    [GraphQLName("SessionId")]
    [GraphQLDescription("Session identifier")]
    public required int SessionId { get; set; }

    [Description("Seller's note for the BuySell")]
    [MaxLength(4000)]
    [DataType(DataType.MultilineText)]
    [JsonPropertyName("Note")]
    [JsonProperty(nameof(Note))]
    [GraphQLName("Note")]
    [GraphQLDescription("Seller's note for the BuySell")]
    public string? Note { get; set; }
}
