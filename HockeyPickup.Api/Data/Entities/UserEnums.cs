using System.ComponentModel.DataAnnotations;

namespace HockeyPickup.Api.Data.Entities;

public enum TeamAssignment
{
    Unassigned,
    Light,
    Dark
}

public enum PositionPreference
{
    None,
    Forward,
    Defense
}

public enum NotificationPreference
{
    [Display(Name = @"None")]
    None,
    [Display(Name = @"All")]
    All,
    [Display(Name = @"Only My Buy/Sells")]
    OnlyMyBuySell
}
