using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using Newtonsoft.Json;

namespace HockeyPickup.Api.Models.Responses;

[GraphQLName("User")]
public class UserDetailedResponse
{
    [Required]
    [Description("Unique identifier for the user")]
    [MaxLength(128)]
    [DataType(DataType.Text)]
    [JsonPropertyName("Id")]
    [JsonProperty(nameof(Id), Required = Required.Always)]
    [GraphQLName("Id")]
    [GraphQLDescription("Unique identifier for the user")]
    public required string Id { get; set; }

    [Required]
    [Description("UserName of the user")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("UserName")]
    [JsonProperty(nameof(UserName), Required = Required.Always)]
    [GraphQLName("UserName")]
    [GraphQLDescription("UserName of the user")]
    public required string UserName { get; set; }

    [Description("Email address of the user")]
    [MaxLength(256)]
    [DataType(DataType.EmailAddress)]
    [JsonPropertyName("Email")]
    [JsonProperty(nameof(Email), Required = Required.Default)]
    [GraphQLName("Email")]
    [GraphQLDescription("Email address of the user")]
    public string? Email { get; set; }

    [Description("First name of the user")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("FirstName")]
    [JsonProperty(nameof(FirstName), Required = Required.Default)]
    [GraphQLName("FirstName")]
    [GraphQLDescription("First name of the user")]
    public string? FirstName { get; set; }

    [Description("Last name of the user")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("LastName")]
    [JsonProperty(nameof(LastName), Required = Required.Default)]
    [GraphQLName("LastName")]
    [GraphQLDescription("Last name of the user")]
    public string? LastName { get; set; }

    [Required]
    [Description("Indicates if user account is active")]
    [JsonPropertyName("Active")]
    [JsonProperty(nameof(Active), Required = Required.Always)]
    [GraphQLName("Active")]
    [GraphQLDescription("Indicates if the user account is active")]
    public required bool Active { get; set; }

    [Required]
    [Description("Indicates if the user has preferred status")]
    [JsonPropertyName("Preferred")]
    [JsonProperty(nameof(Preferred), Required = Required.Always)]
    [GraphQLName("Preferred")]
    [GraphQLDescription("Indicates if the user has preferred status")]
    public required bool Preferred { get; set; }

    [Required]
    [Description("Indicates if the user has preferred plus status")]
    [JsonPropertyName("PreferredPlus")]
    [JsonProperty(nameof(PreferredPlus), Required = Required.Always)]
    [GraphQLName("PreferredPlus")]
    [GraphQLDescription("Indicates if the user has preferred plus status")]
    public required bool PreferredPlus { get; set; }

    [Description("User's notification preferences")]
    [JsonPropertyName("NotificationPreference")]
    [JsonProperty(nameof(NotificationPreference), Required = Required.Default)]
    [GraphQLName("NotificationPreference")]
    [GraphQLDescription("User's notification preferences")]
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<NotificationPreference>))]
    public NotificationPreference? NotificationPreference { get; set; }

    [Description("User's position preferences")]
    [JsonPropertyName("PositionPreference")]
    [JsonProperty(nameof(PositionPreference), Required = Required.Default)]
    [GraphQLName("PositionPreference")]
    [GraphQLDescription("User's position preferences")]
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<PositionPreference>))]
    public PositionPreference? PositionPreference { get; set; }

    [Description("Emergency contact name")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("EmergencyName")]
    [JsonProperty(nameof(EmergencyName), Required = Required.Default)]
    [GraphQLName("EmergencyName")]
    [GraphQLDescription("Emergency contact name")]
    public string? EmergencyName { get; set; }

    [Description("Emergency contact phone number")]
    [Phone]
    [MaxLength(20)]
    [DataType(DataType.PhoneNumber)]
    [JsonPropertyName("EmergencyPhone")]
    [JsonProperty(nameof(EmergencyPhone), Required = Required.Default)]
    [GraphQLName("EmergencyPhone")]
    [GraphQLDescription("Emergency contact phone number")]
    public string? EmergencyPhone { get; set; }

    [Required]
    [Description("Jersey Number")]
    [Range(0, 98)]
    [JsonPropertyName("JerseyNumber")]
    [JsonProperty(nameof(JerseyNumber), Required = Required.Always)]
    [GraphQLName("JerseyNumber")]
    [GraphQLDescription("Jersey Number")]
    public int JerseyNumber { get; set; }

    [Required]
    [Description("Indicates if user has Locker Room 13 access")]
    [JsonPropertyName("LockerRoom13")]
    [JsonProperty(nameof(LockerRoom13), Required = Required.Always)]
    [GraphQLName("LockerRoom13")]
    [GraphQLDescription("Indicates if user has Locker Room 13 access")]
    public bool LockerRoom13 { get; set; }

    [Description("Profile Photo Url")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("PhotoUrl")]
    [JsonProperty(nameof(PhotoUrl), Required = Required.Default)]
    [GraphQLName("PhotoUrl")]
    [GraphQLDescription("Profile Photo Url")]
    public string? PhotoUrl { get; set; }

    [Description("User's shoot preference")]
    [JsonPropertyName("Shoots")]
    [JsonProperty(nameof(Shoots), Required = Required.Default)]
    [GraphQLName("Shoots")]
    [GraphQLDescription("User's shoot preference")]
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<ShootPreference>))]
    public ShootPreference? Shoots { get; set; }

    [Description("Date and time when lockout ends")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("DateCreated")]
    [JsonProperty(nameof(DateCreated), Required = Required.Default)]
    [GraphQLName("DateCreated")]
    [GraphQLDescription("User account creation date")]
    public DateTime? DateCreated { get; set; }

    [Description("Roles of user")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("Roles")]
    [JsonProperty(nameof(Roles), Required = Required.Default)]
    [GraphQLName("Roles")]
    [GraphQLDescription("Roles of user")]
    public string[] Roles { get; set; } = [];

    [Required]
    [Description("User's rating")]
    [Range(0, 10)]
    [JsonPropertyName("Rating")]
    [JsonProperty(nameof(Rating), Required = Required.Always)]
    [GraphQLName("Rating")]
    [GraphQLDescription("User's rating")]
    public required decimal Rating { get; set; }

    [Description("User's payment methods")]
    [JsonPropertyName("PaymentMethods")]
    [JsonProperty(nameof(PaymentMethods))]
    [GraphQLName("PaymentMethods")]
    [GraphQLDescription("User's payment methods")]
    public ICollection<UserPaymentMethodResponse>? PaymentMethods { get; set; }
}

