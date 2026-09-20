using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiAiAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatAudit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssistantAnswer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedTag = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ToolsUsedJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TagCatalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PiWebId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PiPointName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AfAttributePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CanonicalName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionOverride = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EquipmentId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerTeam = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Criticality = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpectedMin = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExpectedMax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSearchable = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TagDocumentation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanonicalName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagDocumentation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TagRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromCanonicalName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ToCanonicalName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagRelationships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TagAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TagAliases_TagCatalog_TagCatalogId",
                        column: x => x.TagCatalogId,
                        principalTable: "TagCatalog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TagAliases_Alias",
                table: "TagAliases",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TagAliases_TagCatalogId",
                table: "TagAliases",
                column: "TagCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_TagCatalog_CanonicalName",
                table: "TagCatalog",
                column: "CanonicalName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatAudit");

            migrationBuilder.DropTable(
                name: "TagAliases");

            migrationBuilder.DropTable(
                name: "TagDocumentation");

            migrationBuilder.DropTable(
                name: "TagRelationships");

            migrationBuilder.DropTable(
                name: "TagCatalog");
        }
    }
}
