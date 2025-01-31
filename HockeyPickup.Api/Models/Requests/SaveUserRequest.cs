using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using JsonIgnoreAttribute = System.Text.Json.Serialization.JsonIgnoreAttribute;

namespace HockeyPickup.Api.Models.Requests;

public class SaveUserRequest
{
    [Description("User's first name")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("FirstName")]
    [JsonProperty(nameof(FirstName), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstName { get; set; }

    [Description("User's last name")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("LastName")]
    [JsonProperty(nameof(LastName), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastName { get; set; }

    [Description("Emergency contact name")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("EmergencyName")]
    [JsonProperty(nameof(EmergencyName), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmergencyName { get; set; }

    [Description("Emergency contact phone number")]
    [MaxLength(20)]
    [DataType(DataType.PhoneNumber)]
    [JsonPropertyName("EmergencyPhone")]
    [JsonProperty(nameof(EmergencyPhone), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmergencyPhone { get; set; }

    [Required]
    [Description("Jersey Number")]
    [Range(0, 98)]
    [JsonPropertyName("JerseyNumber")]
    [JsonProperty(nameof(JerseyNumber), Required = Required.Always)]
    public int JerseyNumber { get; set; }

    [Description("User's notification preference setting")]
    [JsonPropertyName("NotificationPreference")]
    [JsonProperty(nameof(NotificationPreference), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<NotificationPreference>))]
    public NotificationPreference? NotificationPreference { get; set; }

    [Description("User's position preference setting")]
    [JsonPropertyName("PositionPreference")]
    [JsonProperty(nameof(PositionPreference), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<PositionPreference>))]
    public PositionPreference? PositionPreference { get; set; }
}

public class SaveUserRequestEx : SaveUserRequest
{
    [Description("Whether the user account is active")]
    [JsonPropertyName("Active")]
    [JsonProperty(nameof(Active), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Active { get; set; }

    [Description("Whether the user has preferred status")]
    [JsonPropertyName("Preferred")]
    [JsonProperty(nameof(Preferred), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Preferred { get; set; }

    [Description("Whether the user has preferred plus status")]
    [JsonPropertyName("PreferredPlus")]
    [JsonProperty(nameof(PreferredPlus), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PreferredPlus { get; set; }

    [Description("Whether the user has Locker Room 13 access")]
    [JsonPropertyName("LockerRoom13")]
    [JsonProperty(nameof(LockerRoom13), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LockerRoom13 { get; set; }

    [Description("User's rating")]
    [Range(0, 10)]
    [DataType(DataType.Currency)]
    [JsonPropertyName("Rating")]
    [JsonProperty(nameof(Rating), Required = Required.Default)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Rating { get; set; }
}

public class AdminUserUpdateRequest : SaveUserRequestEx
{
    [Required]
    [Description("ID of the user to update")]
    [MaxLength(128)]
    [DataType(DataType.Text)]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId), Required = Required.Always)]
    public required string UserId { get; set; }
}
