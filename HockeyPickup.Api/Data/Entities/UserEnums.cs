using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HockeyPickup.Api.Data.Entities;

public enum NotificationPreference
{
    [GraphQLName("None")]
    [Display(Name = @"None")]
    None = 0,

    [GraphQLName("All")]
    [Display(Name = @"All")]
    All = 1,

    [GraphQLName("OnlyMyBuySell")]
    [Display(Name = @"OnlyMyBuySell")]
    OnlyMyBuySell = 2,
}

public enum PositionPreference
{
    [GraphQLName("TBD")]
    [Display(Name = @"TBD")]
    TBD = 0,

    [GraphQLName("Forward")]
    [Display(Name = @"Forward")]
    Forward = 1,

    [GraphQLName("Defense")]
    [Display(Name = @"Defense")]
    Defense = 2,

    [GraphQLName("Goalie")]
    [Display(Name = @"Goalie")]
    Goalie = 3
}

public enum PlayerStatus
{
    [GraphQLName("NotPlaying")]
    [Display(Name = @"NotPlaying")]
    [Description("Player no longer in the roster")]
    NotPlaying = 0,

    [GraphQLName("Regular")]
    [Display(Name = @"Regular")]
    [Description("Regular player in the roster")]
    Regular = 1,

    [GraphQLName("Substitute")]
    [Display(Name = @"Substitute")]
    [Description("Substitute player in the roster")]
    Substitute = 2,

    [GraphQLName("InQueue")]
    [Display(Name = @"InQueue")]
    [Description("Player in the queue")]
    InQueue = 3
}

public enum TeamAssignment
{
    [GraphQLName("TBD")]
    [Display(Name = @"TBD")]
    TBD = 0,

    [GraphQLName("Light")]
    [Display(Name = @"Light")]
    Light = 1,

    [GraphQLName("Dark")]
    [Display(Name = @"Dark")]
    Dark = 2
}

// PaymentMethod enum to define available payment types
public enum PaymentMethodType
{
    [GraphQLName("Unknown")]
    [Display(Name = @"Unknown")]
    Unknown = 0,

    [GraphQLName("PayPal")]
    [Display(Name = @"PayPal")]
    PayPal = 1,

    [GraphQLName("Venmo")]
    [Display(Name = @"Venmo")]
    Venmo = 2,

    [GraphQLName("CashApp")]
    [Display(Name = @"CashApp")]
    CashApp = 3,

    [GraphQLName("Zelle")]
    [Display(Name = @"Zelle")]
    Zelle = 4,

    [GraphQLName("Bitcoin")]
    [Display(Name = @"Bitcoin")]
    Bitcoin = 5
}
