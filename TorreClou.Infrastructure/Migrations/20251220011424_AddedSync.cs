using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TorreClou.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "UserJobs");

            migrationBuilder.DropColumn(
                name: "LeaseOwnerId",
                table: "UserJobs");

            migrationBuilder.CreateTable(
                name: "Syncs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LocalFilePath = table.Column<string>(type: "text", nullable: true),
                    S3KeyPrefix = table.Column<string>(type: "text", nullable: true),
                    TotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    BytesSynced = table.Column<long>(type: "bigint", nullable: false),
                    FilesTotal = table.Column<int>(type: "integer", nullable: false),
                    FilesSynced = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Syncs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Syncs_UserJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "UserJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "S3SyncProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<int>(type: "integer", nullable: false),
                    SyncId = table.Column<int>(type: "integer", nullable: false),
                    LocalFilePath = table.Column<string>(type: "text", nullable: false),
                    S3Key = table.Column<string>(type: "text", nullable: false),
                    UploadId = table.Column<string>(type: "text", nullable: true),
                    PartSize = table.Column<long>(type: "bigint", nullable: false),
                    TotalParts = table.Column<int>(type: "integer", nullable: false),
                    PartsCompleted = table.Column<int>(type: "integer", nullable: false),
                    BytesUploaded = table.Column<long>(type: "bigint", nullable: false),
                    TotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    PartETags = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LastPartNumber = table.Column<int>(type: "integer", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_S3SyncProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_S3SyncProgresses_Syncs_SyncId",
                        column: x => x.SyncId,
                        principalTable: "Syncs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_S3SyncProgresses_UserJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "UserJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_S3SyncProgresses_JobId",
                table: "S3SyncProgresses",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_S3SyncProgresses_SyncId",
                table: "S3SyncProgresses",
                column: "SyncId");

            migrationBuilder.CreateIndex(
                name: "IX_Syncs_JobId",
                table: "Syncs",
                column: "JobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "S3SyncProgresses");

            migrationBuilder.DropTable(
                name: "Syncs");

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAt",
                table: "UserJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwnerId",
                table: "UserJobs",
                type: "text",
                nullable: true);
        }
    }
}
