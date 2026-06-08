using Guardiao.Worker.Edge.Options;
using Xunit;

namespace Guardiao.UnitTests.Worker;

public class EdgeWorkerOptionsValidatorTests
{
    [Fact]
    public void Validate_ShouldFail_WhenModelFilesAreMissing()
    {
        var validator = new EdgeWorkerOptionsValidator();
        var options = new EdgeWorkerOptions
        {
            ApiSharedSecret = "worker-secret",
            WorkerId = "edge-worker-01",
            DetectionModelPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml"),
            EmbeddingModelPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.onnx"),
            Cameras =
            [
                new EdgeCameraOptions
                {
                    CameraId = Guid.NewGuid(),
                    SiteId = Guid.NewGuid(),
                    ProtectedCaseId = Guid.NewGuid(),
                    Name = "Camera 1",
                    Source = "webcam://0",
                    Enabled = true
                }
            ]
        };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("DetectionModelPath", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("EmbeddingModelPath", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenRequiredFilesExist()
    {
        var detectionPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
        var embeddingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.onnx");
        File.WriteAllText(detectionPath, "<cascade/>");
        File.WriteAllBytes(embeddingPath, [0x01, 0x02, 0x03]);

        try
        {
            var validator = new EdgeWorkerOptionsValidator();
            var options = new EdgeWorkerOptions
            {
                ApiSharedSecret = "worker-secret",
                WorkerId = "edge-worker-01",
                DetectionModelPath = detectionPath,
                EmbeddingModelPath = embeddingPath,
                Cameras =
                [
                    new EdgeCameraOptions
                    {
                        CameraId = Guid.NewGuid(),
                        SiteId = Guid.NewGuid(),
                        ProtectedCaseId = Guid.NewGuid(),
                        Name = "Camera 1",
                        Source = "webcam://0",
                        Enabled = true
                    }
                ]
            };

            var result = validator.Validate(null, options);

            Assert.True(result.Succeeded);
        }
        finally
        {
            File.Delete(detectionPath);
            File.Delete(embeddingPath);
        }
    }
}
