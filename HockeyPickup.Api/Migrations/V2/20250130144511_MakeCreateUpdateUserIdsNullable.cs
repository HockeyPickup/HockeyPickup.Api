using Microsoft.EntityFrameworkCore.Migrations;
using System.Diagnostics.CodeAnalysis;

#nullable disable

namespace HockeyPickup.Api.Migrations.V2
{
    [ExcludeFromCodeCoverage]
    public partial class MakeCreateUpdateUserIdsNullable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "BuySells",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<bool>(
                name: "PaymentSent",
                table: "BuySells",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "PaymentReceived",
                table: "BuySells",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<int>(
                name: "PaymentMethod",
                table: "BuySells",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "TransactionStatus",
                table: "BuySells",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                computedColumnSql: "case \n            when [SellerUserId] IS NULL AND [BuyerUserId] IS NOT NULL then 'Looking to Buy' \n            when [BuyerUserId] IS NULL AND [SellerUserId] IS NOT NULL then 'Available to Buy'  \n            when [BuyerUserId] IS NOT NULL AND [SellerUserId] IS NOT NULL then \n                case when [PaymentSent]=(1) AND [PaymentReceived]=(1) then 'Complete' \n                when [PaymentSent]=(1) then 'Payment Sent' \n                else 'Payment Pending' end \n            else 'Unknown' end",
                stored: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TransactionStatus",
                table: "BuySells",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldComputedColumnSql: "case \n            when [SellerUserId] IS NULL AND [BuyerUserId] IS NOT NULL then 'Looking to Buy' \n            when [BuyerUserId] IS NULL AND [SellerUserId] IS NOT NULL then 'Available to Buy'  \n            when [BuyerUserId] IS NOT NULL AND [SellerUserId] IS NOT NULL then \n                case when [PaymentSent]=(1) AND [PaymentReceived]=(1) then 'Complete' \n                when [PaymentSent]=(1) then 'Payment Sent' \n                else 'Payment Pending' end \n            else 'Unknown' end");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "BuySells",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "PaymentSent",
                table: "BuySells",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "PaymentReceived",
                table: "BuySells",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "PaymentMethod",
                table: "BuySells",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}