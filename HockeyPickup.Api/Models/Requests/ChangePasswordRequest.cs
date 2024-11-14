using System.ComponentModel.DataAnnotations;

namespace HockeyPickup.Api.Models.Requests;

public class ChangePasswordRequest
{
    [Required]
    [MinLength(8)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare("NewPassword")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
