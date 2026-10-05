using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using Newtonsoft.Json;

namespace HockeyPickup.Api.Models.Responses;

// Everything the signed-in player's home page needs, in one round trip. The lean types below carry only the
// fields the dashboard reads, so none of GetSessionAsync's activity logs, regular sets or payment methods are loaded.
[GraphQLName("Dashboard")]
public class DashboardResponse
{
    [Required]
    [Description("Every upcoming session, soonest first, cancelled ones included")]
    [JsonPropertyName("UpcomingSessions")]
    [JsonProperty(nameof(UpcomingSessions), Required = Required.Always)]
    [GraphQLName("UpcomingSessions")]
    [GraphQLDescription("Every upcoming session, soonest first, cancelled ones included")]
    public required ICollection<SessionBasicResponse> UpcomingSessions { get; set; }

    [Required]
    [Description("Roster and buy/sell detail for the nearest upcoming sessions that are not cancelled, soonest first")]
    [JsonPropertyName("Sessions")]
    [JsonProperty(nameof(Sessions), Required = Required.Always)]
    [GraphQLName("Sessions")]
    [GraphQLDescription("Roster and buy/sell detail for the nearest upcoming sessions that are not cancelled, soonest first")]
    public required ICollection<DashboardSessionResponse> Sessions { get; set; }

    [Required]
    [Description("The signed-in user's completed transactions still awaiting payment or confirmation, with counterparty names")]
    [JsonPropertyName("PendingPayments")]
    [JsonProperty(nameof(PendingPayments), Required = Required.Always)]
    [GraphQLName("PendingPayments")]
    [GraphQLDescription("The signed-in user's completed transactions still awaiting payment or confirmation, with counterparty names")]
    public required ICollection<DashboardBuySell> PendingPayments { get; set; }

    [Required]
    [Description("Past sessions the signed-in user played in net, counted by calendar year")]
    [JsonPropertyName("GoalieStartsByYear")]
    [JsonProperty(nameof(GoalieStartsByYear), Required = Required.Always)]
    [GraphQLName("GoalieStartsByYear")]
    [GraphQLDescription("Past sessions the signed-in user played in net, counted by calendar year")]
    public required ICollection<GoalieStartsYear> GoalieStartsByYear { get; set; }
}

[GraphQLName("DashboardSession")]
public class DashboardSessionResponse : SessionBasicResponse
{
    // Same formulas as SessionDetailedResponse; DashboardResponseTest pins them together
    [Description("Buy window for the session")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("BuyWindow")]
    [JsonProperty(nameof(BuyWindow))]
    [GraphQLName("BuyWindow")]
    [GraphQLDescription("Buy window for the session")]
    public DateTime BuyWindow => SessionDate.AddDays(-BuyDayMinimum.GetValueOrDefault()).AddHours(2);

    [Description("Buy window for preferred users")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("BuyWindowPreferred")]
    [JsonProperty(nameof(BuyWindowPreferred))]
    [GraphQLName("BuyWindowPreferred")]
    [GraphQLDescription("Buy window for preferred users")]
    public DateTime BuyWindowPreferred => SessionDate.AddDays(-BuyDayMinimum.GetValueOrDefault() - 1).AddHours(2);

    [Description("Buy window for preferred plus users")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("BuyWindowPreferredPlus")]
    [JsonProperty(nameof(BuyWindowPreferredPlus))]
    [GraphQLName("BuyWindowPreferredPlus")]
    [GraphQLDescription("Buy window for preferred plus users")]
    public DateTime BuyWindowPreferredPlus => SessionDate.AddDays(-BuyDayMinimum.GetValueOrDefault() - 1).AddHours(2).AddMinutes(-5);

    [Required]
    [Description("Current roster for the session, in the same order as the session page")]
    [JsonPropertyName("CurrentRosters")]
    [JsonProperty(nameof(CurrentRosters), Required = Required.Always)]
    [GraphQLName("CurrentRosters")]
    [GraphQLDescription("Current roster for the session, in the same order as the session page")]
    public ICollection<DashboardRosterPlayer> CurrentRosters { get; set; } = new List<DashboardRosterPlayer>();

