using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Swashbuckle.AspNetCore.Annotations;

namespace HockeyPickup.Api.Models.Domain;

[SwaggerSchema]
public class ServiceBusCommsMessage
{
    [Required]
    [Description("Required message metadata (Type, etc)")]
    [JsonPropertyName("Metadata")]
    [JsonProperty(nameof(Metadata), Required = Required.Always)]
    public required Dictionary<string, string> Metadata { get; set; } = new();

    [Required]
    [Description("Communication method and destination (Email, SMS, etc)")]
    [JsonPropertyName("CommunicationMethod")]
    [JsonProperty(nameof(CommunicationMethod), Required = Required.Always)]
    public required Dictionary<string, string> CommunicationMethod { get; set; } = new();

    [Required]
    [Description("Related entity IDs (Email, SessionId, etc)")]
    [JsonPropertyName("RelatedEntities")]
    [JsonProperty(nameof(RelatedEntities), Required = Required.Always)]
    public required Dictionary<string, string> RelatedEntities { get; set; } = new();

    [Description("Type-specific message payload data")]
    [JsonPropertyName("MessageData")]
    [JsonProperty(nameof(MessageData), Required = Required.Default)]
    public Dictionary<string, string>? MessageData { get; set; }
}
