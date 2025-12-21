using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TorreClou.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deposit_Users_UserId",
                table: "Deposit");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Voucher_VoucherId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_UserVoucherUsage_Users_UserId",
                table: "UserVoucherUsage");

            migrationBuilder.DropForeignKey(
                name: "FK_UserVoucherUsage_Voucher_VoucherId",
                table: "UserVoucherUsage");

            migrationBuilder.DropIndex(
                name: "IX_RequestedFiles_InfoHash",
                table: "RequestedFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Voucher",
                table: "Voucher");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserVoucherUsage",
                table: "UserVoucherUsage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FlashSale",
                table: "FlashSale");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Deposit",
                table: "Deposit");

            migrationBuilder.RenameTable(
                name: "Voucher",
                newName: "Vouchers");

            migrationBuilder.RenameTable(
                name: "UserVoucherUsage",
                newName: "UserVoucherUsages");

            migrationBuilder.RenameTable(
                name: "FlashSale",
                newName: "FlashSales");

            migrationBuilder.RenameTable(
                name: "Deposit",
                newName: "Deposits");

            migrationBuilder.RenameIndex(
                name: "IX_Voucher_Code",
                table: "Vouchers",
                newName: "IX_Vouchers_Code");

            migrationBuilder.RenameIndex(
                name: "IX_UserVoucherUsage_VoucherId",
                table: "UserVoucherUsages",
                newName: "IX_UserVoucherUsages_VoucherId");

            migrationBuilder.RenameIndex(
                name: "IX_UserVoucherUsage_UserId",
                table: "UserVoucherUsages",
                newName: "IX_UserVoucherUsages_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Deposit_UserId",
                table: "Deposits",
                newName: "IX_Deposits_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Deposit_GatewayTransactionId",
                table: "Deposits",
                newName: "IX_Deposits_GatewayTransactionId");

            migrationBuilder.AddColumn<string>(
                name: "HangfireJobId",
                table: "Syncs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartbeat",
                table: "Syncs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Vouchers",
                table: "Vouchers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserVoucherUsages",
                table: "UserVoucherUsages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FlashSales",
                table: "FlashSales",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Deposits",
                table: "Deposits",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_RequestedFiles_InfoHash_UploadedByUserId",
                table: "RequestedFiles",
                columns: new[] { "InfoHash", "UploadedByUserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Deposits_Users_UserId",
                table: "Deposits",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Vouchers_VoucherId",
                table: "Invoices",
                column: "VoucherId",
                principalTable: "Vouchers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserVoucherUsages_Users_UserId",
                table: "UserVoucherUsages",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserVoucherUsages_Vouchers_VoucherId",
                table: "UserVoucherUsages",
                column: "VoucherId",
                principalTable: "Vouchers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deposits_Users_UserId",
                table: "Deposits");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Vouchers_VoucherId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_UserVoucherUsages_Users_UserId",
                table: "UserVoucherUsages");

            migrationBuilder.DropForeignKey(
                name: "FK_UserVoucherUsages_Vouchers_VoucherId",
                table: "UserVoucherUsages");

            migrationBuilder.DropIndex(
                name: "IX_RequestedFiles_InfoHash_UploadedByUserId",
                table: "RequestedFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Vouchers",
                table: "Vouchers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserVoucherUsages",
                table: "UserVoucherUsages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FlashSales",
                table: "FlashSales");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Deposits",
                table: "Deposits");

            migrationBuilder.DropColumn(
                name: "HangfireJobId",
                table: "Syncs");

            migrationBuilder.DropColumn(
                name: "LastHeartbeat",
                table: "Syncs");

            migrationBuilder.RenameTable(
                name: "Vouchers",
                newName: "Voucher");

            migrationBuilder.RenameTable(
                name: "UserVoucherUsages",
                newName: "UserVoucherUsage");

            migrationBuilder.RenameTable(
                name: "FlashSales",
                newName: "FlashSale");

            migrationBuilder.RenameTable(
                name: "Deposits",
                newName: "Deposit");

            migrationBuilder.RenameIndex(
                name: "IX_Vouchers_Code",
                table: "Voucher",
                newName: "IX_Voucher_Code");

            migrationBuilder.RenameIndex(
                name: "IX_UserVoucherUsages_VoucherId",
                table: "UserVoucherUsage",
                newName: "IX_UserVoucherUsage_VoucherId");

            migrationBuilder.RenameIndex(
                name: "IX_UserVoucherUsages_UserId",
                table: "UserVoucherUsage",
                newName: "IX_UserVoucherUsage_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Deposits_UserId",
                table: "Deposit",
                newName: "IX_Deposit_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Deposits_GatewayTransactionId",
                table: "Deposit",
                newName: "IX_Deposit_GatewayTransactionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Voucher",
                table: "Voucher",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserVoucherUsage",
                table: "UserVoucherUsage",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FlashSale",
                table: "FlashSale",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Deposit",
                table: "Deposit",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_RequestedFiles_InfoHash",
                table: "RequestedFiles",
                column: "InfoHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Deposit_Users_UserId",
                table: "Deposit",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Voucher_VoucherId",
                table: "Invoices",
                column: "VoucherId",
                principalTable: "Voucher",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserVoucherUsage_Users_UserId",
                table: "UserVoucherUsage",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserVoucherUsage_Voucher_VoucherId",
                table: "UserVoucherUsage",
                column: "VoucherId",
                principalTable: "Voucher",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
