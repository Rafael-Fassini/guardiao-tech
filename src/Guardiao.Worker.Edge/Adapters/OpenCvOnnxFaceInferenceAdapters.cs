using System.Numerics.Tensors;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.ValueObjects;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Pipeline;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Guardiao.Worker.Edge.Adapters;

public sealed class OpenCvFaceDetectorPort : IFaceDetectorPort, IDisposable
{
    private readonly EdgeWorkerOptions _options;
    private readonly CascadeClassifier _classifier;

    public OpenCvFaceDetectorPort(IOptions<EdgeWorkerOptions> options)
    {
        _options = options.Value;
        _classifier = new CascadeClassifier();

        if (!_classifier.Load(_options.DetectionModelPath))
        {
            throw new InvalidOperationException($"Could not load face detection model from '{_options.DetectionModelPath}'.");
        }
    }

    public async Task<IReadOnlyCollection<DetectedFace>> DetectAsync(Stream frame, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await frame.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (bytes.Length == 0)
        {
            return [];
        }

        using var source = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (source.Empty())
        {
            return [];
        }

        using var grayscale = new Mat();
        Cv2.CvtColor(source, grayscale, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(grayscale, grayscale);

        var detections = _classifier.DetectMultiScale(
            grayscale,
            _options.DetectionScaleFactor,
            _options.DetectionMinNeighbors,
            HaarDetectionTypes.ScaleImage,
            new Size(_options.DetectionMinFaceSizePixels, _options.DetectionMinFaceSizePixels));

        var faces = new List<DetectedFace>(detections.Length);
        foreach (var detection in detections)
        {
            using var crop = new Mat(source, detection);
            Cv2.ImEncode(".png", crop, out var cropBytes);
            faces.Add(new DetectedFace(Guid.NewGuid(), cropBytes));
        }

        return faces;
    }

    public void Dispose()
    {
        _classifier.Dispose();
    }
}

public sealed class PassthroughFaceTrackerPort : IFaceTrackerPort
{
    public Task<IReadOnlyCollection<TrackedFace>> TrackAsync(IReadOnlyCollection<DetectedFace> faces, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<TrackedFace>>(
            [.. faces.Select(x => new TrackedFace(x.DetectionId, x.CropBytes))]);
    }
}

public sealed class OnnxFaceEmbedderPort : IFaceEmbedderPort, IDisposable
{
    private readonly EdgeWorkerOptions _options;
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;

    public OnnxFaceEmbedderPort(IOptions<EdgeWorkerOptions> options)
    {
        _options = options.Value;

        var sessionOptions = new SessionOptions
        {
            InterOpNumThreads = _options.OnnxInterOpThreads,
            IntraOpNumThreads = _options.OnnxIntraOpThreads,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _session = new InferenceSession(_options.EmbeddingModelPath, sessionOptions);
        _inputName = string.IsNullOrWhiteSpace(_options.EmbeddingInputName)
            ? _session.InputMetadata.Keys.First()
            : _options.EmbeddingInputName;
        _outputName = string.IsNullOrWhiteSpace(_options.EmbeddingOutputName)
            ? _session.OutputMetadata.Keys.First()
            : _options.EmbeddingOutputName;
    }

    public Task<IReadOnlyCollection<float>> CreateEmbeddingAsync(TrackedFace face, CancellationToken cancellationToken = default)
    {
        using var image = Cv2.ImDecode(face.CropBytes, ImreadModes.Color);
        if (image.Empty())
        {
            return Task.FromResult<IReadOnlyCollection<float>>([]);
        }

        using var rgb = new Mat();
        using var resized = new Mat();
        Cv2.CvtColor(image, rgb, ColorConversionCodes.BGR2RGB);
        Cv2.Resize(rgb, resized, new Size(_options.EmbeddingInputWidth, _options.EmbeddingInputHeight), 0, 0, InterpolationFlags.Linear);

        var tensor = CreateTensor(resized);
        using var results = _session.Run([NamedOnnxValue.CreateFromTensor(_inputName, tensor)]);
        var output = results.Single(x => x.Name == _outputName).AsTensor<float>().ToArray();

        return Task.FromResult<IReadOnlyCollection<float>>(EmbeddingVectorMath.Normalize(output));
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    private DenseTensor<float> CreateTensor(Mat image)
    {
        var height = _options.EmbeddingInputHeight;
        var width = _options.EmbeddingInputWidth;
        var tensor = new DenseTensor<float>([1, 3, height, width]);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = image.At<Vec3b>(y, x);
                tensor[0, 0, y, x] = NormalizePixel(pixel.Item0);
                tensor[0, 1, y, x] = NormalizePixel(pixel.Item1);
                tensor[0, 2, y, x] = NormalizePixel(pixel.Item2);
            }
        }

        return tensor;
    }

    private float NormalizePixel(byte channel)
    {
        return (float)((channel - _options.EmbeddingPixelMean) / _options.EmbeddingPixelStdDev);
    }
}

public sealed class RestrictedGalleryMatcherPort : IFaceMatcherPort
{
    private readonly IRestrictedGalleryProvider _galleryProvider;
    private readonly EdgeMetricsCollector _metrics;
    private readonly double _matchThreshold;

    public RestrictedGalleryMatcherPort(
        IRestrictedGalleryProvider galleryProvider,
        EdgeMetricsCollector metrics,
        IOptions<EdgeWorkerOptions> options)
    {
        _galleryProvider = galleryProvider;
        _metrics = metrics;
        _matchThreshold = options.Value.MatchThreshold;
    }

    public Task<MatchScore> MatchAsync(IReadOnlyCollection<float> embedding, Guid protectedCaseId, CancellationToken cancellationToken = default)
    {
        var gallery = _galleryProvider.GetByProtectedCase(protectedCaseId);
        if (gallery.Count == 0)
        {
            return Task.FromResult(new MatchScore(0));
        }

        var score = gallery
            .Where(x => !x.IsBystander)
            .Select(x => EmbeddingVectorMath.CosineSimilarity(embedding, x.Embedding))
            .DefaultIfEmpty(0)
            .Max();

        _metrics.RecordGauge("gallery_match_score", score, ("case", protectedCaseId.ToString()));
        return Task.FromResult(new MatchScore(score));
    }

    public GalleryMatchResult MatchWithinScope(IReadOnlyCollection<float> embedding, Guid protectedCaseId, Guid siteId)
    {
        var best = _galleryProvider.GetByScope(protectedCaseId, siteId)
            .Select(x => new
            {
                Candidate = x,
                Score = EmbeddingVectorMath.CosineSimilarity(embedding, x.Embedding)
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (best is null)
        {
            return new GalleryMatchResult(protectedCaseId, siteId, Guid.Empty, string.Empty, 0, false, false);
        }

        var isMatch = !best.Candidate.IsBystander && best.Score >= _matchThreshold;
        return new GalleryMatchResult(
            best.Candidate.ProtectedCaseId,
            best.Candidate.SiteId,
            best.Candidate.PersonProjectionId,
            best.Candidate.ExternalPersonId,
            best.Score,
            isMatch,
            best.Candidate.IsBystander);
    }
}
