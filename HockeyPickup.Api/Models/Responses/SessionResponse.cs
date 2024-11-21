using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace HockeyPickup.Api.Models.Responses;

[GraphQLName("Session")]
public class SessionBasicResponse
{
    [Required]
    [Description("Unique identifier for the session")]
    [JsonPropertyName("SessionId")]
    [JsonProperty(nameof(SessionId), Required = Required.Always)]
    [GraphQLName("SessionId")]
    [GraphQLDescription("Unique identifier for the session")]
    public required int SessionId { get; set; }

    [Required]
    [Description("Date and time when the session was created")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("CreateDateTime")]
    [JsonProperty(nameof(CreateDateTime), Required = Required.Always)]
    [GraphQLName("CreateDateTime")]
    [GraphQLDescription("Date and time when the session was created")]
    public required DateTime CreateDateTime { get; set; }

    [Required]
    [Description("Date and time when the session was last updated")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("UpdateDateTime")]
    [JsonProperty(nameof(UpdateDateTime), Required = Required.Always)]
    [GraphQLName("UpdateDateTime")]
    [GraphQLDescription("Date and time when the session was last updated")]
    public required DateTime UpdateDateTime { get; set; }

    [Description("Additional notes about the session")]
    [DataType(DataType.MultilineText)]
    [JsonPropertyName("Note")]
    [JsonProperty(nameof(Note))]
    [GraphQLName("Note")]
    [GraphQLDescription("Additional notes about the session")]
    public string? Note { get; set; }

    [Required]
    [Description("Date and time when the session is scheduled")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("SessionDate")]
    [JsonProperty(nameof(SessionDate), Required = Required.Always)]
    [GraphQLName("SessionDate")]
    [GraphQLDescription("Date and time when the session is scheduled")]
    public required DateTime SessionDate { get; set; }

    [Description("Associated regular set identifier")]
    [JsonPropertyName("RegularSetId")]
    [JsonProperty(nameof(RegularSetId))]
    [GraphQLName("RegularSetId")]
    [GraphQLDescription("Associated regular set identifier")]
    public int? RegularSetId { get; set; }

    [Description("Minimum number of days before session to allow buying")]
    [Range(0, 365)]
    [JsonPropertyName("BuyDayMinimum")]
    [JsonProperty(nameof(BuyDayMinimum))]
    [GraphQLName("BuyDayMinimum")]
    [GraphQLDescription("Minimum number of days before session to allow buying")]
    public int? BuyDayMinimum { get; set; }
}

[GraphQLName("SessionDetailed")]
public class SessionDetailedResponse : SessionBasicResponse
{
    [Description("Buy/sell transactions associated with the session")]
    [JsonPropertyName("BuySells")]
    [JsonProperty(nameof(BuySells))]
    [GraphQLName("BuySells")]
    [GraphQLDescription("Buy/sell transactions associated with the session")]
    public ICollection<BuySellResponse>? BuySells { get; set; }

    [Description("Activity logs associated with the session")]
    [JsonPropertyName("ActivityLogs")]
    [JsonProperty(nameof(ActivityLogs))]
    [GraphQLName("ActivityLogs")]
    [GraphQLDescription("Activity logs associated with the session")]
    public ICollection<ActivityLogResponse>? ActivityLogs { get; set; }

    [Description("Regular set details for the session")]
    [JsonPropertyName("RegularSet")]
    [JsonProperty(nameof(RegularSet))]
    [GraphQLName("RegularSet")]
    [GraphQLDescription("Regular set details for the session")]
    public RegularSetResponse? RegularSet { get; set; }
}

[GraphQLName("BuySell")]
public class BuySellResponse
{
    [Description("Unique identifier for the buy/sell transaction")]
    [JsonPropertyName("BuySellId")]
    [JsonProperty(nameof(BuySellId))]
    [GraphQLName("BuySellId")]
    [GraphQLDescription("Unique identifier for the buy/sell transaction")]
    public int? BuySellId { get; set; }

