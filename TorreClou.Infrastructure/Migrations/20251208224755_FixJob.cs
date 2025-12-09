using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TorreClou.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserJobs_RequestedFiles_TorrentFileId",
                table: "UserJobs");

            migrationBuilder.RenameColumn(
                name: "TorrentFileId",
                table: "UserJobs",
                newName: "RequestFileId");

            migrationBuilder.RenameColumn(
                name: "RemoteFileId",
                table: "UserJobs",
                newName: "CurrentState");

            migrationBuilder.RenameIndex(
                name: "IX_UserJobs_TorrentFileId",
                table: "UserJobs",
                newName: "IX_UserJobs_RequestFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserJobs_RequestedFiles_RequestFileId",
                table: "UserJobs",
                column: "RequestFileId",
                principalTable: "RequestedFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserJobs_RequestedFiles_RequestFileId",
                table: "UserJobs");

            migrationBuilder.RenameColumn(
                name: "RequestFileId",
                table: "UserJobs",
                newName: "TorrentFileId");

            migrationBuilder.RenameColumn(
                name: "CurrentState",
                table: "UserJobs",
                newName: "RemoteFileId");

            migrationBuilder.RenameIndex(
                name: "IX_UserJobs_RequestFileId",
                table: "UserJobs",
                newName: "IX_UserJobs_TorrentFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserJobs_RequestedFiles_TorrentFileId",
                table: "UserJobs",
                column: "TorrentFileId",
                principalTable: "RequestedFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
