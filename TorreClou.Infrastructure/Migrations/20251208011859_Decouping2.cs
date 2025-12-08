using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TorreClou.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Decouping2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_TorrentFiles_TorrentFileId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_TorrentFiles_Users_UploadedByUserId",
                table: "TorrentFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserJobs_TorrentFiles_TorrentFileId",
                table: "UserJobs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TorrentFiles",
                table: "TorrentFiles");

            migrationBuilder.RenameTable(
                name: "TorrentFiles",
                newName: "RequestedFiles");

            migrationBuilder.RenameIndex(
                name: "IX_TorrentFiles_UploadedByUserId",
                table: "RequestedFiles",
                newName: "IX_RequestedFiles_UploadedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_TorrentFiles_InfoHash",
                table: "RequestedFiles",
                newName: "IX_RequestedFiles_InfoHash");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RequestedFiles",
                table: "RequestedFiles",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_RequestedFiles_TorrentFileId",
                table: "Invoices",
                column: "TorrentFileId",
                principalTable: "RequestedFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestedFiles_Users_UploadedByUserId",
                table: "RequestedFiles",
                column: "UploadedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserJobs_RequestedFiles_TorrentFileId",
                table: "UserJobs",
                column: "TorrentFileId",
                principalTable: "RequestedFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_RequestedFiles_TorrentFileId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestedFiles_Users_UploadedByUserId",
                table: "RequestedFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserJobs_RequestedFiles_TorrentFileId",
                table: "UserJobs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RequestedFiles",
                table: "RequestedFiles");

            migrationBuilder.RenameTable(
                name: "RequestedFiles",
                newName: "TorrentFiles");

            migrationBuilder.RenameIndex(
                name: "IX_RequestedFiles_UploadedByUserId",
                table: "TorrentFiles",
                newName: "IX_TorrentFiles_UploadedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_RequestedFiles_InfoHash",
                table: "TorrentFiles",
                newName: "IX_TorrentFiles_InfoHash");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TorrentFiles",
                table: "TorrentFiles",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_TorrentFiles_TorrentFileId",
                table: "Invoices",
                column: "TorrentFileId",
                principalTable: "TorrentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TorrentFiles_Users_UploadedByUserId",
                table: "TorrentFiles",
                column: "UploadedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserJobs_TorrentFiles_TorrentFileId",
                table: "UserJobs",
                column: "TorrentFileId",
                principalTable: "TorrentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
