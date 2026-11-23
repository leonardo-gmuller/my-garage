using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MinhaGaragem.Application;
using MinhaGaragem.Infrastructure;
using Npgsql;

namespace MinhaGaragem.IntegrationTests;

public sealed class MigrationUpgradeTests
{
    private sealed class MigrationUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
    }

    [Fact]
    public async Task UpgradePreservesServicesBackfillsDetailsAndDoesNotReplaceKnownOtherColor()
    {
        var admin = Environment.GetEnvironmentVariable("GARAGE_TEST_CONNECTION")
            ?? throw new InvalidOperationException("GARAGE_TEST_CONNECTION obrigatório.");
        var name = "garage_upgrade_" + Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var otherVehicleId = Guid.NewGuid();
        var componentId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        await using var server = new NpgsqlConnection(admin);
        await server.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", server))
            await create.ExecuteNonQueryAsync();
        try
        {
            var cs = new NpgsqlConnectionStringBuilder(admin)
            {
                Database = name,
                Pooling = false,
            }.ConnectionString;
            await using var db = new GarageDbContext(
                new DbContextOptionsBuilder<GarageDbContext>().UseNpgsql(cs).Options,
                new MigrationUser(userId)
            );
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261118233021_CascadeComponentDependents");
            await using var connection = new NpgsqlConnection(cs);
            await connection.OpenAsync();
            await using (
                var seed = new NpgsqlCommand(
                    """
                    WITH inserted_user AS (INSERT INTO "AspNetUsers" ("Id", "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount") VALUES ($1, false, false, false, false, 0)),
                    inserted_vehicles AS (INSERT INTO "Vehicles" ("Id", "UserId", "Manufacturer", "Model", "ManufactureYear", "ModelYear", "CreatedAt", "ConcurrencyStamp", "Color")
                    VALUES ($2, $1, 'Volkswagen', 'Saveiro', 2013, 2013, now(), $2, NULL),
                           ($3, $1, 'Volkswagen', 'Saveiro', 2013, 2013, now(), $3, 'Vermelho')),
                    inserted_component AS (INSERT INTO "Components" ("Id", "VehicleId", "Key", "Name") VALUES ($4, $2, 'suspension', 'Suspensão')),
                    inserted_record AS (INSERT INTO "Records" ("Id", "VehicleId", "Date", "Category", "ServiceType", "Description", "LaborCost", "CreatedAt", "Workshop")
                    VALUES ($5, $2, '2026-01-01', 4, 3, 'Serviço anterior à atualização', 125, now(), 'Oficina anterior'))
                    INSERT INTO "MaintenanceRecordComponent" ("MaintenanceRecordId", "VehicleComponentId", "VehicleId") VALUES ($5, $4, $2);
                    """,
                    connection
                )
            )
            {
                foreach (
                    var value in new[] { userId, vehicleId, otherVehicleId, componentId, recordId }
                )
                    seed.Parameters.AddWithValue(value);
                await seed.ExecuteNonQueryAsync();
            }
            await migrator.MigrateAsync();
            var vehicle = await db.Vehicles.SingleAsync(v => v.Id == vehicleId);
            Assert.Equal("Prata", vehicle.Color);
            Assert.Equal("#C0C0C0", vehicle.ColorHex);
            var other = await db.Vehicles.SingleAsync(v => v.Id == otherVehicleId);
            Assert.Equal("Vermelho", other.Color);
            Assert.Null(other.ColorHex);
            Assert.Equal(
                6,
                await db.Subcomponents.CountAsync(s => s.VehicleComponentId == componentId)
            );
            var record = await db.Records.SingleAsync();
            Assert.Equal(recordId, record.Id);
            Assert.Equal(125, record.LaborCost);
            Assert.Equal(componentId, record.PrimaryComponentId);
            Assert.Equal("Oficina anterior", record.Workshop);
            Assert.Null(record.MechanicId);
            Assert.Null(record.MechanicName);
            Assert.Null(record.PackageTotal);
            Assert.Equal(125, record.TotalCost);
            Assert.Empty(await db.Mechanics.ToArrayAsync());
            Assert.Empty(await db.Payments.ToArrayAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", server);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
