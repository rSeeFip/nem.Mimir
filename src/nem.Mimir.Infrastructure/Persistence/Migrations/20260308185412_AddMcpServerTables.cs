using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace nem.Mimir.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpServerTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mcp_server_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    transport_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    command = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    arguments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    environment_variables_json = table.Column<string>(type: "jsonb", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_bundled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_server_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mcp_tool_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mcp_server_config_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tool_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    input = table.Column<string>(type: "text", nullable: true),
                    output = table.Column<string>(type: "text", nullable: true),
                    latency_ms = table.Column<long>(type: "bigint", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_tool_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_mcp_tool_audit_logs_mcp_server_configs_mcp_server_config_id",
                        column: x => x.mcp_server_config_id,
                        principalTable: "mcp_server_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "mcp_tool_whitelists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mcp_server_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tool_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_tool_whitelists", x => x.id);
                    table.ForeignKey(
                        name: "FK_mcp_tool_whitelists_mcp_server_configs_mcp_server_config_id",
                        column: x => x.mcp_server_config_id,
                        principalTable: "mcp_server_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "mcp_server_configs",
                columns: new[] { "id", "arguments", "command", "created_at", "description", "environment_variables_json", "is_bundled", "is_enabled", "name", "transport_type", "updated_at", "url" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-0001-0000-0000-000000000001"), "-y @anthropic/brave-search-mcp", "npx", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Web search powered by Brave Search API", null, true, false, "Brave Web Search", "Stdio", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("a1b2c3d4-0002-0000-0000-000000000002"), "-y @anthropic/filesystem-mcp /tmp", "npx", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Local filesystem access via MCP", null, true, false, "Filesystem", "Stdio", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("a1b2c3d4-0003-0000-0000-000000000003"), "-y @anthropic/database-mcp", "npx", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Database query and management via MCP", null, true, false, "Database", "Stdio", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("a1b2c3d4-0004-0000-0000-000000000004"), "-y @anthropic/code-execution-mcp", "npx", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sandboxed code execution via MCP", null, true, false, "Code Execution", "Stdio", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_mcp_server_configs_is_enabled",
                table: "mcp_server_configs",
                column: "is_enabled");

            migrationBuilder.CreateIndex(
                name: "ix_mcp_server_configs_name",
                table: "mcp_server_configs",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mcp_tool_audit_logs_mcp_server_config_id",
                table: "mcp_tool_audit_logs",
                column: "mcp_server_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_mcp_tool_audit_logs_timestamp",
                table: "mcp_tool_audit_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_mcp_tool_audit_logs_tool_name",
                table: "mcp_tool_audit_logs",
                column: "tool_name");

            migrationBuilder.CreateIndex(
                name: "ix_mcp_tool_whitelists_config_id_tool_name",
                table: "mcp_tool_whitelists",
                columns: new[] { "mcp_server_config_id", "tool_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mcp_tool_audit_logs");

            migrationBuilder.DropTable(
                name: "mcp_tool_whitelists");

            migrationBuilder.DropTable(
                name: "mcp_server_configs");
        }
    }
}
