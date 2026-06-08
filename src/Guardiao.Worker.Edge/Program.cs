using Guardiao.Application.Ports.Outbound;
using Guardiao.Infrastructure.System;
using Guardiao.Worker.Edge.Adapters;
using Guardiao.Worker.Edge.Health;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Pipeline;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<EdgeWorkerOptions>()
    .Bind(builder.Configuration.GetSection(EdgeWorkerOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<EdgeWorkerOptions>, EdgeWorkerOptionsValidator>();

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<BoundedCameraFrameQueue>();
builder.Services.AddSingleton<FrameSamplerFactory>();
builder.Services.AddSingleton<EdgeMetricsCollector>();
builder.Services.AddSingleton<WorkerOperationalState>();
builder.Services.AddSingleton<IMetricsPort>(sp => sp.GetRequiredService<EdgeMetricsCollector>());
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<EdgeWorkerOptions>>().Value;
    return new HttpClient
    {
        BaseAddress = new Uri(options.ApiBaseUrl),
        Timeout = TimeSpan.FromSeconds(options.PublishTimeoutSeconds)
    };
});
builder.Services.AddSingleton<ApiRestrictedGalleryProvider>();
builder.Services.AddSingleton<IRestrictedGalleryProvider>(sp => sp.GetRequiredService<ApiRestrictedGalleryProvider>());
builder.Services.AddSingleton<CandidateEventEvidenceFactory>();
builder.Services.AddSingleton<ICameraCapturePort, AdaptiveCameraCaptureAdapter>();
builder.Services.AddSingleton<IFaceDetectorPort, OpenCvFaceDetectorPort>();
builder.Services.AddSingleton<IFaceTrackerPort, PassthroughFaceTrackerPort>();
builder.Services.AddSingleton<IFaceEmbedderPort, OnnxFaceEmbedderPort>();
builder.Services.AddSingleton<IFaceMatcherPort, RestrictedGalleryMatcherPort>();
builder.Services.AddSingleton<ICandidateEventPublisher, ApiCandidateEventPublisher>();
builder.Services.AddSingleton<CameraPipelineSession>();
builder.Services.AddHostedService<EdgeCameraWorkerService>();
builder.Services.AddHostedService<GalleryRefreshBackgroundService>();
builder.Services.AddHostedService<EdgeHealthEndpointService>();

var host = builder.Build();
await host.RunAsync();
