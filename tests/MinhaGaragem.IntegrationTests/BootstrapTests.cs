using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace MinhaGaragem.IntegrationTests;

public sealed class BootstrapTests
{
    private static WebApplicationFactory<Program> Host(
        string connection,
        string email,
        string password
    ) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseEnvironment("Testing")
                .UseSetting("ConnectionStrings:Garage", connection)
                .UseSetting("Database:MigrateOnStartup", "true")
                .UseSetting("Bootstrap:Enabled", "true")
                .UseSetting("Bootstrap:Email", email)
                .UseSetting("Bootstrap:Password", password)
        );

    private static async Task Csrf(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var response = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", response.GetProperty("token").GetString());
    }

    private static async Task Database(Func<string, Task> verify)
    {
        var admin = Environment.GetEnvironmentVariable("GARAGE_TEST_CONNECTION")
            ?? throw new InvalidOperationException("GARAGE_TEST_CONNECTION obrigatório.");
        var name = "garage_bootstrap_" + Guid.NewGuid().ToString("N");
        await using var server = new NpgsqlConnection(admin);
        await server.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", server))
            await create.ExecuteNonQueryAsync();
        try
        {
            await verify(
                new NpgsqlConnectionStringBuilder(admin)
                {
                    Database = name,
                    Pooling = false,
                }.ConnectionString
            );
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", server);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task FirstStartupCreatesAccessAndRestartDoesNotResetPasswordOrCreateMoreAccounts() =>
        await Database(async connection =>
        {
            Guid vehicleId;
            await using (var host = Host(connection, "owner@example.test", "GarageFirst1234"))
            {
                using var client = host.CreateClient(new() { HandleCookies = true });
                await Csrf(client);
                Assert.Equal(
                    HttpStatusCode.NotFound,
                    (
                        await client.PostAsJsonAsync(
                            "/api/auth/register",
                            new { email = "visitor@example.test", password = "GarageVisitor1234" }
                        )
                    ).StatusCode
                );
                (
                    await client.PostAsJsonAsync(
                        "/api/auth/login",
                        new { email = "owner@example.test", password = "GarageFirst1234" }
                    )
                ).EnsureSuccessStatusCode();
                await Csrf(client);
                var vehicles = await client.GetFromJsonAsync<JsonElement>("/api/vehicles");
                var vehicle = Assert.Single(vehicles.GetProperty("items").EnumerateArray());
                vehicleId = vehicle.GetProperty("id").GetGuid();
                Assert.Equal("Saveiro", vehicle.GetProperty("model").GetString());
                Assert.Equal("Prata", vehicle.GetProperty("color").GetString());
                (
                    await client.PostAsJsonAsync(
                        $"/api/vehicles/{vehicleId}/mileage",
                        new { mileage = 100 }
                    )
                ).EnsureSuccessStatusCode();
            }
            await using (
                var restarted = Host(connection, "owner@example.test", "DifferentPassword1234")
            )
            {
                using var client = restarted.CreateClient(new() { HandleCookies = true });
                await Csrf(client);
                Assert.Equal(
                    HttpStatusCode.Unauthorized,
                    (
                        await client.PostAsJsonAsync(
                            "/api/auth/login",
                            new { email = "owner@example.test", password = "DifferentPassword1234" }
                        )
                    ).StatusCode
                );
                (
                    await client.PostAsJsonAsync(
                        "/api/auth/login",
                        new { email = "owner@example.test", password = "GarageFirst1234" }
                    )
                ).EnsureSuccessStatusCode();
                var vehicles = await client.GetFromJsonAsync<JsonElement>("/api/vehicles");
                var vehicle = Assert.Single(vehicles.GetProperty("items").EnumerateArray());
                Assert.Equal(vehicleId, vehicle.GetProperty("id").GetGuid());
                Assert.Equal(100, vehicle.GetProperty("mileage").GetInt32());
            }
            await using var database = new NpgsqlConnection(connection);
            await database.OpenAsync();
            Assert.Equal(
                1L,
                await new NpgsqlCommand(
                    "SELECT count(*) FROM \"AspNetUsers\"",
                    database
                ).ExecuteScalarAsync()
            );
        });

    [Theory]
    [InlineData("", "")]
    [InlineData("invalid", "GarageFirst1234")]
    [InlineData("owner@example.test", "weak")]
    public async Task MissingOrInvalidCredentialsDoNotCreateAnAccount(
        string email,
        string password
    ) =>
        await Database(async connection =>
        {
            await using var host = Host(connection, email, password);
            Assert.ThrowsAny<Exception>(() => host.CreateClient());
            await using var database = new NpgsqlConnection(connection);
            await database.OpenAsync();
            Assert.Equal(
                0L,
                await new NpgsqlCommand(
                    "SELECT count(*) FROM \"AspNetUsers\"",
                    database
                ).ExecuteScalarAsync()
            );
            Assert.Equal(
                0L,
                await new NpgsqlCommand(
                    "SELECT count(*) FROM \"Vehicles\"",
                    database
                ).ExecuteScalarAsync()
            );
        });
}
