using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MinhaGaragem.Application;

namespace MinhaGaragem.Infrastructure;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<GarageDbContext>
{
    public GarageDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Garage")
            ?? "Host=localhost;Database=minha_garagem;Username=garage";
        return new(
            new DbContextOptionsBuilder<GarageDbContext>().UseNpgsql(connection).Options,
            new MigrationUser()
        );
    }

    private sealed class MigrationUser : ICurrentUser
    {
        public Guid Id => Guid.Empty;
    }
}
