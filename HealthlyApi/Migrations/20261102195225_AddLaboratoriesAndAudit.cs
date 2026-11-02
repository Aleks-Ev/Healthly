using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HealthlyApi.Migrations
{
    /// <inheritdoc />
    public partial class AddLaboratoriesAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LaboratoryId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientCode",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LaboratoryId",
                table: "MedicalDocuments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UploadedByUserId",
                table: "MedicalDocuments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LaboratoryId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: true),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    Details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Laboratories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Laboratories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_LaboratoryId",
                table: "Users",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PatientCode",
                table: "Users",
                column: "PatientCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicalDocuments_LaboratoryId",
                table: "MedicalDocuments",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_DocumentId",
                table: "AuditEvents",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_PatientId",
                table: "AuditEvents",
                column: "PatientId");

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalDocuments_Laboratories_LaboratoryId",
                table: "MedicalDocuments",
                column: "LaboratoryId",
                principalTable: "Laboratories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Laboratories_LaboratoryId",
                table: "Users",
                column: "LaboratoryId",
                principalTable: "Laboratories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MedicalDocuments_Laboratories_LaboratoryId",
                table: "MedicalDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Laboratories_LaboratoryId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "Laboratories");

            migrationBuilder.DropIndex(
                name: "IX_Users_LaboratoryId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PatientCode",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_MedicalDocuments_LaboratoryId",
                table: "MedicalDocuments");

            migrationBuilder.DropColumn(
                name: "LaboratoryId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PatientCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LaboratoryId",
                table: "MedicalDocuments");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "MedicalDocuments");
        }
    }
}
