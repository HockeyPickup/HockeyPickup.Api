using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HockeyPickup.Api.Migrations.V2
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class AddLottery : Migration
    {
        // Lottery-only additive migration. Unrelated model-snapshot drift (Identity FK/index conventions and the
        // SessionBuyingQueue view) was intentionally removed from the scaffolded output so this migration only adds
        // the two Sessions columns and the new SessionLotteryEntrants table.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LotteryEnabled",
                table: "Sessions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "LotteryEntryWindowMinutes",
                table: "Sessions",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.CreateTable(
                name: "SessionLotteryEntrants",
                columns: table => new
                {
                    LotteryEntrantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", nullable: false),
                    LotteryClass = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 1.0m),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DrawOrder = table.Column<int>(type: "int", nullable: true),
                    DrawDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(512)", nullable: true),
                    CreateDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.SessionLotteryEntrants", x => x.LotteryEntrantId);
                    table.ForeignKey(
                        name: "FK_SessionLotteryEntrants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionLotteryEntrants_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionLotteryEntrants_UserId",
                table: "SessionLotteryEntrants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_SessionLotteryEntrants_Session_User",
                table: "SessionLotteryEntrants",
                columns: new[] { "SessionId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionLotteryEntrants");

            migrationBuilder.DropColumn(
                name: "LotteryEnabled",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "LotteryEntryWindowMinutes",
                table: "Sessions");
        }
    }
}
