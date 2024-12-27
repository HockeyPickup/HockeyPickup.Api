using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;
using JsonIgnoreAttribute = System.Text.Json.Serialization.JsonIgnoreAttribute;

namespace HockeyPickup.Api.Models.Requests;

public class CreateSessionRequest
{
    private DateTime _sessionDate;

    [Required]
    [Description("Date and time when the session is scheduled")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("SessionDate")]
    [JsonProperty(nameof(SessionDate), Required = Required.Always)]
    public required DateTime SessionDate
    {
        get => _sessionDate;
        set
        {
            // If incoming date is UTC, convert to Pacific Time
            if (value.Kind == DateTimeKind.Utc)
            {
                var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
                _sessionDate = TimeZoneInfo.ConvertTimeFromUtc(value, pacificZone);
            }
            else
            {
                // If not UTC, assume it's already in Pacific Time
                _sessionDate = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
            }
        }
    }

    [Description("Notes about the session")]
    [DataType(DataType.MultilineText)]
    [MaxLength(4000)]
    [JsonPropertyName("Note")]
    [JsonProperty(nameof(Note), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; set; }

    [Required]
    [Description("Associated regular set identifier")]
    [JsonPropertyName("RegularSetId")]
    [JsonProperty(nameof(RegularSetId), Required = Required.Always)]
    public required int RegularSetId { get; set; }

    [Description("Minimum number of days before session to allow buying")]
    [Range(0, 365)]
    [JsonPropertyName("BuyDayMinimum")]
    [JsonProperty(nameof(BuyDayMinimum), Required = Required.Always)]
    public required int BuyDayMinimum { get; set; }

    [Description("Cost of the session")]
    [Range(0, 1000)]
    [DataType(DataType.Currency)]
    [JsonPropertyName("Cost")]
    [JsonProperty(nameof(Cost), Required = Required.Always)]
    public required decimal Cost { get; set; }
}

public class UpdateSessionRequest : CreateSessionRequest
{
    [Required]
    [Description("Unique identifier for the session")]
    [JsonPropertyName("SessionId")]
    [JsonProperty(nameof(SessionId), Required = Required.Always)]
    public required int SessionId { get; set; }
}