[GraphQLName("LockerRoom13Players")]
public class LockerRoom13Players
{
    [Required]
    [Description("Unique identifier for the user")]
    [MaxLength(128)]
    [DataType(DataType.Text)]
    [JsonPropertyName("Id")]
    [JsonProperty(nameof(Id), Required = Required.Always)]
    [GraphQLName("Id")]
    [GraphQLDescription("Unique identifier for the user")]
    public required string Id { get; set; }

    [Required]
    [Description("UserName of the user")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("UserName")]
    [JsonProperty(nameof(UserName), Required = Required.Always)]
    [GraphQLName("UserName")]
    [GraphQLDescription("UserName of the user")]
    public required string UserName { get; set; }

    [Description("Email address of the user")]
    [MaxLength(256)]
    [DataType(DataType.EmailAddress)]
    [JsonPropertyName("Email")]
    [JsonProperty(nameof(Email), Required = Required.Default)]
    [GraphQLName("Email")]
    [GraphQLDescription("Email address of the user")]
    public string? Email { get; set; }

    [Description("First name of the user")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("FirstName")]
    [JsonProperty(nameof(FirstName), Required = Required.Default)]
    [GraphQLName("FirstName")]
    [GraphQLDescription("First name of the user")]
    public string? FirstName { get; set; }

    [Description("Last name of the user")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("LastName")]
    [JsonProperty(nameof(LastName), Required = Required.Default)]
    [GraphQLName("LastName")]
    [GraphQLDescription("Last name of the user")]
    public string? LastName { get; set; }

    [Required]
    [Description("Indicates if user account is active")]
    [JsonPropertyName("Active")]
    [JsonProperty(nameof(Active), Required = Required.Always)]
    [GraphQLName("Active")]
    [GraphQLDescription("Indicates if the user account is active")]
    public required bool Active { get; set; }

    [Required]
    [Description("Indicates if the user has preferred status")]
    [JsonPropertyName("Preferred")]
    [JsonProperty(nameof(Preferred), Required = Required.Always)]
    [GraphQLName("Preferred")]
    [GraphQLDescription("Indicates if the user has preferred status")]
    public required bool Preferred { get; set; }

    [Required]
    [Description("Indicates if the user has preferred plus status")]
    [JsonPropertyName("PreferredPlus")]
    [JsonProperty(nameof(PreferredPlus), Required = Required.Always)]
    [GraphQLName("PreferredPlus")]
    [GraphQLDescription("Indicates if the user has preferred plus status")]
    public required bool PreferredPlus { get; set; }

    [Required]
    [Description("Indicates if user has Locker Room 13 access")]
    [JsonPropertyName("LockerRoom13")]
    [JsonProperty(nameof(LockerRoom13), Required = Required.Always)]
    [GraphQLName("LockerRoom13")]
    [GraphQLDescription("Indicates if user has Locker Room 13 access")]
    public bool LockerRoom13 { get; set; }


    [Required]
    [Description("Player's status in the roster")]
    [JsonPropertyName("PlayerStatus")]
    [JsonProperty(nameof(PlayerStatus), Required = Required.Always)]
    [GraphQLName("PlayerStatus")]
    [GraphQLDescription("Player's status in the roster")]
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<PlayerStatus>))]
    public required PlayerStatus PlayerStatus { get; set; }

}

[GraphQLName("LockerRoom13")]
public class LockerRoom13Response
{
    [Required]
    [Description("Unique identifier for the session")]
    [JsonPropertyName("SessionId")]
    [JsonProperty(nameof(SessionId), Required = Required.Always)]
    [GraphQLName("SessionId")]
    [GraphQLDescription("Unique identifier for the session")]
    public required int SessionId { get; set; }

    [Required]
    [Description("Date and time when the session is scheduled")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("SessionDate")]
    [JsonProperty(nameof(SessionDate), Required = Required.Always)]
    [GraphQLName("SessionDate")]
    [GraphQLDescription("Date and time when the session is scheduled")]
    public required DateTime SessionDate { get; set; }

    [Required]
    [Description("List of players in LockerRoom13")]
    [JsonPropertyName("LockerRoom13Players")]
    [JsonProperty(nameof(LockerRoom13Players), Required = Required.Always)]
    [GraphQLName("LockerRoom13Players")]
    [GraphQLDescription("List of players in LockerRoom13")]
    public required List<LockerRoom13Players> LockerRoom13Players { get; set; }
}