    [Required]
    [Description("Buy/sell transactions for the session, by BuySellId")]
    [JsonPropertyName("BuySells")]
    [JsonProperty(nameof(BuySells), Required = Required.Always)]
    [GraphQLName("BuySells")]
    [GraphQLDescription("Buy/sell transactions for the session, by BuySellId")]
    public ICollection<DashboardBuySell> BuySells { get; set; } = new List<DashboardBuySell>();

    [Required]
    [Description("Buying queue for the session, by BuySellId")]
    [JsonPropertyName("BuyingQueues")]
    [JsonProperty(nameof(BuyingQueues), Required = Required.Always)]
    [GraphQLName("BuyingQueues")]
    [GraphQLDescription("Buying queue for the session, by BuySellId")]
    public ICollection<DashboardQueueEntry> BuyingQueues { get; set; } = new List<DashboardQueueEntry>();
}

[GraphQLName("DashboardRosterPlayer")]
public class DashboardRosterPlayer
{
    [Required]
    [Description("User Id of the player")]
    [MaxLength(128)]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId), Required = Required.Always)]
    [GraphQLName("UserId")]
    [GraphQLDescription("User Id of the player")]
    public required string UserId { get; set; }

    [Required]
    [Description("First name of the player")]
    [MaxLength(256)]
    [JsonPropertyName("FirstName")]
    [JsonProperty(nameof(FirstName), Required = Required.Always)]
    [GraphQLName("FirstName")]
    [GraphQLDescription("First name of the player")]
    public required string FirstName { get; set; }

    [Required]
    [Description("Last name of the player")]
    [MaxLength(256)]
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
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<TeamAssignment>))]
    public required TeamAssignment TeamAssignment { get; set; }

    [Required]
    [Description("Position for the player")]
    [JsonPropertyName("Position")]
    [JsonProperty(nameof(Position), Required = Required.Always)]
    [GraphQLName("Position")]
    [GraphQLDescription("Position for the player")]
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<PositionPreference>))]
    public required PositionPreference Position { get; set; }

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
    [Description("Whether the player is currently playing")]
    [JsonPropertyName("IsPlaying")]
    [JsonProperty(nameof(IsPlaying), Required = Required.Always)]
    [GraphQLName("IsPlaying")]
    [GraphQLDescription("Whether the player is currently playing")]
    public required bool IsPlaying { get; set; }
}

[GraphQLName("DashboardBuySell")]
public class DashboardBuySell
{
    [Required]
    [Description("Unique identifier for the BuySell")]
    [JsonPropertyName("BuySellId")]
    [JsonProperty(nameof(BuySellId), Required = Required.Always)]
    [GraphQLName("BuySellId")]
    [GraphQLDescription("Unique identifier for the BuySell")]
    public required int BuySellId { get; set; }

    [Required]
    [Description("Unique identifier for the session")]
    [JsonPropertyName("SessionId")]
    [JsonProperty(nameof(SessionId), Required = Required.Always)]
    [GraphQLName("SessionId")]
    [GraphQLDescription("Unique identifier for the session")]
    public required int SessionId { get; set; }

    [Description("User Id of the buyer")]
    [MaxLength(128)]
    [JsonPropertyName("BuyerUserId")]
    [JsonProperty(nameof(BuyerUserId))]
    [GraphQLName("BuyerUserId")]
    [GraphQLDescription("User Id of the buyer")]
    public string? BuyerUserId { get; set; }

    [Description("User Id of the seller")]
    [MaxLength(128)]
    [JsonPropertyName("SellerUserId")]
    [JsonProperty(nameof(SellerUserId))]
    [GraphQLName("SellerUserId")]
    [GraphQLDescription("User Id of the seller")]
    public string? SellerUserId { get; set; }

    [Required]
    [Description("Whether the buyer has sent payment")]
    [JsonPropertyName("PaymentSent")]
    [JsonProperty(nameof(PaymentSent), Required = Required.Always)]
    [GraphQLName("PaymentSent")]
    [GraphQLDescription("Whether the buyer has sent payment")]
    public required bool PaymentSent { get; set; }

    [Required]
    [Description("Whether the seller has confirmed payment")]
    [JsonPropertyName("PaymentReceived")]
    [JsonProperty(nameof(PaymentReceived), Required = Required.Always)]
    [GraphQLName("PaymentReceived")]
    [GraphQLDescription("Whether the seller has confirmed payment")]
    public required bool PaymentReceived { get; set; }

