using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HealthlyApi.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceMedicalValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferenceMedicalValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MetricName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinNormal = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    MaxNormal = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DoctorSpecialty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceMedicalValues", x => x.Id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferenceMedicalValues");
        }
    }
}
