using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HealthlyApi.Migrations
{
    /// <inheritdoc />
    public partial class ReferenceValuesFromCsv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReferenceMedicalValues_MetricName",
                table: "ReferenceMedicalValues");

            migrationBuilder.DeleteData(
                table: "ReferenceMedicalValues",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ReferenceMedicalValues",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ReferenceMedicalValues",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "ReferenceMedicalValues",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "AgeFrom",
                table: "ReferenceMedicalValues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AgeTo",
                table: "ReferenceMedicalValues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aliases",
                table: "ReferenceMedicalValues",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CriticalHigh",
                table: "ReferenceMedicalValues",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CriticalLow",
                table: "ReferenceMedicalValues",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReviewedAt",
                table: "ReferenceMedicalValues",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                table: "ReferenceMedicalValues",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Sex",
                table: "ReferenceMedicalValues",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ReferenceMedicalValues",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceMedicalValues_MetricName",
                table: "ReferenceMedicalValues",
                column: "MetricName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReferenceMedicalValues_MetricName",
                table: "ReferenceMedicalValues");

            migrationBuilder.DropColumn(
                name: "AgeFrom",
                table: "ReferenceMedicalValues");

            migrationBuilder.DropColumn(
                name: "AgeTo",
                table: "ReferenceMedicalValues");

            migrationBuilder.DropColumn(
                name: "Aliases",
                table: "ReferenceMedicalValues");

            migrationBuilder.DropColumn(
                name: "CriticalHigh",
                table: "ReferenceMedicalValues");

            migrationBuilder.DropColumn(
                name: "CriticalLow",
                table: "ReferenceMedicalValues");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "ReferenceMedicalValues");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "ReferenceMedicalValues");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "ReferenceMedicalValues");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ReferenceMedicalValues");

            migrationBuilder.InsertData(
                table: "ReferenceMedicalValues",
                columns: new[] { "Id", "Category", "DoctorSpecialty", "MaxNormal", "MetricName", "MinNormal", "Unit" },
                values: new object[,]
                {
                    { 1, "Biochemistry", "Endocrinologist", 5.6m, "Glucose", 3.9m, "mmol/L" },
                    { 2, "Lipid profile", "Cardiologist", 5.2m, "Cholesterol", null, "mmol/L" },
                    { 3, "Complete blood count", "Hematologist", 160m, "Hemoglobin", 120m, "g/L" },
                    { 4, "Complete blood count", "Hematologist", 9.0m, "Leukocytes", 4.0m, "10^9/L" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceMedicalValues_MetricName",
                table: "ReferenceMedicalValues",
                column: "MetricName",
                unique: true);
        }
    }
}
