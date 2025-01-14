using HockeyPickup.Api.Data.Entities;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Responses;

[GraphQLName("PaymentMethod")]
public class UserPaymentMethodResponse
{
    [Required]
    [Description("Unique identifier for the payment method")]
    [JsonPropertyName("UserPaymentMethodId")]
    [JsonProperty(nameof(UserPaymentMethodId), Required = Required.Always)]
    [GraphQLName("UserPaymentMethodId")]
    [GraphQLDescription("Unique identifier for the payment method")]
    public required int UserPaymentMethodId { get; set; }

    [Required]
    [Description("Type of payment method")]
    [JsonPropertyName("MethodType")]
    [JsonProperty(nameof(MethodType), Required = Required.Always)]
    [GraphQLName("MethodType")]
    [GraphQLDescription("Type of payment method")]
    public required PaymentMethodType MethodType { get; set; }

    [Required]
    [Description("Payment identifier (email, username, etc.)")]
    [JsonPropertyName("Identifier")]
    [JsonProperty(nameof(Identifier), Required = Required.Always)]
    [GraphQLName("Identifier")]
    [GraphQLDescription("Payment identifier (email, username, etc.)")]
    public required string Identifier { get; set; }

    [Required]
    [Description("Order of preference for this payment method")]
    [JsonPropertyName("PreferenceOrder")]
    [JsonProperty(nameof(PreferenceOrder), Required = Required.Always)]
    [GraphQLName("PreferenceOrder")]
    [GraphQLDescription("Order of preference for this payment method")]
    public required int PreferenceOrder { get; set; }

    [Required]
    [Description("Whether this payment method is currently active")]
    [JsonPropertyName("IsActive")]
    [JsonProperty(nameof(IsActive), Required = Required.Always)]
    [GraphQLName("IsActive")]
    [GraphQLDescription("Whether this payment method is currently active")]
    public required bool IsActive { get; set; }
}
