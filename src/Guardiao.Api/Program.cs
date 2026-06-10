using Guardiao.Api.Infrastructure;
using Guardiao.Application.Ports.Inbound;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.Services;
using Guardiao.Infrastructure.Clients;
using Guardiao.Infrastructure.Caching;
using Guardiao.Infrastructure.HostedServices;
using Guardiao.Infrastructure.Messaging;
using Guardiao.Infrastructure.Notifications;
using Guardiao.Infrastructure.Options;
using Guardiao.Application.UseCases;
using Guardiao.Infrastructure.Persistence;
using Guardiao.Infrastructure.Repositories;
using Guardiao.Infrastructure.Security;
using Guardiao.Infrastructure.System;
using Guardiao.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Title = "Validation failed.",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };

        return new BadRequestObjectResult(problem);
    };
});

builder.Services.AddAuthentication(HeaderAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(
        HeaderAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy(AuthorizationPolicies.MetadataRead, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(AuthorizationPolicies.CasesRead, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(AuthorizationPolicies.IncidentsRead, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(AuthorizationPolicies.RulesManage, policy => policy.RequireRole("admin", "operator"));
    options.AddPolicy(AuthorizationPolicies.IncidentsReview, policy => policy.RequireRole("operator"));
    options.AddPolicy(AuthorizationPolicies.AuditRead, policy => policy.RequireRole("admin", "auditor", "operator"));
    options.AddPolicy(AuthorizationPolicies.CandidateEventsIngest, policy => policy.RequireRole("worker"));
    options.AddPolicy(AuthorizationPolicies.BiometricGalleryRead, policy => policy.RequireRole("admin", "operator", "auditor", "worker"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(SecurityRateLimitPolicies.ApiWrites, context =>
    {
        var security = context.RequestServices.GetRequiredService<IOptions<ApiSecurityOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = security.ApiWriteRateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(security.ApiWriteRateLimitWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.AddPolicy(SecurityRateLimitPolicies.Webhook, context =>
    {
        var security = context.RequestServices.GetRequiredService<IOptions<ApiSecurityOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "webhook",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = security.WebhookRateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(security.WebhookRateLimitWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

// EF Core
builder.Services.AddDbContext<GuardiaoDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly(typeof(GuardiaoDbContext).Assembly.FullName)));

builder.Services
    .AddOptions<VictimRegistryOptions>()
    .Bind(builder.Configuration.GetSection(VictimRegistryOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<VictimRegistryOptions>, VictimRegistryOptionsValidator>();
builder.Services
    .AddOptions<RedisOptions>()
    .Bind(builder.Configuration.GetSection(RedisOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<RedisOptions>, RedisOptionsValidator>();
builder.Services
    .AddOptions<ObjectStorageOptions>()
    .Bind(builder.Configuration.GetSection(ObjectStorageOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ObjectStorageOptions>, ObjectStorageOptionsValidator>();
builder.Services
    .AddOptions<RetentionOptions>()
    .Bind(builder.Configuration.GetSection(RetentionOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<RetentionOptions>, RetentionOptionsValidator>();
builder.Services
    .AddOptions<BiometricProcessingOptions>()
    .Bind(builder.Configuration.GetSection(BiometricProcessingOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<BiometricProcessingOptions>, BiometricProcessingOptionsValidator>();
builder.Services
    .AddOptions<ApiSecurityOptions>()
    .Bind(builder.Configuration.GetSection(ApiSecurityOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ApiSecurityOptions>, ApiSecurityOptionsValidator>();
builder.Services
    .AddOptions<HousekeepingOptions>()
    .Bind(builder.Configuration.GetSection(HousekeepingOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<HousekeepingOptions>, HousekeepingOptionsValidator>();
builder.Services
    .AddOptions<OperationalNotificationsOptions>()
    .Bind(builder.Configuration.GetSection(OperationalNotificationsOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<OperationalNotificationsOptions>, OperationalNotificationsOptionsValidator>();
builder.Services
    .AddOptions<WorkerPreviewOptions>()
    .Bind(builder.Configuration.GetSection(WorkerPreviewOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<WorkerPreviewOptions>, WorkerPreviewOptionsValidator>();

builder.Services.AddHttpClient<IVictimRegistryAccessTokenProvider, VictimRegistryClientCredentialsTokenProvider>()
    .ConfigurePrimaryHttpMessageHandler(sp => sp.GetService<HttpMessageHandler>() ?? new HttpClientHandler());

builder.Services.AddHttpClient<VictimRegistryHttpClientAdapter>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<VictimRegistryOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .ConfigurePrimaryHttpMessageHandler(sp => sp.GetService<HttpMessageHandler>() ?? new HttpClientHandler());
builder.Services.AddHttpClient<WebhookIncidentNotificationChannel>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<OperationalNotificationsOptions>>().Value;
        if (Uri.TryCreate(options.WebhookUrl, UriKind.Absolute, out var webhookUri))
        {
            client.BaseAddress = new Uri($"{webhookUri.Scheme}://{webhookUri.Authority}");
        }

        client.Timeout = TimeSpan.FromSeconds(options.DeliveryTimeoutSeconds);
    })
    .ConfigurePrimaryHttpMessageHandler(sp => sp.GetService<HttpMessageHandler>() ?? new HttpClientHandler());
builder.Services.AddHttpClient<ICameraLivePreviewPort, HttpCameraLivePreviewPort>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<WorkerPreviewOptions>>().Value;
        var security = sp.GetRequiredService<IOptions<ApiSecurityOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Worker-Preview-Auth", security.WorkerSharedSecret);
    })
    .ConfigurePrimaryHttpMessageHandler(sp => sp.GetService<HttpMessageHandler>() ?? new HttpClientHandler());

// Hexagonal wiring: API resolves inbound use cases and infrastructure adapters.
builder.Services.AddScoped<ICreateInstitutionUseCase, CreateInstitutionUseCase>();
builder.Services.AddScoped<IInstitutionRepositoryPort, InstitutionRepository>();
builder.Services.AddScoped<IVictimRegistryPort>(sp => sp.GetRequiredService<VictimRegistryHttpClientAdapter>());
builder.Services.AddScoped<IVictimRegistryMediaPort>(sp => sp.GetRequiredService<VictimRegistryHttpClientAdapter>());
builder.Services.AddScoped<ICaseProjectionRepository, CaseProjectionRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();
builder.Services.AddScoped<IMonitoringRuleRepository, MonitoringRuleRepository>();
builder.Services.AddScoped<ICorrelationDecisionRepository, CorrelationDecisionRepository>();
builder.Services.AddScoped<ICandidateEventRepository, CandidateEventRepository>();
builder.Services.AddScoped<IBiometricTemplateRepository, BiometricTemplateRepository>();
builder.Services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
builder.Services.AddScoped<ISyncCursorRepository, SyncCursorRepository>();
builder.Services.AddScoped<IEvidenceStoragePort, MinioEvidenceStorageAdapter>();
builder.Services.AddScoped<IEvidenceArtifactRepository, EvidenceArtifactRepository>();
builder.Services.AddSingleton<IVictimRegistrySyncQueue, InMemoryVictimRegistrySyncQueue>();
builder.Services.AddSingleton<IClock, Guardiao.Infrastructure.System.SystemClock>();
builder.Services.AddSingleton<IShortLivedStateStore, RedisShortLivedStateStore>();
builder.Services.AddSingleton<IShortLivedStatePort>(sp => sp.GetRequiredService<IShortLivedStateStore>() as IShortLivedStatePort
    ?? throw new InvalidOperationException("Short-lived state store does not implement application port."));
builder.Services.AddSingleton<IRetentionPolicyProvider, RetentionPolicyProvider>();
builder.Services.AddScoped<PendingIncidentEscalationService>();
builder.Services.AddScoped<INotificationPort, OperationalNotificationPort>();
builder.Services.AddScoped<IIncidentNotificationChannel>(sp => sp.GetRequiredService<WebhookIncidentNotificationChannel>());
builder.Services.AddScoped<IIncidentNotificationChannel, SmtpIncidentNotificationChannel>();
builder.Services.AddScoped<IWebhookSignatureVerifier, HmacSha256WebhookSignatureVerifier>();
builder.Services.AddSingleton<IBiometricTemplateExtractor, OpenCvOnnxBiometricTemplateExtractor>();
builder.Services.AddSingleton<SensitiveDataRedactor>();
builder.Services.AddSingleton<ApiMetricsCollector>();
builder.Services.AddSingleton<ApiReadinessService>();
builder.Services.AddSingleton<IMetricsPort>(sp => sp.GetRequiredService<ApiMetricsCollector>());
builder.Services.AddScoped(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>().GetSection("CorrelationEngine");
    var coPresenceWindowMinutes = configuration.GetValue<double?>("CoPresenceWindowMinutes") ?? 5;
    var duplicateSuppressionWindowSeconds = configuration.GetValue<double?>("DuplicateSuppressionWindowSeconds") ?? 30;
    var requireSameSiteForCoPresence = configuration.GetValue<bool?>("RequireSameSiteForCoPresence") ?? true;

    return new CorrelationEngineOptions
    {
        CoPresenceWindow = TimeSpan.FromMinutes(coPresenceWindowMinutes),
        DuplicateSuppressionWindow = TimeSpan.FromSeconds(duplicateSuppressionWindowSeconds),
        RequireSameSiteForCoPresence = requireSameSiteForCoPresence
    };
});
builder.Services.AddScoped<CandidateEventCorrelationService>();
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
builder.Services.AddHostedService<EvidenceEligibilityScanBackgroundService>();
builder.Services.AddHostedService<PendingIncidentEscalationBackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    var securityOptions = app.Services.GetRequiredService<IOptions<ApiSecurityOptions>>().Value;
    if (securityOptions.EnableSwaggerUi)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
}

app.UseRateLimiter();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestBodyLimitMiddleware>();
app.UseMiddleware<DomainExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapGet("/ready", async (ApiReadinessService readiness, CancellationToken cancellationToken) =>
{
    var status = await readiness.CheckAsync(cancellationToken);
    return status.IsReady
        ? Results.Ok(new
        {
            status = "Ready",
            databaseReachable = status.DatabaseReachable,
            migrationsApplied = status.MigrationsApplied,
            objectStorageWritable = status.ObjectStorageWritable
        })
        : Results.Json(new
        {
            status = "NotReady",
            databaseReachable = status.DatabaseReachable,
            migrationsApplied = status.MigrationsApplied,
            objectStorageWritable = status.ObjectStorageWritable,
            databaseError = status.DatabaseError,
            objectStorageError = status.ObjectStorageError
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();
app.MapGet("/metrics", (ApiMetricsCollector metrics) => Results.Ok(new
{
    counters = metrics.SnapshotCounters(),
    gauges = metrics.SnapshotGauges()
})).AllowAnonymous();

app.Run();

public partial class Program;
