using Microsoft.EntityFrameworkCore.Migrations;
using System.Diagnostics.CodeAnalysis;

#nullable disable

namespace HockeyPickup.Api.Migrations.V2
{
    [ExcludeFromCodeCoverage]
    public partial class FixEnumSentinelValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Set default NotificationPreference to 2 (OnlyMyBuySell)
            migrationBuilder.AlterColumn<int>(
                name: "NotificationPreference",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 2,  // OnlyMyBuySell
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            // Set default PositionPreference to 0 (TBD)
            migrationBuilder.AlterColumn<int>(
                name: "PositionPreference",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0,  // TBD
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            // Set default Position to 0 (TBD)
            migrationBuilder.AlterColumn<int>(
                name: "Position",
                table: "SessionRosters",
                type: "int",
                nullable: false,
                defaultValue: 0,  // TBD
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "NotificationPreference",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 2);

            migrationBuilder.AlterColumn<int>(
                name: "PositionPreference",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "Position",
                table: "SessionRosters",
                type: "int",
                nullable: false,
                defaultValue: 2,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);
        }
    }
}