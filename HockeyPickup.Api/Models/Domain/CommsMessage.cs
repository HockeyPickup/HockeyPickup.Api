using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Domain;

public class ServiceBusCommsMessage
{
    [Required]
    [Description("Required message metadata (Type, etc)")]
    [DataType("Dictionary<string, string>")]
    [MinLength(1)]
    [JsonPropertyName("Metadata")]
    [JsonProperty(nameof(Metadata), Required = Required.Always)]
    public required Dictionary<string, string> Metadata { get; set; } = new();

    [Required]
    [Description("Communication method and destination (Email, SMS, etc)")]
    [DataType("Dictionary<string, string>")]
    [MinLength(1)]
    [JsonPropertyName("CommunicationMethod")]
    [JsonProperty(nameof(CommunicationMethod), Required = Required.Always)]
    public required Dictionary<string, string> CommunicationMethod { get; set; } = new();

    [Required]
    [Description("Related entity IDs (Email, SessionId, etc)")]
    [DataType("Dictionary<string, string>")]
    [MinLength(1)]
    [JsonPropertyName("RelatedEntities")]
    [JsonProperty(nameof(RelatedEntities), Required = Required.Always)]
    public required Dictionary<string, string> RelatedEntities { get; set; } = new();

    [Description("Type-specific message payload data")]
    [DataType("Dictionary<string, string>")]
    [JsonPropertyName("MessageData")]
    [JsonProperty(nameof(MessageData), Required = Required.Default)]
    public Dictionary<string, string>? MessageData { get; set; }
}
