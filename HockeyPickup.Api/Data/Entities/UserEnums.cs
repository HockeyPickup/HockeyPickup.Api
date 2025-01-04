using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HockeyPickup.Api.Data.Entities;

public enum NotificationPreference
{
    [Display(Name = @"None")]
    None,
    [Display(Name = @"All")]
    All,
    [Display(Name = @"Only My Buy/Sells")]
    OnlyMyBuySell
}

public enum PositionPreference
{
    [Display(Name = @"TBD")]
    TBD,
    [Display(Name = @"Forward")]
    Forward,
    [Display(Name = @"Defense")]
    Defense,
    [Display(Name = @"Goalie")]
    Goalie
}

public enum PlayerStatus
{
    [Description("Regular player in the roster")]
    Regular = 0,

    [Description("Substitute player in the roster")]
    Substitute = 1,

    [Description("Player no longer in the roster")]
    NotPlaying = 2,

    [Description("Player int the queue")]
    InQueue = 3
}
