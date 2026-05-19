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
builder.Services.AddSingleton<IMetricsPort>(sp => sp.GetRequiredService<EdgeMetricsCollector>());
builder.Services.AddSingleton<IRestrictedGalleryProvider, RestrictedGalleryProvider>();
builder.Services.AddSingleton<ICameraCapturePort, AdaptiveCameraCaptureAdapter>();
builder.Services.AddSingleton<IFaceDetectorPort, DeterministicFaceDetectorPort>();
builder.Services.AddSingleton<IFaceTrackerPort, DeterministicFaceTrackerPort>();
builder.Services.AddSingleton<IFaceEmbedderPort, DeterministicFaceEmbedderPort>();
builder.Services.AddSingleton<IFaceMatcherPort, RestrictedGalleryMatcherPort>();
builder.Services.AddSingleton<ICandidateEventPublisher, InMemoryCandidateEventPublisher>();
builder.Services.AddSingleton<CameraPipelineSession>();
builder.Services.AddHostedService<EdgeCameraWorkerService>();
builder.Services.AddHostedService<EdgeHealthEndpointService>();

var host = builder.Build();
await host.RunAsync();
