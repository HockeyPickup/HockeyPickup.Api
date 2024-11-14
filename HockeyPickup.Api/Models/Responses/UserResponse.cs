using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HockeyPickup.Api.Data.Entities;
using Newtonsoft.Json;

namespace HockeyPickup.Api.Models.Responses;

[GraphQLName("User")]
public class UserBasicResponse
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
    [Description("Username of the user")]
    [MaxLength(256)]
    [DataType(DataType.Text)]
    [JsonPropertyName("UserName")]
    [JsonProperty(nameof(UserName), Required = Required.Always)]
    [GraphQLName("UserName")]
    [GraphQLDescription("Username of the user")]
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
    [Description("Indicates if the user has preferred status")]
    [JsonPropertyName("IsPreferred")]
    [JsonProperty(nameof(IsPreferred), Required = Required.Always)]
    [GraphQLName("IsPreferred")]
    [GraphQLDescription("Indicates if the user has preferred status")]
    public required bool IsPreferred { get; set; }

    [Required]
    [Description("Indicates if the user has preferred plus status")]
    [JsonPropertyName("IsPreferredPlus")]
    [JsonProperty(nameof(IsPreferredPlus), Required = Required.Always)]
    [GraphQLName("IsPreferredPlus")]
    [GraphQLDescription("Indicates if the user has preferred plus status")]
    public required bool IsPreferredPlus { get; set; }

    [Description("Current team assignment")]
    [JsonPropertyName("TeamAssignment")]
    [JsonProperty(nameof(TeamAssignment), Required = Required.Default)]
    [GraphQLName("TeamAssignment")]
    [GraphQLDescription("Current team assignment")]
    public TeamAssignment? TeamAssignment { get; set; }

    [Description("User's preferred position")]
    [JsonPropertyName("PositionPreference")]
    [JsonProperty(nameof(PositionPreference), Required = Required.Default)]
    [GraphQLName("PositionPreference")]
    [GraphQLDescription("User's preferred position")]
    public PositionPreference? PositionPreference { get; set; }

    [Description("User's notification preferences")]
    [JsonPropertyName("NotificationPreference")]
    [JsonProperty(nameof(NotificationPreference), Required = Required.Default)]
    [GraphQLName("NotificationPreference")]
    [GraphQLDescription("User's notification preferences")]
    public NotificationPreference? NotificationPreference { get; set; }
}

public class UserDetailedResponse : UserBasicResponse
{
    [Required]
    [Description("User's rating")]
    [Range(0, 5)]
    [JsonPropertyName("Rating")]
    [JsonProperty(nameof(Rating), Required = Required.Always)]
    [GraphQLName("Rating")]
    [GraphQLDescription("User's rating")]
    public required decimal Rating { get; set; }
}