    [Description("User ID of the buyer")]
    [MaxLength(128)]
    [JsonPropertyName("BuyerUserId")]
    [JsonProperty(nameof(BuyerUserId))]
    [GraphQLName("BuyerUserId")]
    [GraphQLDescription("User ID of the buyer")]
    public string? BuyerUserId { get; set; }

    [Description("User ID of the seller")]
    [MaxLength(128)]
    [JsonPropertyName("SellerUserId")]
    [JsonProperty(nameof(SellerUserId))]
    [GraphQLName("SellerUserId")]
    [GraphQLDescription("User ID of the seller")]
    public string? SellerUserId { get; set; }

    [Description("Note from the seller")]
    [DataType(DataType.MultilineText)]
    [JsonPropertyName("SellerNote")]
    [JsonProperty(nameof(SellerNote))]
    [GraphQLName("SellerNote")]
    [GraphQLDescription("Note from the seller")]
    public string? SellerNote { get; set; }

    [Description("Note from the buyer")]
    [DataType(DataType.MultilineText)]
    [JsonPropertyName("BuyerNote")]
    [JsonProperty(nameof(BuyerNote))]
    [GraphQLName("BuyerNote")]
    [GraphQLDescription("Note from the buyer")]
    public string? BuyerNote { get; set; }

    [Required]
    [Description("Indicates if payment has been sent")]
    [JsonPropertyName("PaymentSent")]
    [JsonProperty(nameof(PaymentSent), Required = Required.Always)]
    [GraphQLName("PaymentSent")]
    [GraphQLDescription("Indicates if payment has been sent")]
    public required bool PaymentSent { get; set; }

    [Required]
    [Description("Indicates if payment has been received")]
    [JsonPropertyName("PaymentReceived")]
    [JsonProperty(nameof(PaymentReceived), Required = Required.Always)]
    [GraphQLName("PaymentReceived")]
    [GraphQLDescription("Indicates if payment has been received")]
    public required bool PaymentReceived { get; set; }

    [Required]
    [Description("Date and time of transaction creation")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("CreateDateTime")]
    [JsonProperty(nameof(CreateDateTime), Required = Required.Always)]
    [GraphQLName("CreateDateTime")]
    [GraphQLDescription("Date and time of transaction creation")]
    public required DateTime CreateDateTime { get; set; }

    [Required]
    [Description("Team assignment for the transaction")]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("TeamAssignment")]
    [JsonProperty(nameof(TeamAssignment), Required = Required.Always)]
    [GraphQLName("TeamAssignment")]
    [GraphQLDescription("Team assignment for the transaction")]
    public required int TeamAssignment { get; set; }

    [Description("Buyer details")]
    [JsonPropertyName("Buyer")]
    [JsonProperty(nameof(Buyer))]
    [GraphQLName("Buyer")]
    [GraphQLDescription("Buyer details")]
    public UserBasicResponse? Buyer { get; set; }

    [Description("Seller details")]
    [JsonPropertyName("Seller")]
    [JsonProperty(nameof(Seller))]
    [GraphQLName("Seller")]
    [GraphQLDescription("Seller details")]
    public UserBasicResponse? Seller { get; set; }
}

[GraphQLName("ActivityLog")]
public class ActivityLogResponse
{
    [Required]
    [Description("Unique identifier for the activity log")]
    [JsonPropertyName("ActivityLogId")]
    [JsonProperty(nameof(ActivityLogId), Required = Required.Always)]
    [GraphQLName("ActivityLogId")]
    [GraphQLDescription("Unique identifier for the activity log")]
    public required int ActivityLogId { get; set; }

