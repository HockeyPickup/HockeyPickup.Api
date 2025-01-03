using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

[GraphQLName("UserStats")]
public class UserStatsResponse
{
    [Required]
    [Description("Date when user became a member")]
    [JsonPropertyName("MemberSince")]
    [JsonProperty(nameof(MemberSince), Required = Required.Always)]
    [GraphQLName("MemberSince")]
    [GraphQLDescription("Date when user became a member")]
    public required DateTime MemberSince { get; set; }

    [Required]
    [Description("Games played in current year")]
    [JsonPropertyName("CurrentYearGamesPlayed")]
    [JsonProperty(nameof(CurrentYearGamesPlayed), Required = Required.Always)]
    [GraphQLName("CurrentYearGamesPlayed")]
    [GraphQLDescription("Games played in current year")]
    public required int CurrentYearGamesPlayed { get; set; }

    [Required]
    [Description("Games played in prior year")]
    [JsonPropertyName("PriorYearGamesPlayed")]
    [JsonProperty(nameof(PriorYearGamesPlayed), Required = Required.Always)]
    [GraphQLName("PriorYearGamesPlayed")]
    [GraphQLDescription("Games played in prior year")]
    public required int PriorYearGamesPlayed { get; set; }

    [Required]
    [Description("Spots bought in current year")]
    [JsonPropertyName("CurrentYearBoughtTotal")]
    [JsonProperty(nameof(CurrentYearBoughtTotal), Required = Required.Always)]
    [GraphQLName("CurrentYearBoughtTotal")]
    [GraphQLDescription("Spots bought in current year")]
    public required int CurrentYearBoughtTotal { get; set; }

    [Required]
    [Description("Spots bought in prior year")]
    [JsonPropertyName("PriorYearBoughtTotal")]
    [JsonProperty(nameof(PriorYearBoughtTotal), Required = Required.Always)]
    [GraphQLName("PriorYearBoughtTotal")]
    [GraphQLDescription("Spots bought in prior year")]
    public required int PriorYearBoughtTotal { get; set; }

    [Description("Date of last bought session")]
    [JsonPropertyName("LastBoughtSessionDate")]
    [JsonProperty(nameof(LastBoughtSessionDate))]
    [GraphQLName("LastBoughtSessionDate")]
    [GraphQLDescription("Date of last bought session")]
    public DateTime? LastBoughtSessionDate { get; set; }

    [Required]
    [Description("Spots sold in current year")]
    [JsonPropertyName("CurrentYearSoldTotal")]
    [JsonProperty(nameof(CurrentYearSoldTotal), Required = Required.Always)]
    [GraphQLName("CurrentYearSoldTotal")]
    [GraphQLDescription("Spots sold in current year")]
    public required int CurrentYearSoldTotal { get; set; }

    [Required]
    [Description("Spots sold in prior year")]
    [JsonPropertyName("PriorYearSoldTotal")]
    [JsonProperty(nameof(PriorYearSoldTotal), Required = Required.Always)]
    [GraphQLName("PriorYearSoldTotal")]
    [GraphQLDescription("Spots sold in prior year")]
    public required int PriorYearSoldTotal { get; set; }

    [Description("Date of last sold session")]
    [JsonPropertyName("LastSoldSessionDate")]
    [JsonProperty(nameof(LastSoldSessionDate))]
    [GraphQLName("LastSoldSessionDate")]
    [GraphQLDescription("Date of last sold session")]
    public DateTime? LastSoldSessionDate { get; set; }

    [Description("Most frequently played position")]
    [JsonPropertyName("MostPlayedPosition")]
    [JsonProperty(nameof(MostPlayedPosition))]
    [GraphQLName("MostPlayedPosition")]
    [GraphQLDescription("Most frequently played position")]
    public string? MostPlayedPosition { get; set; }

    [Required]
    [Description("Current active buy requests")]
    [JsonPropertyName("CurrentBuyRequests")]
    [JsonProperty(nameof(CurrentBuyRequests), Required = Required.Always)]
    [GraphQLName("CurrentBuyRequests")]
    [GraphQLDescription("Current active buy requests")]
    public required int CurrentBuyRequests { get; set; }
}
