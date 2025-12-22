using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TorreClou.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusHistoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobStatusHistories",
                schema: "dev",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<string>(type: "text", nullable: true),
                    ToStatus = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobStatusHistories_UserJobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "dev",
                        principalTable: "UserJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SyncStatusHistories",
                schema: "dev",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SyncId = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<string>(type: "text", nullable: true),
                    ToStatus = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncStatusHistories_Syncs_SyncId",
                        column: x => x.SyncId,
                        principalSchema: "dev",
                        principalTable: "Syncs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistories_JobId_ChangedAt",
                schema: "dev",
                table: "JobStatusHistories",
                columns: new[] { "JobId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncStatusHistories_SyncId_ChangedAt",
                schema: "dev",
                table: "SyncStatusHistories",
                columns: new[] { "SyncId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobStatusHistories",
                schema: "dev");

            migrationBuilder.DropTable(
                name: "SyncStatusHistories",
                schema: "dev");
        }
    }
}
