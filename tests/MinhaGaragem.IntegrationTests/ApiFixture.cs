using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinhaGaragem.Infrastructure;
using Npgsql;

namespace MinhaGaragem.IntegrationTests;

public sealed class ApiFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    private string admin = "",
        database = "",
        storage = "";

    public async Task InitializeAsync()
    {
        admin = Environment.GetEnvironmentVariable("GARAGE_TEST_CONNECTION")
            ?? throw new InvalidOperationException(
                "Defina GARAGE_TEST_CONNECTION para um PostgreSQL de testes com permissão CREATEDB. Nenhum banco existente será apagado."
            );
        database = "garage_test_" + Guid.NewGuid().ToString("N");
        storage = Path.Combine(Path.GetTempPath(), database);
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
            await cmd.ExecuteNonQueryAsync();
        }
        var cs = new NpgsqlConnectionStringBuilder(admin) { Database = database }.ConnectionString;
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseEnvironment("Testing")
                .UseSetting("ConnectionStrings:Garage", cs)
                .UseSetting("Auth:PermitLimit", "1000")
                .UseSetting("AI_ENABLED", "false")
                .UseSetting("Storage:Path", storage)
                .UseSetting("Bootstrap:Enabled", "false")
                .UseSetting("Database:MigrateOnStartup", "false")
        );
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<GarageDbContext>().Database.MigrateAsync();
    }

    public async Task<string> CreateAccount(string password)
    {
        using var scope = Factory.Services.CreateScope();
        var email = Guid.NewGuid() + "@example.test";
        await scope
            .ServiceProvider.GetRequiredService<AccountProvisioner>()
            .Create(email, password);
        return email;
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        if (!string.IsNullOrEmpty(database))
        {
            await using var c = new NpgsqlConnection(admin);
            await c.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS {database} WITH (FORCE)",
                c
            );
            await cmd.ExecuteNonQueryAsync();
        }
        if (Directory.Exists(storage))
            Directory.Delete(storage, true);
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;
