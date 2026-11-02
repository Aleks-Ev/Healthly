using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HealthlyApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAnalysesAndCriticalResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCritical",
                table: "TestResults",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DocumentAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicalDocumentId = table.Column<int>(type: "integer", nullable: false),
                    Indicators = table.Column<string>(type: "jsonb", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Recommendations = table.Column<List<string>>(type: "text[]", nullable: false),
                    AnonymizedText = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsMock = table.Column<bool>(type: "boolean", nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentAnalyses_MedicalDocuments_MedicalDocumentId",
                        column: x => x.MedicalDocumentId,
                        principalTable: "MedicalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAnalyses_MedicalDocumentId",
                table: "DocumentAnalyses",
                column: "MedicalDocumentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentAnalyses");

            migrationBuilder.DropColumn(
                name: "IsCritical",
                table: "TestResults");
        }
    }
}
