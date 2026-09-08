using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FirmaOperaCloud.Infrastructure.Persistence;

/// <summary>Permite generar scripts de migración sin cargar secretos ni iniciar la API.</summary>
public sealed class FirmaOperaCloudDbContextFactory : IDesignTimeDbContextFactory<FirmaOperaCloudDbContext>
{
    public FirmaOperaCloudDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__FirmaOperaCloud")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=FirmaOperaCloud_Design;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<FirmaOperaCloudDbContext>()
            .UseSqlServer(connection)
            .Options;
        return new FirmaOperaCloudDbContext(options);
    }
}