    [Required]
    [Description("Price for the BuySell (from session)")]
    [Range(0, 999.99)]
    [DataType(DataType.Currency)]
    [JsonPropertyName("Price")]
    [JsonProperty(nameof(Price), Required = Required.Always)]
    [GraphQLName("Price")]
    [GraphQLDescription("Price for the BuySell (from session)")]
    public required decimal Price { get; set; }

    [Description("Buyer's name")]
    [JsonPropertyName("Buyer")]
    [JsonProperty(nameof(Buyer))]
    [GraphQLName("Buyer")]
    [GraphQLDescription("Buyer's name")]
    public DashboardCounterparty? Buyer { get; set; }

    [Description("Seller's name")]
    [JsonPropertyName("Seller")]
    [JsonProperty(nameof(Seller))]
    [GraphQLName("Seller")]
    [GraphQLDescription("Seller's name")]
    public DashboardCounterparty? Seller { get; set; }
}

[GraphQLName("DashboardCounterparty")]
public class DashboardCounterparty
{
    [Required]
    [Description("Unique identifier for the user")]
    [MaxLength(128)]
    [JsonPropertyName("Id")]
    [JsonProperty(nameof(Id), Required = Required.Always)]
    [GraphQLName("Id")]
    [GraphQLDescription("Unique identifier for the user")]
    public required string Id { get; set; }

    [Description("User's first name")]
    [MaxLength(256)]
    [JsonPropertyName("FirstName")]
    [JsonProperty(nameof(FirstName))]
    [GraphQLName("FirstName")]
    [GraphQLDescription("User's first name")]
    public string? FirstName { get; set; }

    [Description("User's last name")]
    [MaxLength(256)]
    [JsonPropertyName("LastName")]
    [JsonProperty(nameof(LastName))]
    [GraphQLName("LastName")]
    [GraphQLDescription("User's last name")]
    public string? LastName { get; set; }
}

[GraphQLName("DashboardQueueEntry")]
public class DashboardQueueEntry
{
    [Required]
    [Description("Unique identifier for the BuySell")]
    [JsonPropertyName("BuySellId")]
    [JsonProperty(nameof(BuySellId), Required = Required.Always)]
    [GraphQLName("BuySellId")]
    [GraphQLDescription("Unique identifier for the BuySell")]
    public required int BuySellId { get; set; }

    [Description("User Id of the buyer")]
    [MaxLength(128)]
    [JsonPropertyName("BuyerUserId")]
    [JsonProperty(nameof(BuyerUserId))]
    [GraphQLName("BuyerUserId")]
    [GraphQLDescription("User Id of the buyer")]
    public string? BuyerUserId { get; set; }

    [Description("User Id of the seller")]
    [MaxLength(128)]
    [JsonPropertyName("SellerUserId")]
    [JsonProperty(nameof(SellerUserId))]
    [GraphQLName("SellerUserId")]
    [GraphQLDescription("User Id of the seller")]
    public string? SellerUserId { get; set; }

    [Required]
    [Description("Position in the buying queue")]
    [MaxLength(50)]
    [DataType(DataType.Text)]
    [JsonPropertyName("QueueStatus")]
    [JsonProperty(nameof(QueueStatus), Required = Required.Always)]
    [GraphQLName("QueueStatus")]
    [GraphQLDescription("Position in the buying queue")]
    public required string QueueStatus { get; set; }
}

[GraphQLName("GoalieStartsYear")]
public class GoalieStartsYear
{
    [Required]
    [Description("Calendar year of the sessions")]
    [JsonPropertyName("Year")]
    [JsonProperty(nameof(Year), Required = Required.Always)]
    [GraphQLName("Year")]
    [GraphQLDescription("Calendar year of the sessions")]
    public required int Year { get; set; }

    [Required]
    [Description("Number of sessions played in net that year")]
    [JsonPropertyName("Starts")]
    [JsonProperty(nameof(Starts), Required = Required.Always)]
    [GraphQLName("Starts")]
    [GraphQLDescription("Number of sessions played in net that year")]
    public required int Starts { get; set; }
}
