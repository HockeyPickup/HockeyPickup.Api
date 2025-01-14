using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HockeyPickup.Api.Migrations.V2
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class AddUserPaymentMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create new table
            migrationBuilder.CreateTable(
                name: "UserPaymentMethods",
                columns: table => new
                {
                    UserPaymentMethodId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(maxLength: 128, nullable: false),
                    MethodType = table.Column<int>(nullable: false),
                    Identifier = table.Column<string>(maxLength: 256, nullable: false),
                    PreferenceOrder = table.Column<int>(nullable: false),
                    IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPaymentMethods", x => x.UserPaymentMethodId);
                    table.ForeignKey(
                        name: "FK_UserPaymentMethods_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_UserPaymentMethods_UserId_MethodType",
                table: "UserPaymentMethods",
                columns: new[] { "UserId", "MethodType" },
                unique: true);

            migrationBuilder.Sql(@"
            INSERT INTO UserPaymentMethods (UserId, MethodType, Identifier, PreferenceOrder, IsActive)
            SELECT Id, 1, PayPalEmail, 2, 1
            FROM AspNetUsers
            WHERE PayPalEmail != '' AND PayPalEmail IS NOT NULL");

            // Data migration - move existing Venmo accounts
            migrationBuilder.Sql(@"
            INSERT INTO UserPaymentMethods (UserId, MethodType, Identifier, PreferenceOrder, IsActive)
            SELECT Id, 2, VenmoAccount, 1, 1
            FROM AspNetUsers
            WHERE VenmoAccount != '' AND VenmoAccount IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserPaymentMethods");
        }
    }
}
