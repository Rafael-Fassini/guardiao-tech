using System.Net;
using System.Net.Http.Json;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Guardiao.IntegrationTests.Api;

public class CasesControllerIntegrationTests : IClassFixture<GuardiaoApiFactory>
{
    private readonly GuardiaoApiFactory _factory;

    public CasesControllerIntegrationTests(GuardiaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCases_ShouldExposeSubjectRole()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();

        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-role-list"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            Guardiao.Domain.Enums.MonitoredSubjectRole.Aggressor);

        db.ProtectedCases.Add(protectedCase);
        await db.SaveChangesAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "case-reader");
        client.DefaultRequestHeaders.Add("X-Debug-Role", "operator");

        var response = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<Guardiao.Api.Contracts.ProtectedCaseListItemResponse>>();
        var item = Assert.Single(payload!.Where(x => x.Id == protectedCase.Id));
        Assert.Equal("Aggressor", item.SubjectRole);
    }

    [Fact]
    public async Task PutSubjectRole_ShouldUpdateCaseAndWriteAudit_WhenRoleChanges()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();

        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-role-update"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);

        db.ProtectedCases.Add(protectedCase);
        await db.SaveChangesAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "case-admin");
        client.DefaultRequestHeaders.Add("X-Debug-Role", "admin");

        var response = await client.PutAsJsonAsync($"/api/cases/{protectedCase.Id}/subject-role", new { subjectRole = "Aggressor" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reloaded = await db.ProtectedCases.SingleAsync(x => x.Id == protectedCase.Id);
        var auditEntries = await db.AuditLogs.Where(x => x.EntityId == protectedCase.Id.ToString()).ToListAsync();

        Assert.Equal(Guardiao.Domain.Enums.MonitoredSubjectRole.Aggressor, reloaded.SubjectRole);
        Assert.Contains(auditEntries, x => x.Action == "protected_case.subject_role.updated");
    }

    [Fact]
    public async Task PutSubjectRole_ShouldNotDuplicateAudit_WhenRoleDoesNotChange()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();

        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-role-same"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);

        db.ProtectedCases.Add(protectedCase);
        await db.SaveChangesAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "case-admin");
        client.DefaultRequestHeaders.Add("X-Debug-Role", "admin");

        var response = await client.PutAsJsonAsync($"/api/cases/{protectedCase.Id}/subject-role", new { subjectRole = "ProtectedWoman" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auditEntries = await db.AuditLogs.Where(x => x.EntityId == protectedCase.Id.ToString()).ToListAsync();
        Assert.DoesNotContain(auditEntries, x => x.Action == "protected_case.subject_role.updated");
    }

    [Fact]
    public async Task PutSubjectRole_ShouldReturnBadRequest_WhenRoleIsInvalid()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();

        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-role-invalid"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);

        db.ProtectedCases.Add(protectedCase);
        await db.SaveChangesAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "case-admin");
        client.DefaultRequestHeaders.Add("X-Debug-Role", "admin");

        var response = await client.PutAsJsonAsync($"/api/cases/{protectedCase.Id}/subject-role", new { subjectRole = "UnknownRole" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
