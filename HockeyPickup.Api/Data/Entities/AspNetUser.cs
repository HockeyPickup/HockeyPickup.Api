// Data/Entities/AspNetUser.cs - Updated to match exactly
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace HockeyPickup.Api.Data.Entities;

[ExcludeFromCodeCoverage]
public partial class AspNetUser : IdentityUser<string>
{
    private DateTime? _lockoutEndDateUtc;
    
    public AspNetUser()
    {
        Id = Guid.NewGuid().ToString();
        DateCreated = DateTime.UtcNow;
        EmailConfirmed = false;
        PhoneNumberConfirmed = false;
        TwoFactorEnabled = false;
        LockoutEnabled = false;
        AccessFailedCount = 0;
        NotificationPreference = 1;
        PositionPreference = 0;
        Active = false;
        Rating = 0;
        PreferredPlus = false;
        LockerRoom13 = false;
    }

    [Column("LockoutEndDateUtc")]
    public DateTime? LockoutEndDateUtc
    {
        get => _lockoutEndDateUtc;
        set
        {
            _lockoutEndDateUtc = value;
            LockoutEnd = value?.ToUniversalTime();
        }
    }

    [NotMapped]
    public override DateTimeOffset? LockoutEnd { get; set; }

    // Existing columns - no need to override base properties that match
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int NotificationPreference { get; set; }
    public int PositionPreference { get; set; }
    public bool Active { get; set; }
    public bool Preferred { get; set; }
    public decimal Rating { get; set; }
    public bool PreferredPlus { get; set; }
    public string? EmergencyName { get; set; }
    public string? EmergencyPhone { get; set; }
    public int JerseyNumber { get; set; }
    public bool LockerRoom13 { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime DateCreated { get; set; }

    // Virtual navigation property
    public virtual ICollection<AspNetRole> Roles { get; set; } = new List<AspNetRole>();

    // Navigation properties
    public virtual ICollection<BuySell>? BuyerTransactions { get; set; }
    public virtual ICollection<BuySell>? SellerTransactions { get; set; }
    public virtual ICollection<ActivityLog>? ActivityLogs { get; set; }
    public virtual ICollection<Regular>? Regulars { get; set; }
}

[ExcludeFromCodeCoverage]
public partial class SchemaComplete : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Just mark the migration as completed, since we've done the changes manually
        migrationBuilder.Sql("SELECT 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("SELECT 1");
    }
}
