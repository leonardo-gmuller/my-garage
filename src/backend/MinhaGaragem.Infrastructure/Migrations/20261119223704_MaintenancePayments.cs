using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaGaragem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MaintenancePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Installments = table.Column<int>(type: "integer", nullable: true),
                    TotalCharged = table.Column<decimal>(
                        type: "numeric(16,2)",
                        precision: 16,
                        scale: 2,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint(
                        "CK_Payment_Values",
                        "\"Scope\" BETWEEN 0 AND 2 AND \"Method\" BETWEEN 0 AND 4 AND (\"Installments\" IS NULL OR \"Installments\" BETWEEN 1 AND 120) AND (\"TotalCharged\" IS NULL OR \"TotalCharged\" BETWEEN 0 AND 100000000)"
                    );
                    table.ForeignKey(
                        name: "FK_Payments_Records_MaintenanceRecordId",
                        column: x => x.MaintenanceRecordId,
                        principalTable: "Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MaintenanceRecordId_Scope",
                table: "Payments",
                columns: new[] { "MaintenanceRecordId", "Scope" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Payments");
        }
    }
}
