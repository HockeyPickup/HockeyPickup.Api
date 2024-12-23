using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HockeyPickup.Api.Data.Entities;
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

    [Description("Cost of the session")]
    [JsonPropertyName("Cost")]
    [JsonProperty(nameof(Cost))]
    [GraphQLName("Cost")]
    [GraphQLDescription("Cost of the session")]
    public decimal? Cost{ get; set; }
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

    [Description("Current roster state for the session")]
    [JsonPropertyName("CurrentRosters")]
    [JsonProperty(nameof(CurrentRosters))]
    [GraphQLName("CurrentRosters")]
    [GraphQLDescription("Current roster state for the session")]
    public ICollection<RosterPlayer>? CurrentRosters { get; set; }

    [Description("Buying queue for the session")]
    [JsonPropertyName("BuyingQueues")]
    [JsonProperty(nameof(BuyingQueues))]
    [GraphQLName("BuyingQueues")]
    [GraphQLDescription("Buying queue for the session")]
    public ICollection<BuyingQueueItem>? BuyingQueues { get; set; }
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
    [Range(0, 2)]
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
    public UserDetailedResponse? Buyer { get; set; }

    [Description("Seller details")]
    [JsonPropertyName("Seller")]
    [JsonProperty(nameof(Seller))]
    [GraphQLName("Seller")]
    [GraphQLDescription("Seller details")]
    public UserDetailedResponse? Seller { get; set; }
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
    public UserDetailedResponse? User { get; set; }
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
    [Range(0, 2)]
    [JsonPropertyName("TeamAssignment")]
    [JsonProperty(nameof(TeamAssignment), Required = Required.Always)]
    [GraphQLName("TeamAssignment")]
    [GraphQLDescription("Team assignment for the regular player")]
    public required int TeamAssignment { get; set; }

    [Required]
    [Description("Position preference for the regular player")]
    [Range(0, 2)]
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
    public UserDetailedResponse? User { get; set; }
}

[GraphQLName("BuyingQueueItem")]
public class BuyingQueueItem
{
    [Required]
    [Description("Unique identifier for the buy/sell transaction")]
    [JsonPropertyName("BuySellId")]
    [JsonProperty(nameof(BuySellId), Required = Required.Always)]
    [GraphQLName("BuySellId")]
    [GraphQLDescription("Unique identifier for the buy/sell transaction")]
    public required int BuySellId { get; set; }

    [Required]
    [Description("Session identifier")]
    [JsonPropertyName("SessionId")]
    [JsonProperty(nameof(SessionId), Required = Required.Always)]
    [GraphQLName("SessionId")]
    [GraphQLDescription("Session identifier")]
    public required int SessionId { get; set; }

    [Description("Name of the buyer")]
    [MaxLength(512)]
    [DataType(DataType.Text)]
    [JsonPropertyName("BuyerName")]
    [JsonProperty(nameof(BuyerName), Required = Required.Default)]
    [GraphQLName("BuyerName")]
    [GraphQLDescription("Name of the buyer")]
    public string? BuyerName { get; set; }

    [Description("Name of the seller")]
    [MaxLength(512)]
    [DataType(DataType.Text)]
    [JsonPropertyName("SellerName")]
    [JsonProperty(nameof(SellerName), Required = Required.Default)]
    [GraphQLName("SellerName")]
    [GraphQLDescription("Name of the seller")]
    public string? SellerName { get; set; }

    [Required]
    [Description("Team assignment (1 for Light, 2 for Dark)")]
    [JsonPropertyName("TeamAssignment")]
    [JsonProperty(nameof(TeamAssignment), Required = Required.Always)]
    [GraphQLName("TeamAssignment")]
    [GraphQLDescription("Team assignment (1 for Light, 2 for Dark)")]
    public required int TeamAssignment { get; set; }

    [Required]
    [Description("Current status of the transaction")]
    [MaxLength(50)]
    [DataType(DataType.Text)]
    [JsonPropertyName("TransactionStatus")]
    [JsonProperty(nameof(TransactionStatus), Required = Required.Always)]
    [GraphQLName("TransactionStatus")]
    [GraphQLDescription("Current status of the transaction")]
    public required string TransactionStatus { get; set; }

    [Required]
    [Description("Position in the buying queue")]
    [MaxLength(50)]
    [DataType(DataType.Text)]
    [JsonPropertyName("QueueStatus")]
    [JsonProperty(nameof(QueueStatus), Required = Required.Always)]
    [GraphQLName("QueueStatus")]
    [GraphQLDescription("Position in the buying queue")]
    public required string QueueStatus { get; set; }

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

    [Description("Note from the buyer")]
    [MaxLength(4000)]
    [DataType(DataType.Text)]
    [JsonPropertyName("BuyerNote")]
    [JsonProperty(nameof(BuyerNote), Required = Required.Default)]
    [GraphQLName("BuyerNote")]
    [GraphQLDescription("Note from the buyer")]
    public string? BuyerNote { get; set; }

    [Description("Note from the seller")]
    [MaxLength(4000)]
    [DataType(DataType.Text)]
    [JsonPropertyName("SellerNote")]
    [JsonProperty(nameof(SellerNote), Required = Required.Default)]
    [GraphQLName("SellerNote")]
    [GraphQLDescription("Note from the seller")]
    public string? SellerNote { get; set; }
}

