using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using Newtonsoft.Json;

namespace HockeyPickup.Api.Models.Responses;

[GraphQLName("RegularSetDetailed")]
public class RegularSetDetailedResponse
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
    [Description("Day of the week (0 = Sunday, 6 = Saturday)")]
    [Range(0, 6)]
    [JsonPropertyName("DayOfWeek")]
    [JsonProperty(nameof(DayOfWeek), Required = Required.Always)]
    [GraphQLName("DayOfWeek")]
    [GraphQLDescription("Day of the week (0 = Sunday, 6 = Saturday)")]
    public required int DayOfWeek { get; set; }

    [Required]
    [Description("Date and time when the regular set was created")]
    [DataType(DataType.DateTime)]
    [JsonPropertyName("CreateDateTime")]
    [JsonProperty(nameof(CreateDateTime), Required = Required.Always)]
    [GraphQLName("CreateDateTime")]
    [GraphQLDescription("Date and time when the regular set was created")]
    public required DateTime CreateDateTime { get; set; }

    [Required]
    [Description("Indicates if the regular set is archived")]
    [JsonPropertyName("Archived")]
    [JsonProperty(nameof(Archived), Required = Required.Always)]
    [GraphQLName("Archived")]
    [GraphQLDescription("Indicates if the regular set is archived")]
    public required bool Archived { get; set; }

    [Description("List of regular players in this set")]
    [JsonPropertyName("Regulars")]
    [JsonProperty(nameof(Regulars))]
    [GraphQLName("Regulars")]
    [GraphQLDescription("List of regular players in this set")]
    public List<RegularDetailedResponse>? Regulars { get; set; }
}

[GraphQLName("RegularDetailed")]
public class RegularDetailedResponse
{
    [Required]
    [Description("Regular set identifier")]
    [JsonPropertyName("RegularSetId")]
    [JsonProperty(nameof(RegularSetId), Required = Required.Always)]
    [GraphQLName("RegularSetId")]
    [GraphQLDescription("Regular set identifier")]
    public required int RegularSetId { get; set; }

    [Required]
    [Description("User identifier")]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId), Required = Required.Always)]
    [GraphQLName("UserId")]
    [GraphQLDescription("User identifier")]
    public required string UserId { get; set; }

    [Required]
    [Description("Team assignment for the regular player")]
    [JsonPropertyName("TeamAssignment")]
    [JsonProperty(nameof(TeamAssignment), Required = Required.Always)]
    [GraphQLName("TeamAssignment")]
    [GraphQLDescription("Team assignment for the regular player")]
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<TeamAssignment>))]
    public required TeamAssignment TeamAssignment { get; set; }

    [Required]
    [Description("Position preference for the regular player")]
    [JsonPropertyName("PositionPreference")]
    [JsonProperty(nameof(PositionPreference), Required = Required.Always)]
    [GraphQLName("PositionPreference")]
    [GraphQLDescription("Position preference for the regular player")]
    [System.Text.Json.Serialization.JsonConverter(typeof(EnumDisplayNameConverter<PositionPreference>))]
    public required PositionPreference PositionPreference { get; set; }

    [Description("Detailed user information")]
    [JsonPropertyName("User")]
    [JsonProperty(nameof(User))]
    [GraphQLName("User")]
    [GraphQLDescription("Detailed user information")]
    public UserDetailedResponse? User { get; set; }
}
