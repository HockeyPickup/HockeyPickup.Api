using System.ComponentModel.DataAnnotations;

namespace HockeyPickup.Api.Models.Requests;

public class ForgotPasswordRequest
{
    [Required]
    [Url]
    public string FrontendUrl { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
