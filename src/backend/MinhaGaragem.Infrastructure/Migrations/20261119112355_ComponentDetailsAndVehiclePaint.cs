using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaGaragem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComponentDetailsAndVehiclePaint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorHex",
                table: "Vehicles",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "PaintFinish",
                table: "Vehicles",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleSubcomponentId",
                table: "Schedules",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryComponentId",
                table: "Records",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MaintenanceRecordComponent_MaintenanceRecordId_VehicleCompo~",
                table: "MaintenanceRecordComponent",
                columns: new[] { "MaintenanceRecordId", "VehicleComponentId", "VehicleId" }
            );

            migrationBuilder.CreateTable(
                name: "Subcomponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(
                        type: "character varying(40)",
                        maxLength: 40,
                        nullable: false
                    ),
                    Name = table.Column<string>(
                        type: "character varying(80)",
                        maxLength: 80,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subcomponents", x => x.Id);
                    table.UniqueConstraint(
                        "AK_Subcomponents_Id_VehicleComponentId_VehicleId",
                        x => new
                        {
                            x.Id,
                            x.VehicleComponentId,
                            x.VehicleId,
                        }
                    );
                    table.ForeignKey(
                        name: "FK_Subcomponents_Components_VehicleComponentId_VehicleId",
                        columns: x => new { x.VehicleComponentId, x.VehicleId },
                        principalTable: "Components",
                        principalColumns: new[] { "Id", "VehicleId" },
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "RecordSubcomponents",
                columns: table => new
                {
                    MaintenanceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleSubcomponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordSubcomponents", x => new
                    {
                        x.MaintenanceRecordId,
                        x.VehicleSubcomponentId,
                    });
                    table.ForeignKey(
                        name: "FK_RecordSubcomponents_MaintenanceRecordComponent_MaintenanceR~",
                        columns: x => new
                        {
                            x.MaintenanceRecordId,
                            x.VehicleComponentId,
                            x.VehicleId,
                        },
                        principalTable: "MaintenanceRecordComponent",
                        principalColumns: new[]
                        {
                            "MaintenanceRecordId",
                            "VehicleComponentId",
                            "VehicleId",
                        },
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_RecordSubcomponents_Records_MaintenanceRecordId_VehicleId",
                        columns: x => new { x.MaintenanceRecordId, x.VehicleId },
                        principalTable: "Records",
                        principalColumns: new[] { "Id", "VehicleId" },
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_RecordSubcomponents_Subcomponents_VehicleSubcomponentId_Veh~",
                        columns: x => new
                        {
                            x.VehicleSubcomponentId,
                            x.VehicleComponentId,
                            x.VehicleId,
                        },
                        principalTable: "Subcomponents",
                        principalColumns: new[] { "Id", "VehicleComponentId", "VehicleId" },
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicle_ColorHex",
                table: "Vehicles",
                sql: "\"ColorHex\" IS NULL OR \"ColorHex\" ~ '^#[0-9a-fA-F]{6}$'"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_VehicleSubcomponentId_VehicleComponentId_VehicleId",
                table: "Schedules",
                columns: new[] { "VehicleSubcomponentId", "VehicleComponentId", "VehicleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Records_PrimaryComponentId_VehicleId",
                table: "Records",
                columns: new[] { "PrimaryComponentId", "VehicleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_RecordSubcomponents_MaintenanceRecordId_VehicleComponentId_~",
                table: "RecordSubcomponents",
                columns: new[] { "MaintenanceRecordId", "VehicleComponentId", "VehicleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_RecordSubcomponents_MaintenanceRecordId_VehicleId",
                table: "RecordSubcomponents",
                columns: new[] { "MaintenanceRecordId", "VehicleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_RecordSubcomponents_VehicleSubcomponentId_VehicleComponentI~",
                table: "RecordSubcomponents",
                columns: new[] { "VehicleSubcomponentId", "VehicleComponentId", "VehicleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Subcomponents_VehicleComponentId_Key",
                table: "Subcomponents",
                columns: new[] { "VehicleComponentId", "Key" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Subcomponents_VehicleComponentId_VehicleId",
                table: "Subcomponents",
                columns: new[] { "VehicleComponentId", "VehicleId" }
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Records_Components_PrimaryComponentId_VehicleId",
                table: "Records",
                columns: new[] { "PrimaryComponentId", "VehicleId" },
                principalTable: "Components",
                principalColumns: new[] { "Id", "VehicleId" }
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_Subcomponents_VehicleSubcomponentId_VehicleCompon~",
                table: "Schedules",
                columns: new[] { "VehicleSubcomponentId", "VehicleComponentId", "VehicleId" },
                principalTable: "Subcomponents",
                principalColumns: new[] { "Id", "VehicleComponentId", "VehicleId" },
                onDelete: ReferentialAction.Cascade
            );

            // Backfill choices for existing vehicles without fabricating service records.
            migrationBuilder.Sql(
                """
                INSERT INTO "Subcomponents" ("Id", "VehicleId", "VehicleComponentId", "Key", "Name")
                SELECT gen_random_uuid(), c."VehicleId", c."Id", catalog.key, catalog.name
                FROM "Components" c
                JOIN (VALUES
                ('suspension', 'shock-absorbers', 'Amortecedores'),
                ('suspension', 'springs', 'Molas'),
                ('suspension', 'bump-stops', 'Batentes'),
                ('suspension', 'control-arms', 'Bandejas'),
                ('suspension', 'ball-joints', 'Pivôs'),
                ('suspension', 'bushings', 'Buchas'),
                ('engine', 'spark-plugs', 'Velas'),
                ('engine', 'ignition-cables', 'Cabos de vela'),
                ('engine', 'battery', 'Bateria'),
                ('engine', 'filters', 'Filtros'),
                ('engine', 'belts', 'Correias'),
                ('engine', 'cooling-system', 'Sistema de arrefecimento'),
                ('battery', 'terminals', 'Terminais'),
                ('battery', 'battery-unit', 'Bateria completa'),
                ('wheels', 'tires', 'Pneus'),
                ('wheels', 'rims', 'Rodas'),
                ('brakes', 'pads', 'Pastilhas'),
                ('brakes', 'discs', 'Discos'),
                ('brakes', 'fluid', 'Fluido de freio'),
                ('lights', 'bulbs', 'Lâmpadas'),
                ('lights', 'headlights', 'Conjunto dos faróis'),
                ('cooling', 'radiator', 'Radiador'),
                ('cooling', 'hoses', 'Mangueiras'),
                ('transmission', 'clutch', 'Embreagem'),
                ('transmission', 'gearbox', 'Câmbio'),
                ('exhaust', 'muffler', 'Silencioso'),
                ('exhaust', 'catalyst', 'Catalisador'),
                ('body', 'doors', 'Portas'),
                ('body', 'bonnet', 'Capô'),
                ('body', 'panels', 'Painéis externos')
                ) AS catalog(component_key, key, name) ON catalog.component_key = c."Key";

                UPDATE "Records" r SET "PrimaryComponentId" = (
                    SELECT link."VehicleComponentId" FROM "MaintenanceRecordComponent" link
                    WHERE link."MaintenanceRecordId" = r."Id"
                    ORDER BY link."VehicleComponentId" LIMIT 1
                );
                UPDATE "Vehicles" SET "Color" = 'Prata', "ColorHex" = '#C0C0C0', "PaintFinish" = 1
                WHERE lower("Manufacturer") = 'volkswagen' AND lower("Model") = 'saveiro' AND "ModelYear" = 2013
                AND "ColorHex" IS NULL AND ("Color" IS NULL OR trim("Color") = '' OR lower(trim("Color")) = 'prata');
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Records_Components_PrimaryComponentId_VehicleId",
                table: "Records"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_Subcomponents_VehicleSubcomponentId_VehicleCompon~",
                table: "Schedules"
            );

            migrationBuilder.DropTable(name: "RecordSubcomponents");

            migrationBuilder.DropTable(name: "Subcomponents");

            migrationBuilder.DropCheckConstraint(name: "CK_Vehicle_ColorHex", table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_VehicleSubcomponentId_VehicleComponentId_VehicleId",
                table: "Schedules"
            );

            migrationBuilder.DropIndex(
                name: "IX_Records_PrimaryComponentId_VehicleId",
                table: "Records"
            );

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MaintenanceRecordComponent_MaintenanceRecordId_VehicleCompo~",
                table: "MaintenanceRecordComponent"
            );

            migrationBuilder.DropColumn(name: "ColorHex", table: "Vehicles");

            migrationBuilder.DropColumn(name: "PaintFinish", table: "Vehicles");

            migrationBuilder.DropColumn(name: "VehicleSubcomponentId", table: "Schedules");

            migrationBuilder.DropColumn(name: "PrimaryComponentId", table: "Records");
        }
    }
}
