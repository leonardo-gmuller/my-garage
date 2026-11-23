using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhaGaragem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MechanicsDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MechanicId",
                table: "Records",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "MechanicName",
                table: "Records",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "Mechanics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(
                        type: "character varying(120)",
                        maxLength: 120,
                        nullable: false
                    ),
                    Workshop = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: true
                    ),
                    Specialty = table.Column<string>(
                        type: "character varying(120)",
                        maxLength: 120,
                        nullable: true
                    ),
                    Phone = table.Column<string>(
                        type: "character varying(40)",
                        maxLength: 40,
                        nullable: true
                    ),
                    Email = table.Column<string>(
                        type: "character varying(254)",
                        maxLength: 254,
                        nullable: true
                    ),
                    Address = table.Column<string>(
                        type: "character varying(300)",
                        maxLength: 300,
                        nullable: true
                    ),
                    Notes = table.Column<string>(
                        type: "character varying(4000)",
                        maxLength: 4000,
                        nullable: true
                    ),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mechanics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mechanics_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Records_MechanicId",
                table: "Records",
                column: "MechanicId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Mechanics_UserId_IsArchived_Name",
                table: "Mechanics",
                columns: new[] { "UserId", "IsArchived", "Name" }
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Records_Mechanics_MechanicId",
                table: "Records",
                column: "MechanicId",
                principalTable: "Mechanics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Records_Mechanics_MechanicId",
                table: "Records"
            );

            migrationBuilder.DropTable(name: "Mechanics");

            migrationBuilder.DropIndex(name: "IX_Records_MechanicId", table: "Records");

            migrationBuilder.DropColumn(name: "MechanicId", table: "Records");

            migrationBuilder.DropColumn(name: "MechanicName", table: "Records");
        }
    }
}
