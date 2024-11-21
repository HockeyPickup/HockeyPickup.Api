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
