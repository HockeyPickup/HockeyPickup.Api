using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;

namespace HockeyPickup.Api.Models.Requests;

public class UserPaymentMethodRequest
{
    [Required]
    [Description("Type of payment method")]
    [EnumDataType(typeof(PaymentMethodType))]
    [JsonPropertyName("MethodType")]
    [JsonProperty(nameof(MethodType), Required = Required.Always)]
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<PaymentMethodType>))]
    public required PaymentMethodType MethodType { get; set; }

    [Required]
    [Description("Payment identifier (email, username, etc.)")]
    [MaxLength(256)]
    [JsonPropertyName("Identifier")]
    [JsonProperty(nameof(Identifier), Required = Required.Always)]
    public required string Identifier { get; set; }

    [Required]
    [Description("Order of preference for this payment method")]
    [Range(1, 100)]
    [JsonPropertyName("PreferenceOrder")]
    [JsonProperty(nameof(PreferenceOrder), Required = Required.Always)]
    public required int PreferenceOrder { get; set; }

    [Description("Whether this payment method is active")]
    [JsonPropertyName("IsActive")]
    [JsonProperty(nameof(IsActive))]
    public bool IsActive { get; set; } = true;
}