[GraphQLName("RosterPlayer")]
public class RosterPlayer
{
    [Required]
    [Description("Unique identifier for the roster entry")]
    [JsonPropertyName("SessionRosterId")]
    [JsonProperty(nameof(SessionRosterId), Required = Required.Always)]
    [GraphQLName("SessionRosterId")]
    [GraphQLDescription("Unique identifier for the roster entry")]
    public required int SessionRosterId { get; set; }

    [Required]
    [Description("Session identifier")]
    [JsonPropertyName("SessionId")]
    [JsonProperty(nameof(SessionId), Required = Required.Always)]
    [GraphQLName("SessionId")]
    [GraphQLDescription("Session identifier")]
    public required int SessionId { get; set; }

    [Required]
    [Description("User identifier")]
    [MaxLength(128)]
    [DataType(DataType.Text)]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId), Required = Required.Always)]
    [GraphQLName("UserId")]
    [GraphQLDescription("User identifier")]
    public required string UserId { get; set; }

    [Required]
    [Description("Email address of the user")]
    [MaxLength(256)]
    [DataType(DataType.EmailAddress)]
    [JsonPropertyName("Email")]
    [JsonProperty(nameof(Email), Required = Required.Always)]
    [GraphQLName("Email")]
    [GraphQLDescription("Email address of the user")]
    public required string Email { get; set; }

    [Required]
    [Description("First name of the player")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("FirstName")]
    [JsonProperty(nameof(FirstName), Required = Required.Always)]
    [GraphQLName("FirstName")]
    [GraphQLDescription("First name of the player")]
    public required string FirstName { get; set; }

    [Required]
    [Description("Last name of the player")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("LastName")]
    [JsonProperty(nameof(LastName), Required = Required.Always)]
    [GraphQLName("LastName")]
    [GraphQLDescription("Last name of the player")]
    public required string LastName { get; set; }

    [Required]
    [Description("Team assignment (1 for Light, 2 for Dark)")]
    [JsonPropertyName("TeamAssignment")]
    [JsonProperty(nameof(TeamAssignment), Required = Required.Always)]
    [GraphQLName("TeamAssignment")]
    [GraphQLDescription("Team assignment (1 for Light, 2 for Dark)")]
    public required int TeamAssignment { get; set; }

    [Required]
    [Description("Position for the player")]
    [Range(0, 2)]
    [JsonPropertyName("Position")]
    [JsonProperty(nameof(Position), Required = Required.Always)]
    [GraphQLName("Position")]
    [GraphQLDescription("Position for the player")]
    public required int Position{ get; set; }

    [Required]
    [Description("Position name for the player")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("CurrentPosition")]
    [JsonProperty(nameof(CurrentPosition), Required = Required.Always)]
    [GraphQLName("CurrentPosition")]
    [GraphQLDescription("Position name for the player")]
    public required string CurrentPosition { get; set; }

    [Required]
    [Description("Indicates if the player is currently playing")]
    [JsonPropertyName("IsPlaying")]
    [JsonProperty(nameof(IsPlaying), Required = Required.Always)]
    [GraphQLName("IsPlaying")]
    [GraphQLDescription("Indicates if the player is currently playing")]
    public required bool IsPlaying { get; set; }

    [Required]
    [Description("Indicates if the player is a regular")]
    [JsonPropertyName("IsRegular")]
    [JsonProperty(nameof(IsRegular), Required = Required.Always)]
    [GraphQLName("IsRegular")]
    [GraphQLDescription("Indicates if the player is a regular")]
    public required bool IsRegular { get; set; }

    [Required]
    [Description("Player's status in the roster")]
    [JsonPropertyName("PlayerStatus")]
    [JsonProperty(nameof(PlayerStatus), Required = Required.Always)]
    [GraphQLName("PlayerStatus")]
    [GraphQLDescription("Player's status in the roster")]
    public required PlayerStatus PlayerStatus { get; set; }

    [Required]
    [Description("Player's rating")]
    [JsonPropertyName("Rating")]
    [JsonProperty(nameof(Rating), Required = Required.Always)]
    [GraphQLName("Rating")]
    [GraphQLDescription("Player's rating")]
    public required decimal Rating { get; set; }

    [Required]
    [Description("Indicates if the player has preferred status")]
    [JsonPropertyName("Preferred")]
    [JsonProperty(nameof(Preferred), Required = Required.Always)]
    [GraphQLName("Preferred")]
    [GraphQLDescription("Indicates if the player has preferred status")]
    public required bool Preferred { get; set; }

    [Required]
    [Description("Indicates if the player has preferred plus status")]
    [JsonPropertyName("PreferredPlus")]
    [JsonProperty(nameof(PreferredPlus), Required = Required.Always)]
    [GraphQLName("PreferredPlus")]
    [GraphQLDescription("Indicates if the player has preferred plus status")]
    public required bool PreferredPlus { get; set; }

    [Description("Last buy/sell transaction ID affecting this roster position")]
    [JsonPropertyName("LastBuySellId")]
    [JsonProperty(nameof(LastBuySellId), Required = Required.Default)]
    [GraphQLName("LastBuySellId")]
    [GraphQLDescription("Last buy/sell transaction ID affecting this roster position")]
    public int? LastBuySellId { get; set; }

    [Required]
    [Description("Date and time when the player joined the roster")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("JoinedDateTime")]
    [JsonProperty(nameof(JoinedDateTime), Required = Required.Always)]
    [GraphQLName("JoinedDateTime")]
    [GraphQLDescription("Date and time when the player joined the roster")]
    public required DateTime JoinedDateTime { get; set; }
}
