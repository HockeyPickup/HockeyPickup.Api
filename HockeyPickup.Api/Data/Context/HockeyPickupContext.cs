using HockeyPickup.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace HockeyPickup.Api.Data.Context;

[ExcludeFromCodeCoverage]
public partial class HockeyPickupContext : IdentityDbContext<AspNetUser, AspNetRole, string>
{
    public HockeyPickupContext(DbContextOptions<HockeyPickupContext> options) : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<string>().HaveMaxLength(256);  // Default max length for string properties
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Don't call base.OnModelCreating(modelBuilder) since we're configuring everything manually

        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.ToTable("AspNetUsers");
            entity.HasKey(e => e.Id).HasName("PK_dbo.AspNetUsers");

            // Map existing columns with their exact types from your DB
            entity.Property(e => e.Id).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.EmailConfirmed);
            entity.Property(e => e.PasswordHash).HasColumnType("nvarchar(max)");
            entity.Property(e => e.SecurityStamp).HasColumnType("nvarchar(max)");
            entity.Property(e => e.PhoneNumber).HasColumnType("nvarchar(max)");
            entity.Property(e => e.PhoneNumberConfirmed);
            entity.Property(e => e.TwoFactorEnabled);
            entity.Property(e => e.LockoutEndDateUtc)
                        .HasColumnName("LockoutEndDateUtc")
                        .HasColumnType("datetime");

            // Ignore LockoutEnd as it's handled through LockoutEndDateUtc
            entity.Ignore(e => e.LockoutEnd);
            entity.Property(e => e.LockoutEnabled);
            entity.Property(e => e.AccessFailedCount);
            entity.Property(e => e.UserName).HasMaxLength(256).IsRequired();

            // Custom columns
            entity.Property(e => e.FirstName).HasColumnType("nvarchar(max)");
            entity.Property(e => e.LastName).HasColumnType("nvarchar(max)");
            entity.Property(e => e.NotificationPreference).HasDefaultValue(1);
            entity.Property(e => e.PayPalEmail).HasColumnType("nvarchar(max)").HasDefaultValue("");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.Preferred).HasDefaultValue(false);
            entity.Property(e => e.VenmoAccount).HasColumnType("nvarchar(max)");
            entity.Property(e => e.MobileLast4).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Rating).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.PreferredPlus).HasDefaultValue(false);
            entity.Property(e => e.EmergencyName).HasColumnType("nvarchar(max)");
            entity.Property(e => e.EmergencyPhone).HasColumnType("nvarchar(max)");
            entity.Property(e => e.LockerRoom13).HasDefaultValue(false);

            // New Identity columns
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.ConcurrencyStamp).HasColumnType("nvarchar(max)");
            entity.Property(e => e.DateCreated).HasColumnType("datetime");
        });

        modelBuilder.Entity<AspNetRole>(entity =>
        {
            entity.ToTable("AspNetRoles");
            entity.HasKey(e => e.Id).HasName("PK_dbo.AspNetRoles");

            entity.Property(e => e.Id).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.NormalizedName).HasMaxLength(256);
            entity.Property(e => e.ConcurrencyStamp).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.ToTable("AspNetUserRoles");
            entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("PK_dbo.AspNetUserRoles");

            entity.Property(e => e.UserId).HasMaxLength(128);
            entity.Property(e => e.RoleId).HasMaxLength(128);

            // Configure existing foreign keys
            entity.HasOne<AspNetRole>()
                .WithMany()
                .HasForeignKey(ur => ur.RoleId)
                .HasConstraintName("FK_dbo.AspNetUserRoles_dbo.AspNetRoles_RoleId");

            entity.HasOne<AspNetUser>()
                .WithMany()
                .HasForeignKey(ur => ur.UserId)
                .HasConstraintName("FK_dbo.AspNetUserRoles_dbo.AspNetUsers_UserId");
        });

        modelBuilder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.ToTable("AspNetUserLogins");
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey });

            entity.Property(e => e.LoginProvider).HasMaxLength(128);
            entity.Property(e => e.ProviderKey).HasMaxLength(128);
            entity.Property(e => e.UserId).HasMaxLength(128);

            entity.HasOne<AspNetUser>()
                .WithMany()
                .HasForeignKey(ul => ul.UserId)
                .HasConstraintName("FK_dbo.AspNetUserLogins_dbo.AspNetUsers_UserId");
        });

        modelBuilder.Entity<IdentityUserClaim<string>>(entity =>
        {
            entity.ToTable("AspNetUserClaims");
            entity.HasKey(e => e.Id).HasName("PK_dbo.AspNetUserClaims");

            entity.Property(e => e.UserId).HasMaxLength(128);
            entity.Property(e => e.ClaimType).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ClaimValue).HasColumnType("nvarchar(max)");

            entity.HasOne<AspNetUser>()
                .WithMany()
                .HasForeignKey(uc => uc.UserId)
                .HasConstraintName("FK_dbo.AspNetUserClaims_dbo.AspNetUsers_UserId");
        });

        modelBuilder.Entity<IdentityRoleClaim<string>>(entity =>
        {
            entity.ToTable("AspNetRoleClaims");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RoleId).HasMaxLength(128);
            entity.Property(e => e.ClaimType).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ClaimValue).HasColumnType("nvarchar(max)");

            entity.HasOne<AspNetRole>()
                .WithMany()
                .HasForeignKey(rc => rc.RoleId)
                .HasConstraintName("FK_dbo.AspNetRoleClaims_dbo.AspNetRoles_RoleId");
        });

        modelBuilder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.ToTable("AspNetUserTokens");
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });

            entity.Property(e => e.UserId).HasMaxLength(128);
            entity.Property(e => e.LoginProvider).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Value).HasColumnType("nvarchar(max)");

            entity.HasOne<AspNetUser>()
                .WithMany()
                .HasForeignKey(ut => ut.UserId)
                .HasConstraintName("FK_dbo.AspNetUserTokens_dbo.AspNetUsers_UserId");
        });

        // Configure many-to-many relationship
        modelBuilder.Entity<AspNetUser>()
            .HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity<IdentityUserRole<string>>();

        modelBuilder.HasAnnotation("Relational:IsStoredInDatabase", true);
    }
}
