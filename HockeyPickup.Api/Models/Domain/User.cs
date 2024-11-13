namespace HockeyPickup.Api.Models.Domain;

public class User
{
    public string Id { get; set; } = null!;
    public string? Email { get; set; }
    public string? Username { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PasswordHash { get; set; }
    public string? SecurityStamp { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTime? LockoutEndDateUtc { get; set; }
    public bool LockoutEnabled { get; set; }
    public int AccessFailedCount { get; set; }
    public string UserName { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int NotificationPreference { get; set; }
    public string PayPalEmail { get; set; } = null!;
    public bool Active { get; set; }
    public bool Preferred { get; set; }
    public string? VenmoAccount { get; set; }
    public string? MobileLast4 { get; set; }
    public decimal Rating { get; set; }
    public bool PreferredPlus { get; set; }
    public string? EmergencyName { get; set; }
    public string? EmergencyPhone { get; set; }
    public bool LockerRoom13 { get; set; }
}
