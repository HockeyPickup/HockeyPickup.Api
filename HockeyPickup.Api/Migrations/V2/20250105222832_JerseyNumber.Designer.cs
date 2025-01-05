﻿// <auto-generated />
using System;
using HockeyPickup.Api.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace HockeyPickup.Api.Migrations.V2
{
    [DbContext(typeof(HockeyPickupContext))]
    [Migration("20250105222832_JerseyNumber")]
    partial class JerseyNumber
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Relational:IsStoredInDatabase", true)
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.ActivityLog", b =>
                {
                    b.Property<int>("ActivityLogId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ActivityLogId"));

                    b.Property<string>("Activity")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreateDateTime")
                        .HasColumnType("datetime");

                    b.Property<int>("SessionId")
                        .HasColumnType("int");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.HasKey("ActivityLogId")
                        .HasName("PK_dbo.ActivityLogs");

                    b.HasIndex("SessionId");

                    b.HasIndex("UserId");

                    b.ToTable("ActivityLogs", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.AspNetRole", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<string>("ConcurrencyStamp")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("Id")
                        .HasName("PK_dbo.AspNetRoles");

                    b.ToTable("AspNetRoles", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.AspNetUser", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<int>("AccessFailedCount")
                        .HasColumnType("int");

                    b.Property<bool>("Active")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(true);

                    b.Property<string>("ConcurrencyStamp")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime");

                    b.Property<string>("Email")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<bool>("EmailConfirmed")
                        .HasColumnType("bit");

                    b.Property<string>("EmergencyName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("EmergencyPhone")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FirstName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("JerseyNumber")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<string>("LastName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("LockerRoom13")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("LockoutEnabled")
                        .HasColumnType("bit");

                    b.Property<DateTime?>("LockoutEndDateUtc")
                        .HasColumnType("datetime")
                        .HasColumnName("LockoutEndDateUtc");

                    b.Property<string>("MobileLast4")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<int>("NotificationPreference")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(1);

                    b.Property<string>("PasswordHash")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PayPalEmail")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)")
                        .HasDefaultValue("");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("PhoneNumberConfirmed")
                        .HasColumnType("bit");

                    b.Property<string>("PhotoUrl")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(512)");

                    b.Property<int>("PositionPreference")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(1);

                    b.Property<bool>("Preferred")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("PreferredPlus")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<decimal>("Rating")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("decimal(18,2)")
                        .HasDefaultValue(0m);

                    b.Property<string>("SecurityStamp")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("TwoFactorEnabled")
                        .HasColumnType("bit");

                    b.Property<string>("UserName")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("VenmoAccount")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id")
                        .HasName("PK_dbo.AspNetUsers");

                    b.ToTable("AspNetUsers", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.BuySell", b =>
                {
                    b.Property<int>("BuySellId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("BuySellId"));

                    b.Property<string>("BuyerNote")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("BuyerNoteFlagged")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<string>("BuyerUserId")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<DateTime>("CreateDateTime")
                        .HasColumnType("datetime");

                    b.Property<bool>("PaymentReceived")
                        .HasColumnType("bit");

                    b.Property<bool>("PaymentSent")
                        .HasColumnType("bit");

                    b.Property<string>("SellerNote")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("SellerNoteFlagged")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<string>("SellerUserId")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<int>("SessionId")
                        .HasColumnType("int");

                    b.Property<int>("TeamAssignment")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<DateTime>("UpdateDateTime")
                        .HasColumnType("datetime");

                    b.HasKey("BuySellId")
                        .HasName("PK_dbo.BuySells");

                    b.HasIndex("BuyerUserId");

                    b.HasIndex("SellerUserId");

                    b.HasIndex("SessionId");

                    b.ToTable("BuySells", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.BuyingQueue", b =>
                {
                    b.Property<int>("BuySellId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("BuySellId"));

                    b.Property<string>("BuyerName")
                        .HasMaxLength(512)
                        .HasColumnType("nvarchar(512)");

                    b.Property<string>("BuyerNote")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("PaymentReceived")
                        .HasColumnType("bit");

                    b.Property<bool>("PaymentSent")
                        .HasColumnType("bit");

                    b.Property<string>("QueueStatus")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<string>("SellerName")
                        .HasMaxLength(512)
                        .HasColumnType("nvarchar(512)");

                    b.Property<string>("SellerNote")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("SessionId")
                        .HasColumnType("int");

                    b.Property<int>("TeamAssignment")
                        .HasColumnType("int");

                    b.Property<string>("TransactionStatus")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.HasKey("BuySellId");

                    b.HasIndex("SessionId");

                    b.ToTable("SessionBuyingQueue");

                    b.ToView("SessionBuyingQueue", (string)null);
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.Regular", b =>
                {
                    b.Property<int>("RegularSetId")
                        .HasColumnType("int")
                        .HasColumnOrder(0);

                    b.Property<string>("UserId")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)")
                        .HasColumnOrder(1);

                    b.Property<int>("PositionPreference")
                        .HasColumnType("int");

                    b.Property<int>("TeamAssignment")
                        .HasColumnType("int");

                    b.HasKey("RegularSetId", "UserId")
                        .HasName("PK_dbo.Regulars");

                    b.HasIndex("UserId");

                    b.ToTable("Regulars", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.RegularSet", b =>
                {
                    b.Property<int>("RegularSetId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("RegularSetId"));

                    b.Property<bool>("Archived")
                        .HasColumnType("bit");

                    b.Property<DateTime>("CreateDateTime")
                        .HasColumnType("datetime");

                    b.Property<int>("DayOfWeek")
                        .HasColumnType("int");

                    b.Property<string>("Description")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("RegularSetId")
                        .HasName("PK_dbo.RegularSets");

                    b.ToTable("RegularSets", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.RosterPlayer", b =>
                {
                    b.Property<int>("SessionRosterId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("SessionRosterId"));

                    b.Property<string>("CurrentPosition")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<bool>("IsPlaying")
                        .HasColumnType("bit");

                    b.Property<bool>("IsRegular")
                        .HasColumnType("bit");

                    b.Property<DateTime>("JoinedDateTime")
                        .HasColumnType("datetime2");

                    b.Property<int?>("LastBuySellId")
                        .HasColumnType("int");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("PhotoUrl")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("PlayerStatus")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<int>("Position")
                        .HasColumnType("int");

                    b.Property<bool>("Preferred")
                        .HasColumnType("bit");

                    b.Property<bool>("PreferredPlus")
                        .HasColumnType("bit");

                    b.Property<decimal>("Rating")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("SessionId")
                        .HasColumnType("int");

                    b.Property<int>("TeamAssignment")
                        .HasColumnType("int");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.HasKey("SessionRosterId");

                    b.HasIndex("SessionId");

                    b.ToTable("CurrentSessionRoster");

                    b.ToView("CurrentSessionRoster", (string)null);
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.Session", b =>
                {
                    b.Property<int>("SessionId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("SessionId"));

                    b.Property<int?>("BuyDayMinimum")
                        .HasColumnType("int");

                    b.Property<decimal?>("Cost")
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTime>("CreateDateTime")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime")
                        .HasDefaultValue(new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

                    b.Property<string>("Note")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("RegularSetId")
                        .HasColumnType("int");

                    b.Property<DateTime>("SessionDate")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime")
                        .HasDefaultValue(new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

                    b.Property<DateTime>("UpdateDateTime")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime")
                        .HasDefaultValue(new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

                    b.HasKey("SessionId")
                        .HasName("PK_dbo.Sessions");

                    b.HasIndex("RegularSetId");

                    b.ToTable("Sessions", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.SessionRoster", b =>
                {
                    b.Property<int>("SessionRosterId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("SessionRosterId"));

                    b.Property<bool>("IsPlaying")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(true);

                    b.Property<bool>("IsRegular")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<DateTime>("JoinedDateTime")
                        .HasColumnType("datetime");

                    b.Property<int?>("LastBuySellId")
                        .HasColumnType("int");

                    b.Property<DateTime?>("LeftDateTime")
                        .HasColumnType("datetime");

                    b.Property<int>("Position")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(2);

                    b.Property<int>("SessionId")
                        .HasColumnType("int");

                    b.Property<int>("TeamAssignment")
                        .HasColumnType("int");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.HasKey("SessionRosterId")
                        .HasName("PK_dbo.SessionRosters");

                    b.HasIndex("SessionId");

                    b.HasIndex("UserId");

                    b.ToTable("SessionRosters", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClaimType")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClaimValue")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RoleId")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetRoleClaims", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClaimType")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClaimValue")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UserId")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.HasKey("Id")
                        .HasName("PK_dbo.AspNetUserClaims");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserClaims", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.Property<string>("LoginProvider")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<string>("ProviderKey")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<string>("ProviderDisplayName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("UserId")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserLogins", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<string>("RoleId")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.HasKey("UserId", "RoleId")
                        .HasName("PK_dbo.AspNetUserRoles");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetUserRoles", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<string>("LoginProvider")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<string>("Name")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<string>("Value")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("AspNetUserTokens", (string)null);

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.ActivityLog", b =>
                {
                    b.HasOne("HockeyPickup.Api.Data.Entities.Session", "Session")
                        .WithMany("ActivityLogs")
                        .HasForeignKey("SessionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("FK_dbo.ActivityLogs_dbo.Sessions_SessionId");

                    b.HasOne("HockeyPickup.Api.Data.Entities.AspNetUser", "User")
                        .WithMany("ActivityLogs")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("FK_dbo.ActivityLogs_dbo.AspNetUsers_UserId");

                    b.Navigation("Session");

                    b.Navigation("User");
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.BuySell", b =>
                {
                    b.HasOne("HockeyPickup.Api.Data.Entities.AspNetUser", "Buyer")
                        .WithMany("BuyerTransactions")
                        .HasForeignKey("BuyerUserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .HasConstraintName("FK_dbo.BuySells_dbo.AspNetUsers_BuyerUserId");

                    b.HasOne("HockeyPickup.Api.Data.Entities.AspNetUser", "Seller")
                        .WithMany("SellerTransactions")
                        .HasForeignKey("SellerUserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .HasConstraintName("FK_dbo.BuySells_dbo.AspNetUsers_SellerUserId");

                    b.HasOne("HockeyPickup.Api.Data.Entities.Session", "Session")
                        .WithMany("BuySells")
                        .HasForeignKey("SessionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("FK_dbo.BuySells_dbo.Sessions_SessionId");

                    b.Navigation("Buyer");

                    b.Navigation("Seller");

                    b.Navigation("Session");
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.BuyingQueue", b =>
                {
                    b.HasOne("HockeyPickup.Api.Data.Entities.Session", null)
                        .WithMany("BuyingQueues")
                        .HasForeignKey("SessionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.Regular", b =>
                {
                    b.HasOne("HockeyPickup.Api.Data.Entities.RegularSet", "RegularSet")
                        .WithMany("Regulars")
                        .HasForeignKey("RegularSetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("FK_dbo.Regulars_dbo.RegularSets_RegularSetId");

                    b.HasOne("HockeyPickup.Api.Data.Entities.AspNetUser", "User")
                        .WithMany("Regulars")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("RegularSet");

                    b.Navigation("User");
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.RosterPlayer", b =>
                {
                    b.HasOne("HockeyPickup.Api.Data.Entities.Session", null)
                        .WithMany("CurrentRosters")
                        .HasForeignKey("SessionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.Session", b =>
                {
                    b.HasOne("HockeyPickup.Api.Data.Entities.RegularSet", "RegularSet")
                        .WithMany("Sessions")
                        .HasForeignKey("RegularSetId")
                        .HasConstraintName("FK_dbo.Sessions_dbo.RegularSets_RegularSetId");

                    b.Navigation("RegularSet");
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.SessionRoster", b =>
                {
                    b.HasOne("HockeyPickup.Api.Data.Entities.Session", "Session")
                        .WithMany()
                        .HasForeignKey("SessionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("FK_dbo.SessionRosters_Sessions");

                    b.HasOne("HockeyPickup.Api.Data.Entities.AspNetUser", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("FK_dbo.SessionRosters_AspNetUsers");

                    b.Navigation("Session");

                    b.Navigation("User");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.HasOne("HockeyPickup.Api.Data.Entities.AspNetRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .HasConstraintName("FK_dbo.AspNetRoleClaims_dbo.AspNetRoles_RoleId");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.HasOne("HockeyPickup.Api.Data.Entities.AspNetUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .HasConstraintName("FK_dbo.AspNetUserClaims_dbo.AspNetUsers_UserId");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.HasOne("HockeyPickup.Api.Data.Entities.AspNetUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .HasConstraintName("FK_dbo.AspNetUserLogins_dbo.AspNetUsers_UserId");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.HasOne("HockeyPickup.Api.Data.Entities.AspNetRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("FK_dbo.AspNetUserRoles_dbo.AspNetRoles_RoleId");

                    b.HasOne("HockeyPickup.Api.Data.Entities.AspNetUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("FK_dbo.AspNetUserRoles_dbo.AspNetUsers_UserId");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.HasOne("HockeyPickup.Api.Data.Entities.AspNetUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("FK_dbo.AspNetUserTokens_dbo.AspNetUsers_UserId");
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.AspNetUser", b =>
                {
                    b.Navigation("ActivityLogs");

                    b.Navigation("BuyerTransactions");

                    b.Navigation("Regulars");

                    b.Navigation("SellerTransactions");
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.RegularSet", b =>
                {
                    b.Navigation("Regulars");

                    b.Navigation("Sessions");
                });

            modelBuilder.Entity("HockeyPickup.Api.Data.Entities.Session", b =>
                {
                    b.Navigation("ActivityLogs");

                    b.Navigation("BuySells");

                    b.Navigation("BuyingQueues");

                    b.Navigation("CurrentRosters");
                });
#pragma warning restore 612, 618
        }
    }
}
