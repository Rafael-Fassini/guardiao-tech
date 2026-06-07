using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Guardiao.UnitTests.Infrastructure;

public class MigrationMetadataTests
{
    [Fact]
    public void GetMigrations_ShouldExposeInitialPersistenceMigration()
    {
        var options = new DbContextOptionsBuilder<GuardiaoDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=guardiao_db;Username=guardiao_user;Password=test",
                npgsql => npgsql.MigrationsAssembly(typeof(GuardiaoDbContext).Assembly.FullName))
            .Options;

        using var context = new GuardiaoDbContext(options);
        var migrations = context.Database.GetMigrations().ToArray();

        Assert.Contains("202605120401_InitialPersistence", migrations);
    }
}
