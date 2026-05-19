using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Options;
using Guardiao.Infrastructure.Persistence;
using Guardiao.Infrastructure.Repositories;
using Guardiao.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardiao.IntegrationTests.Infrastructure;

public class PersistenceIntegrationTests
{
    [Fact]
    public async Task CaseProjectionRepository_ShouldPersistAndReadProjection()
    {
        await using var db = CreateDbContext();
        var repository = new CaseProjectionRepository(db);

        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-repo"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);
        var person = new PersonProjection(
            new ExternalPersonId("person-repo"),
            protectedCase.Id,
            "Repository Person",
            false,
            DateTime.UtcNow);

        protectedCase.BindPersonProjection(person.Id);

        await repository.UpsertAsync(protectedCase, person);
        var loaded = await repository.GetByExternalIdAsync(new ExternalCaseId("case-repo"));

        Assert.NotNull(loaded);
        Assert.Equal("case-repo", loaded!.ExternalCaseId.Value);
    }

    [Fact]
    public async Task BiometricTemplateRepository_ShouldPersistTemplates()
    {
        await using var db = CreateDbContext();
        var repository = new BiometricTemplateRepository(db);

        var template = new BiometricTemplate(
            Guid.NewGuid(),
            new ExternalPersonId("person-template"),
            [0.1f, 0.2f, 0.3f],
            RetentionMode.CaseBound,
            false);

        await repository.AddAsync(template);
        var loaded = await repository.ListByPersonAsync(template.PersonProjectionId);

        Assert.Single(loaded);
    }

    [Fact]
    public async Task EvidenceStorageAdapter_ShouldWriteObjectToConfiguredRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "guardiao-storage-tests", Guid.NewGuid().ToString("N"));
        var adapter = new MinioEvidenceStorageAdapter(Options.Create(new ObjectStorageOptions
        {
            BucketName = "test-bucket",
            RootPath = root,
            AllowedContentTypes = ["image/jpeg"],
            AllowedFileExtensions = [".jpg"]
        }));

        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("evidence"));
        var objectKey = await adapter.StoreAsync(content, "evidence.jpg", "image/jpeg");

        var fullPath = Path.Combine(root, objectKey.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath));
    }

    [Fact]
    public void Migration_ShouldBeVersionControlled()
    {
        var migrationPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "src", "Guardiao.Infrastructure", "Persistence", "Migrations", "202605120401_InitialPersistence.cs");

        Assert.True(File.Exists(Path.GetFullPath(migrationPath)));
    }

    private static GuardiaoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GuardiaoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new GuardiaoDbContext(options);
    }
}
