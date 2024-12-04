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

    public DbSet<Session>? Sessions { get; set; }
    public DbSet<RegularSet>? RegularSets { get; set; }
    public DbSet<Regular>? Regulars { get; set; }
    public DbSet<BuySell>? BuySells { get; set; }
    public DbSet<ActivityLog>? ActivityLogs { get; set; }
    public DbSet<RosterPlayer>? CurrentSessionRosters { get; set; }
    public DbSet<BuyingQueue>? SessionBuyingQueues { get; set; }

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

            entity.HasMany(u => u.BuyerTransactions)
                    .WithOne(b => b.Buyer)
                    .HasForeignKey(b => b.BuyerUserId)
                    .IsRequired(false);

            entity.HasMany(u => u.SellerTransactions)
                .WithOne(b => b.Seller)
                .HasForeignKey(b => b.SellerUserId)
                .IsRequired(false);
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

        modelBuilder.Entity<Session>(entity =>
        {
            entity.ToTable("Sessions");
            entity.HasKey(e => e.SessionId).HasName("PK_dbo.Sessions");

            entity.Property(e => e.CreateDateTime)
                .HasColumnType("datetime")
                .HasDefaultValue("1900-01-01T00:00:00.000");

            entity.Property(e => e.UpdateDateTime)
                .HasColumnType("datetime")
                .HasDefaultValue("1900-01-01T00:00:00.000");

            entity.Property(e => e.Note).HasColumnType("nvarchar(max)");

            entity.Property(e => e.SessionDate)
                .HasColumnType("datetime")
                .HasDefaultValue("1900-01-01T00:00:00.000");

            entity.HasOne(e => e.RegularSet)
                .WithMany(r => r.Sessions)
                .HasForeignKey(e => e.RegularSetId)
                .HasConstraintName("FK_dbo.Sessions_dbo.RegularSets_RegularSetId");

            entity.HasMany(s => s.CurrentRosters)
                    .WithOne()
                    .HasForeignKey(r => r.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(s => s.BuyingQueues)
                .WithOne()
                .HasForeignKey(q => q.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure BuySells
        modelBuilder.Entity<BuySell>(entity =>
        {
            entity.ToTable("BuySells");
            entity.HasKey(e => e.BuySellId).HasName("PK_dbo.BuySells");

            entity.Property(e => e.CreateDateTime).HasColumnType("datetime");
            entity.Property(e => e.UpdateDateTime).HasColumnType("datetime");
            entity.Property(e => e.BuyerNote).HasColumnType("nvarchar(max)");
            entity.Property(e => e.SellerNote).HasColumnType("nvarchar(max)");
            entity.Property(e => e.TeamAssignment).HasDefaultValue(0);
            entity.Property(e => e.SellerNoteFlagged).HasDefaultValue(false);
            entity.Property(e => e.BuyerNoteFlagged).HasDefaultValue(false);

            entity.HasOne(e => e.Session)
                    .WithMany(s => s.BuySells)
                    .HasForeignKey(e => e.SessionId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_dbo.BuySells_dbo.Sessions_SessionId")
                    .IsRequired(); // Make this relationship required

            // Configure one-way relationships for Buyer/Seller
            entity.HasOne(e => e.Buyer)
                .WithMany(u => u.BuyerTransactions)
                .HasForeignKey(e => e.BuyerUserId)
                .HasConstraintName("FK_dbo.BuySells_dbo.AspNetUsers_BuyerUserId")
                .IsRequired(false)  // Explicitly mark as optional
                .OnDelete(DeleteBehavior.Restrict);  // Prevent cascading deletes

            entity.HasOne(e => e.Seller)
                .WithMany(u => u.SellerTransactions)
                .HasForeignKey(e => e.SellerUserId)
                .HasConstraintName("FK_dbo.BuySells_dbo.AspNetUsers_SellerUserId")
                .IsRequired(false)  // Explicitly mark as optional
                .OnDelete(DeleteBehavior.Restrict);  // Prevent cascading deletes
        });

            // Configure RegularSets
        modelBuilder.Entity<RegularSet>(entity =>
        {
            entity.ToTable("RegularSets");
            entity.HasKey(e => e.RegularSetId).HasName("PK_dbo.RegularSets");

            entity.Property(e => e.Description).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreateDateTime).HasColumnType("datetime");

            entity.HasMany(e => e.Regulars)
                    .WithOne(r => r.RegularSet)
                    .HasForeignKey(r => r.RegularSetId)
                    .HasConstraintName("FK_dbo.Regulars_dbo.RegularSets_RegularSetId");
        });

        // Configure Regulars (composite key)
        modelBuilder.Entity<Regular>(entity =>
        {
            entity.ToTable("Regulars");
            entity.HasKey(e => new { e.RegularSetId, e.UserId }).HasName("PK_dbo.Regulars");

            entity.HasOne(e => e.RegularSet)
                .WithMany(r => r.Regulars)
                .HasForeignKey(e => e.RegularSetId);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Regulars)
                .HasForeignKey(e => e.UserId);
        });

        // Configure ActivityLogs
        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.ToTable("ActivityLogs");
            entity.HasKey(e => e.ActivityLogId).HasName("PK_dbo.ActivityLogs");

            entity.Property(e => e.CreateDateTime).HasColumnType("datetime");
            entity.Property(e => e.Activity).HasColumnType("nvarchar(max)");

            entity.HasOne(e => e.Session)
                .WithMany(s => s.ActivityLogs)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_dbo.ActivityLogs_dbo.Sessions_SessionId");

            entity.HasOne(e => e.User)
                .WithMany(u => u.ActivityLogs)
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("FK_dbo.ActivityLogs_dbo.AspNetUsers_UserId");
        });

        // Configure Views
        modelBuilder.Entity<RosterPlayer>(entity =>
        {
            entity.ToView("CurrentSessionRoster");
            entity.HasKey(r => r.SessionRosterId);

            entity.Property(r => r.PlayerStatus).HasMaxLength(50);
            entity.Property(r => r.Rating).HasColumnType("decimal(18,2)");
            entity.Property(r => r.UserId).HasMaxLength(128);
            entity.Property(r => r.FirstName).HasMaxLength(256);
            entity.Property(r => r.LastName).HasMaxLength(256);
        });

        modelBuilder.Entity<BuyingQueue>(entity =>
        {
            entity.ToView("SessionBuyingQueue");
            entity.HasKey(q => q.BuySellId);

            entity.Property(q => q.TransactionStatus).HasMaxLength(50);
            entity.Property(q => q.QueueStatus).HasMaxLength(50);
            entity.Property(q => q.BuyerName).HasMaxLength(512);
            entity.Property(q => q.SellerName).HasMaxLength(512);
            entity.Property(q => q.BuyerNote).HasColumnType("nvarchar(max)");
            entity.Property(q => q.SellerNote).HasColumnType("nvarchar(max)");
        });

        modelBuilder.HasAnnotation("Relational:IsStoredInDatabase", true);
    }
}
