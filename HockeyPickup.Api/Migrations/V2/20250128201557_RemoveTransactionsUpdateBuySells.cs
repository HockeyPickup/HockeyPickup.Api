using Microsoft.EntityFrameworkCore.Migrations;
using System.Diagnostics.CodeAnalysis;

#nullable disable

namespace HockeyPickup.Api.Migrations.V2
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class RemoveTransactionsUpdateBuySells : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                        name: "TransactionHistory");

            migrationBuilder.DropTable(
                name: "Transactions");

            // Add new columns to BuySells
            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "BuySells",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "BuySells",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreateByUserId",
                table: "BuySells",
                type: "nvarchar(128)",
                nullable: false,
                defaultValue: "");  // Will need data migration for existing records

            migrationBuilder.AddColumn<string>(
                name: "UpdateByUserId",
                table: "BuySells",
                type: "nvarchar(128)",
                nullable: false,
                defaultValue: "");  // Will need data migration for existing records

            // Remove TeamAssignment default constraint
            migrationBuilder.Sql(@"
            DECLARE @sql nvarchar(max)
            SELECT @sql = 'ALTER TABLE [dbo].[BuySells] DROP CONSTRAINT ' + dc.name
            FROM sys.default_constraints dc
            JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE OBJECT_NAME(dc.parent_object_id) = 'BuySells'
            AND c.name = 'TeamAssignment'
            IF @sql IS NOT NULL EXEC(@sql)");

            // Add computed TransactionStatus column
            migrationBuilder.Sql(@"
            ALTER TABLE [dbo].[BuySells] ADD TransactionStatus AS 
            CASE 
                WHEN SellerUserId IS NULL AND BuyerUserId IS NOT NULL THEN 'Looking to Buy'
                WHEN BuyerUserId IS NULL AND SellerUserId IS NOT NULL THEN 'Available to Buy'
                WHEN BuyerUserId IS NOT NULL AND SellerUserId IS NOT NULL THEN
                    CASE
                        WHEN PaymentSent = 1 AND PaymentReceived = 1 THEN 'Complete'
                        WHEN PaymentSent = 1 THEN 'Payment Sent'
                        ELSE 'Payment Pending'
                    END
                ELSE 'Unknown'
            END PERSISTED");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [dbo].[BuySells] DROP COLUMN TransactionStatus");

            // Remove added columns
            migrationBuilder.DropColumn(
                name: "Price",
                table: "BuySells");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "BuySells");

            migrationBuilder.DropColumn(
                name: "CreateByUserId",
                table: "BuySells");

            migrationBuilder.DropColumn(
                name: "UpdateByUserId",
                table: "BuySells");

            // Restore TeamAssignment default
            migrationBuilder.Sql("ALTER TABLE [dbo].[BuySells] ADD CONSTRAINT [DF_BuySells_TeamAssignment] DEFAULT (0) FOR [TeamAssignment]");
        }
    }
}
