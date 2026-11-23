using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaGaragem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CascadeComponentDependents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecordComponent_Components_VehicleComponentId_Ve~",
                table: "MaintenanceRecordComponent"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_Components_VehicleComponentId_VehicleId",
                table: "Schedules"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecordComponent_Components_VehicleComponentId_Ve~",
                table: "MaintenanceRecordComponent",
                columns: new[] { "VehicleComponentId", "VehicleId" },
                principalTable: "Components",
                principalColumns: new[] { "Id", "VehicleId" },
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_Components_VehicleComponentId_VehicleId",
                table: "Schedules",
                columns: new[] { "VehicleComponentId", "VehicleId" },
                principalTable: "Components",
                principalColumns: new[] { "Id", "VehicleId" },
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecordComponent_Components_VehicleComponentId_Ve~",
                table: "MaintenanceRecordComponent"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_Components_VehicleComponentId_VehicleId",
                table: "Schedules"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecordComponent_Components_VehicleComponentId_Ve~",
                table: "MaintenanceRecordComponent",
                columns: new[] { "VehicleComponentId", "VehicleId" },
                principalTable: "Components",
                principalColumns: new[] { "Id", "VehicleId" }
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_Components_VehicleComponentId_VehicleId",
                table: "Schedules",
                columns: new[] { "VehicleComponentId", "VehicleId" },
                principalTable: "Components",
                principalColumns: new[] { "Id", "VehicleId" }
            );
        }
    }
}
