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

public enum ShootPreference
{
    [GraphQLName("TBD")]
    [Display(Name = @"TBD")]
    TBD = 0,

    [GraphQLName("Left")]
    [Display(Name = @"Left")]
    Left = 1,

    [GraphQLName("Right")]
    [Display(Name = @"Right")]
    Right = 2
}

// Lottery tier - mirrors the existing BuyWindow naming (PreferredPlus > Preferred > Standard)
public enum LotteryClass
{
    [GraphQLName("PreferredPlus")]
    [Display(Name = @"PreferredPlus")]
    [Description("Preferred Plus tier lottery")]
    PreferredPlus = 1,

    [GraphQLName("Preferred")]
    [Display(Name = @"Preferred")]
    [Description("Preferred tier lottery")]
    Preferred = 2,

    [GraphQLName("Standard")]
    [Display(Name = @"Standard")]
    [Description("Standard tier lottery")]
    Standard = 3
}

public enum LotteryEntrantStatus
{
    [GraphQLName("Entered")]
    [Display(Name = @"Entered")]
    [Description("Entrant is entered and awaiting the draw")]
    Entered = 1,

    [GraphQLName("Withdrawn")]
    [Display(Name = @"Withdrawn")]
    [Description("Entrant withdrew before the draw")]
    Withdrawn = 2,

    [GraphQLName("Drawing")]
    [Display(Name = @"Drawing")]
    [Description("Entrant is claimed by an in-progress draw")]
    Drawing = 3,

    [GraphQLName("Drawn")]
    [Display(Name = @"Drawn")]
    [Description("Entrant was drawn and their buy processed")]
    Drawn = 4,

    [GraphQLName("Failed")]
    [Display(Name = @"Failed")]
    [Description("Entrant was drawn but their buy failed")]
    Failed = 5
}

// Drives the front end as a pure switch - populated on every CanBuy path
public enum BuyActionState
{
    [GraphQLName("NotEligible")]
    [Display(Name = @"NotEligible")]
    [Description("User cannot buy or enter a lottery for this session")]
    NotEligible = 0,

    [GraphQLName("WindowNotOpen")]
    [Display(Name = @"WindowNotOpen")]
    [Description("The user's buy/entry window has not opened yet")]
    WindowNotOpen = 1,

    [GraphQLName("EnterLottery")]
    [Display(Name = @"EnterLottery")]
    [Description("The user may enter the applicable lottery")]
    EnterLottery = 2,

    [GraphQLName("InLottery")]
    [Display(Name = @"InLottery")]
    [Description("The user is already entered in the applicable lottery")]
    InLottery = 3,

    [GraphQLName("BuyNow")]
    [Display(Name = @"BuyNow")]
    [Description("The user may buy a spot directly")]
    BuyNow = 4
}
