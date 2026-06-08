using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using Xunit;

namespace Guardiao.UnitTests.Worker;

public class CandidateEventEvidenceFactoryTests
{
    [Fact]
    public void Create_ShouldGenerateSnapshotAndFaceCrop()
    {
        var metrics = new EdgeMetricsCollector();
        var factory = new CandidateEventEvidenceFactory(
            Options.Create(new EdgeWorkerOptions
            {
                EvidenceSnapshotMaxWidthPixels = 320,
                EvidenceJpegQuality = 85
            }),
            metrics,
            NullLogger<CandidateEventEvidenceFactory>.Instance);

        var frameBytes = EncodeImage(800, 600);
        var cropBytes = EncodeImage(112, 112);

        var evidences = factory.Create(Guid.NewGuid(), frameBytes, cropBytes);

        Assert.Equal(2, evidences.Count);
        Assert.Contains(evidences, x => x.ArtifactType == "Snapshot" && x.ContentType == "image/jpeg");
        Assert.Contains(evidences, x => x.ArtifactType == "FaceCrop" && x.ContentType == "image/jpeg");
    }

    private static byte[] EncodeImage(int width, int height)
    {
        using var image = new Mat(new Size(width, height), MatType.CV_8UC3, Scalar.LightGray);
        Cv2.ImEncode(".png", image, out var bytes);
        return bytes;
    }
}