    [Description("User ID associated with the activity")]
    [MaxLength(128)]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId))]
    [GraphQLName("UserId")]
    [GraphQLDescription("User ID associated with the activity")]
    public string? UserId { get; set; }

    [Required]
    [Description("Date and time of the activity")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("CreateDateTime")]
    [JsonProperty(nameof(CreateDateTime), Required = Required.Always)]
    [GraphQLName("CreateDateTime")]
    [GraphQLDescription("Date and time of the activity")]
    public required DateTime CreateDateTime { get; set; }

    [Description("Description of the activity")]
    [DataType(DataType.MultilineText)]
    [JsonPropertyName("Activity")]
    [JsonProperty(nameof(Activity))]
    [GraphQLName("Activity")]
    [GraphQLDescription("Description of the activity")]
    public string? Activity { get; set; }

    [Description("User details")]
    [JsonPropertyName("User")]
    [JsonProperty(nameof(User))]
    [GraphQLName("User")]
    [GraphQLDescription("User details")]
    public UserBasicResponse? User { get; set; }
}

[GraphQLName("RegularSet")]
public class RegularSetResponse
{
    [Required]
    [Description("Unique identifier for the regular set")]
    [JsonPropertyName("RegularSetId")]
    [JsonProperty(nameof(RegularSetId), Required = Required.Always)]
    [GraphQLName("RegularSetId")]
    [GraphQLDescription("Unique identifier for the regular set")]
    public required int RegularSetId { get; set; }

    [Description("Description of the regular set")]
    [DataType(DataType.MultilineText)]
    [JsonPropertyName("Description")]
    [JsonProperty(nameof(Description))]
    [GraphQLName("Description")]
    [GraphQLDescription("Description of the regular set")]
    public string? Description { get; set; }

    [Required]
    [Description("Day of the week")]
    [Range(0, 6)]
    [JsonPropertyName("DayOfWeek")]
    [JsonProperty(nameof(DayOfWeek), Required = Required.Always)]
    [GraphQLName("DayOfWeek")]
    [GraphQLDescription("Day of the week (0 = Sunday, 6 = Saturday)")]
    public required int DayOfWeek { get; set; }

    [Required]
    [Description("Date and time of creation")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("CreateDateTime")]
    [JsonProperty(nameof(CreateDateTime), Required = Required.Always)]
    [GraphQLName("CreateDateTime")]
    [GraphQLDescription("Date and time of creation")]
    public required DateTime CreateDateTime { get; set; }

    [Description("Regular players in the set")]
    [JsonPropertyName("Regulars")]
    [JsonProperty(nameof(Regulars))]
    [GraphQLName("Regulars")]
    [GraphQLDescription("Regular players in the set")]
    public ICollection<RegularResponse>? Regulars { get; set; }
}

[GraphQLName("Regular")]
public class RegularResponse
{
    [Required]
    [Description("Regular set identifier")]
    [JsonPropertyName("RegularSetId")]
    [JsonProperty(nameof(RegularSetId), Required = Required.Always)]
    [GraphQLName("RegularSetId")]
    [GraphQLDescription("Regular set identifier")]
    public required int RegularSetId { get; set; }

    [Description("User ID of the regular player")]
    [MaxLength(128)]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId))]
    [GraphQLName("UserId")]
    [GraphQLDescription("User ID of the regular player")]
    public string? UserId { get; set; }

    [Required]
    [Description("Team assignment for the regular player")]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("TeamAssignment")]
    [JsonProperty(nameof(TeamAssignment), Required = Required.Always)]
    [GraphQLName("TeamAssignment")]
    [GraphQLDescription("Team assignment for the regular player")]
    public required int TeamAssignment { get; set; }

    [Required]
    [Description("Position preference for the regular player")]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("PositionPreference")]
    [JsonProperty(nameof(PositionPreference), Required = Required.Always)]
    [GraphQLName("PositionPreference")]
    [GraphQLDescription("Position preference for the regular player")]
    public required int PositionPreference { get; set; }

    [Description("User details")]
    [JsonPropertyName("User")]
    [JsonProperty(nameof(User))]
    [GraphQLName("User")]
    [GraphQLDescription("User details")]
    public UserBasicResponse? User { get; set; }
}
