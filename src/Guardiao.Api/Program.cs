using Guardiao.Application.Ports.Inbound;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.Services;
using Guardiao.Infrastructure.Clients;
using Guardiao.Infrastructure.HostedServices;
using Guardiao.Infrastructure.Messaging;
using Guardiao.Infrastructure.Options;
using Guardiao.Application.UseCases;
using Guardiao.Infrastructure.Persistence;
using Guardiao.Infrastructure.Repositories;
using Guardiao.Infrastructure.Security;
using Guardiao.Infrastructure.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

// EF Core
builder.Services.AddDbContext<GuardiaoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddOptions<VictimRegistryOptions>()
    .Bind(builder.Configuration.GetSection(VictimRegistryOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<VictimRegistryOptions>, VictimRegistryOptionsValidator>();

builder.Services.AddHttpClient<IVictimRegistryAccessTokenProvider, VictimRegistryClientCredentialsTokenProvider>()
    .ConfigurePrimaryHttpMessageHandler(sp => sp.GetService<HttpMessageHandler>() ?? new HttpClientHandler());

builder.Services.AddHttpClient<VictimRegistryHttpClientAdapter>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<VictimRegistryOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .ConfigurePrimaryHttpMessageHandler(sp => sp.GetService<HttpMessageHandler>() ?? new HttpClientHandler());

// Hexagonal wiring: API resolves inbound use cases and infrastructure adapters.
builder.Services.AddScoped<ICreateInstitutionUseCase, CreateInstitutionUseCase>();
builder.Services.AddScoped<IInstitutionRepositoryPort, InstitutionRepository>();
builder.Services.AddScoped<IVictimRegistryPort>(sp => sp.GetRequiredService<VictimRegistryHttpClientAdapter>());
builder.Services.AddScoped<IVictimRegistryMediaPort>(sp => sp.GetRequiredService<VictimRegistryHttpClientAdapter>());
builder.Services.AddScoped<ICaseProjectionRepository, CaseProjectionRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
builder.Services.AddScoped<ISyncCursorRepository, SyncCursorRepository>();
builder.Services.AddSingleton<IVictimRegistrySyncQueue, InMemoryVictimRegistrySyncQueue>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IWebhookSignatureVerifier, HmacSha256WebhookSignatureVerifier>();
builder.Services.AddScoped<VictimRegistrySyncService>();
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<VictimRegistryOptions>>().Value;
    return new VictimRegistryWebhookService(
        sp.GetRequiredService<IWebhookSignatureVerifier>(),
        sp.GetRequiredService<IWebhookDeliveryRepository>(),
        sp.GetRequiredService<IVictimRegistrySyncQueue>(),
        sp.GetRequiredService<IClock>(),
        TimeSpan.FromSeconds(options.AllowedClockSkewSeconds));
});
builder.Services.AddHostedService<VictimRegistryWebhookWorker>();
builder.Services.AddHostedService<VictimRegistryReconciliationBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/ready", () => Results.Ok(new { status = "Ready" }));

app.Run();

public partial class Program;
