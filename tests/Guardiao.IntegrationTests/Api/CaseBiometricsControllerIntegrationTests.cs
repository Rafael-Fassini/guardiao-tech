using System.Net;
using System.Net.Http.Headers;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Guardiao.IntegrationTests.Api;

public class CaseBiometricsControllerIntegrationTests : IClassFixture<GuardiaoApiFactory>
{
    private readonly GuardiaoApiFactory _factory;

    public CaseBiometricsControllerIntegrationTests(GuardiaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Upload_ShouldPersistTemplate_AndListIt()
    {
        var (client, caseId) = await CreateAuthorizedClientWithCaseAsync();

        using var form = CreateImageForm("face.png", "image/png", [1, 2, 3, 4]);
        var upload = await client.PostAsync($"/api/cases/{caseId}/biometrics", form);

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);

        var list = await client.GetAsync($"/api/cases/{caseId}/biometrics");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        Assert.Single(await db.BiometricTemplates.ToListAsync());
        Assert.Contains(await db.AuditLogs.ToListAsync(), x => x.Action == "biometric_template.created");
    }

    [Fact]
    public async Task Upload_ShouldReturnBadRequest_WhenExtractorRejectsImage()
    {
        _factory.BiometricExtractor.ForceNoFace = true;
        try
        {
            var (client, caseId) = await CreateAuthorizedClientWithCaseAsync();
            using var form = CreateImageForm("face.png", "image/png", [1, 2, 3, 4]);

            var response = await client.PostAsync($"/api/cases/{caseId}/biometrics", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _factory.BiometricExtractor.ForceNoFace = false;
        }
    }

    [Fact]
    public async Task Delete_ShouldDeactivateTemplate()
    {
        var (client, caseId) = await CreateAuthorizedClientWithCaseAsync();
        using var form = CreateImageForm("face.png", "image/png", [1, 2, 3, 4]);
        var upload = await client.PostAsync($"/api/cases/{caseId}/biometrics", form);
        var payload = await upload.Content.ReadAsStringAsync();
        var created = System.Text.Json.JsonDocument.Parse(payload).RootElement;
        var templateId = created.GetProperty("id").GetGuid();

        var deactivate = await client.DeleteAsync($"/api/cases/{caseId}/biometrics/{templateId}");

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        var template = await db.BiometricTemplates.SingleAsync(x => x.Id == templateId);
        Assert.False(template.IsActive);
    }

    [Fact]
    public async Task Gallery_ShouldReturnActiveTemplates_ForWorker()
    {
        var (client, caseId) = await CreateAuthorizedClientWithCaseAsync(role: "operator");
        using var form = CreateImageForm("face.png", "image/png", [1, 2, 3, 4]);
        await client.PostAsync($"/api/cases/{caseId}/biometrics", form);

        using var workerClient = _factory.CreateClient();
        workerClient.DefaultRequestHeaders.Add("X-Worker-Id", "edge-worker-01");
        workerClient.DefaultRequestHeaders.Add("X-Worker-Auth", "worker-test-secret");

        var response = await workerClient.GetAsync($"/api/cases/{caseId}/gallery?siteId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<(HttpClient Client, Guid CaseId)> CreateAuthorizedClientWithCaseAsync(string role = "operator")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "integration-user");
        client.DefaultRequestHeaders.Add("X-Debug-Role", role);

        Guid caseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            var protectedCase = new ProtectedCase(
                new ExternalCaseId($"case-{Guid.NewGuid():N}"),
                1,
                Guid.NewGuid(),
                Guid.NewGuid(),
                MonitoringStatus.Enabled,
                ConsentStatus.Granted);
            var person = new PersonProjection(
                new ExternalPersonId($"person-{Guid.NewGuid():N}"),
                protectedCase.Id,
                "Case Person",
                false,
                DateTime.UtcNow);
            protectedCase.BindPersonProjection(person.Id);

            db.ProtectedCases.Add(protectedCase);
            db.PersonProjections.Add(person);
            await db.SaveChangesAsync();
            caseId = protectedCase.Id;
        }

        return (client, caseId);
    }

    private static MultipartFormDataContent CreateImageForm(string name, string contentType, byte[] content)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", name);
        return form;
    }
}
