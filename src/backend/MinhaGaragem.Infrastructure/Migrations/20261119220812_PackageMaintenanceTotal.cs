using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaGaragem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PackageMaintenanceTotal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PackageTotal",
                table: "Records",
                type: "numeric(16,2)",
                precision: 16,
                scale: 2,
                nullable: true
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_Record_PackageTotal",
                table: "Records",
                sql: "\"PackageTotal\" IS NULL OR (\"PackageTotal\" >= 0 AND \"PackageTotal\" <= 100000000 AND \"LaborCost\" = 0)"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "CK_Record_PackageTotal", table: "Records");

            migrationBuilder.DropColumn(name: "PackageTotal", table: "Records");
        }
    }
}
