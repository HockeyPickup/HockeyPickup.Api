using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;
using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Domain;

[ExcludeFromCodeCoverage]
public class User
{
    [Required]
    [Description("Unique identifier for the user")]
    [MaxLength(128)]
    [DataType(DataType.Text)]
    [JsonPropertyName("Id")]
    [JsonProperty(nameof(Id), Required = Required.Always)]
    public string Id { get; set; } = null!;

    [Description("User's email address")]
    [MaxLength(256)]
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [JsonPropertyName("Email")]
    [JsonProperty(nameof(Email), Required = Required.Default)]
    public string? Email { get; set; }

    [Description("User's username")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("UserName")]
    [JsonProperty(nameof(UserName), Required = Required.Default)]
    public string? UserName { get; set; }

    [Description("First name of the user")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("FirstName")]
    [JsonProperty(nameof(FirstName), Required = Required.Default)]
    public string? FirstName { get; set; }

    [Description("Last name of the user")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("LastName")]
    [JsonProperty(nameof(LastName), Required = Required.Default)]
    public string? LastName { get; set; }

    [Required]
    [Description("Indicates if email has been confirmed")]
    [JsonPropertyName("EmailConfirmed")]
    [JsonProperty(nameof(EmailConfirmed), Required = Required.Always)]
    public bool EmailConfirmed { get; set; }

    [Description("Hashed password")]
    [MaxLength(1024)]
    [DataType(DataType.Password)]
    [JsonPropertyName("PasswordHash")]
    [JsonProperty(nameof(PasswordHash), Required = Required.Default)]
    public string? PasswordHash { get; set; }

    [Description("Security stamp for user")]
    [MaxLength(1024)]
    [DataType(DataType.Text)]
    [JsonPropertyName("SecurityStamp")]
    [JsonProperty(nameof(SecurityStamp), Required = Required.Default)]
    public string? SecurityStamp { get; set; }

    [Description("User's phone number")]
    [Phone]
    [MaxLength(20)]
    [DataType(DataType.PhoneNumber)]
    [JsonPropertyName("PhoneNumber")]
    [JsonProperty(nameof(PhoneNumber), Required = Required.Default)]
    public string? PhoneNumber { get; set; }

    [Required]
    [Description("Indicates if phone number has been confirmed")]
    [JsonPropertyName("PhoneNumberConfirmed")]
    [JsonProperty(nameof(PhoneNumberConfirmed), Required = Required.Always)]
    public bool PhoneNumberConfirmed { get; set; }

    [Required]
    [Description("Indicates if two-factor authentication is enabled")]
    [JsonPropertyName("TwoFactorEnabled")]
    [JsonProperty(nameof(TwoFactorEnabled), Required = Required.Always)]
    public bool TwoFactorEnabled { get; set; }

    [Description("Date and time when lockout ends")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("LockoutEndDateUtc")]
    [JsonProperty(nameof(LockoutEndDateUtc), Required = Required.Default)]
    public DateTime? LockoutEndDateUtc { get; set; }

    [Required]
    [Description("Indicates if lockout is enabled")]
    [JsonPropertyName("LockoutEnabled")]
    [JsonProperty(nameof(LockoutEnabled), Required = Required.Always)]
    public bool LockoutEnabled { get; set; }

    [Required]
    [Description("Number of failed access attempts")]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("AccessFailedCount")]
    [JsonProperty(nameof(AccessFailedCount), Required = Required.Always)]
    public int AccessFailedCount { get; set; }

    [Required]
    [Description("User's notification preferences")]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("NotificationPreference")]
    [JsonProperty(nameof(NotificationPreference), Required = Required.Always)]
    public int NotificationPreference { get; set; }

    [Required]
    [Description("User's PayPal email address")]
    [MaxLength(256)]
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [JsonPropertyName("PayPalEmail")]
    [JsonProperty(nameof(PayPalEmail), Required = Required.Always)]
    public string PayPalEmail { get; set; } = null!;

    [Required]
    [Description("Indicates if user account is active")]
    [JsonPropertyName("Active")]
    [JsonProperty(nameof(Active), Required = Required.Always)]
    public bool Active { get; set; }

    [Required]
    [Description("Indicates if user has preferred status")]
    [JsonPropertyName("Preferred")]
    [JsonProperty(nameof(Preferred), Required = Required.Always)]
    public bool Preferred { get; set; }

    [Description("User's Venmo account")]
    [MaxLength(255)]
    [RegularExpression(@"^[^\\\./:\@\*\?\""<>\|]{1}[^\\/:\@\*\?\""<>\|]{0,254}$")]
    [DataType(DataType.Text)]
    [JsonPropertyName("VenmoAccount")]
    [JsonProperty(nameof(VenmoAccount), Required = Required.Default)]
    public string? VenmoAccount { get; set; }

    [Description("Last 4 digits of mobile number")]
    [MaxLength(4)]
    [RegularExpression(@"^(\d{4})$")]
    [DataType(DataType.Text)]
    [JsonPropertyName("MobileLast4")]
    [JsonProperty(nameof(MobileLast4), Required = Required.Default)]
    public string? MobileLast4 { get; set; }

    [Required]
    [Description("User's rating")]
    [Range(0, 5)]
    [DataType(DataType.Currency)]
    [JsonPropertyName("Rating")]
    [JsonProperty(nameof(Rating), Required = Required.Always)]
    public decimal Rating { get; set; }

    [Required]
    [Description("Indicates if user has preferred plus status")]
    [JsonPropertyName("PreferredPlus")]
    [JsonProperty(nameof(PreferredPlus), Required = Required.Always)]
    public bool PreferredPlus { get; set; }

    [Description("Emergency contact name")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("EmergencyName")]
    [JsonProperty(nameof(EmergencyName), Required = Required.Default)]
    public string? EmergencyName { get; set; }

    [Description("Emergency contact phone number")]
    [Phone]
    [MaxLength(20)]
    [DataType(DataType.PhoneNumber)]
    [JsonPropertyName("EmergencyPhone")]
    [JsonProperty(nameof(EmergencyPhone), Required = Required.Default)]
    public string? EmergencyPhone { get; set; }

    [Required]
    [Description("Indicates if user has Locker Room 13 access")]
    [JsonPropertyName("LockerRoom13")]
    [JsonProperty(nameof(LockerRoom13), Required = Required.Always)]
    public bool LockerRoom13 { get; set; }

    [Description("Date and time when lockout ends")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("DateCreated")]
    [JsonProperty(nameof(DateCreated), Required = Required.Default)]
    public DateTime? DateCreated { get; set; }

    [Description("Roles of user")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("Roles")]
    [JsonProperty(nameof(Roles), Required = Required.Default)]
    public string[] Roles { get; set; } = [];

    internal UserDetailedResponse ToUserDetailedResponse()
    {
        return new UserDetailedResponse
        {
            Id = Id,
            UserName = UserName,
            Email = Email,
            FirstName = FirstName,
            LastName = LastName,
            Active = Active,
            Preferred = Preferred,
            PreferredPlus = PreferredPlus,
            NotificationPreference = (NotificationPreference) NotificationPreference,
            VenmoAccount = VenmoAccount,
            PayPalEmail = PayPalEmail,
            MobileLast4 = MobileLast4,
            EmergencyName = EmergencyName,
            EmergencyPhone = EmergencyPhone,
            LockerRoom13 = LockerRoom13,
            DateCreated = DateCreated,
            Rating = Rating,
            Roles = Roles
        };
    }
}
