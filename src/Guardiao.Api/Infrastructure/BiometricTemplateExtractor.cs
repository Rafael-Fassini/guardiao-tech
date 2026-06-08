using Microsoft.Extensions.Options;
using Guardiao.Infrastructure.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Guardiao.Api.Infrastructure;

public interface IBiometricTemplateExtractor
{
    Task<BiometricExtractionResult> ExtractAsync(Stream imageStream, CancellationToken cancellationToken = default);
}

public sealed record BiometricExtractionResult(IReadOnlyCollection<float> Embedding, int DetectedFaceCount);

public sealed class OpenCvOnnxBiometricTemplateExtractor : IBiometricTemplateExtractor, IDisposable
{
    private readonly BiometricProcessingOptions _options;
    private readonly CascadeClassifier _classifier;
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;

    public OpenCvOnnxBiometricTemplateExtractor(IOptions<BiometricProcessingOptions> options)
    {
        _options = options.Value;

        _classifier = new CascadeClassifier();
        if (!_classifier.Load(_options.DetectionModelPath))
        {
            throw new InvalidOperationException($"Could not load face detection model from '{_options.DetectionModelPath}'.");
        }

        var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
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

    public async Task<BiometricExtractionResult> ExtractAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await imageStream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        if (bytes.Length == 0)
        {
            throw new InvalidDataException("Uploaded image was empty.");
        }

        using var source = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (source.Empty())
        {
            throw new InvalidDataException("Uploaded image could not be decoded.");
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

        if (detections.Length == 0)
        {
            throw new InvalidDataException("No face was detected in the uploaded image.");
        }

        if (detections.Length > 1)
        {
            throw new InvalidDataException("The uploaded image must contain a single face.");
        }

        using var crop = new Mat(source, detections[0]);
        using var rgb = new Mat();
        using var resized = new Mat();
        Cv2.CvtColor(crop, rgb, ColorConversionCodes.BGR2RGB);
        Cv2.Resize(rgb, resized, new Size(_options.EmbeddingInputWidth, _options.EmbeddingInputHeight), 0, 0, InterpolationFlags.Linear);

        var tensor = CreateTensor(resized);
        using var results = _session.Run([NamedOnnxValue.CreateFromTensor(_inputName, tensor)]);
        var output = results.Single(x => x.Name == _outputName).AsTensor<float>().ToArray();

        return new BiometricExtractionResult(Normalize(output), detections.Length);
    }

    public void Dispose()
    {
        _classifier.Dispose();
        _session.Dispose();
    }

    private DenseTensor<float> CreateTensor(Mat image)
    {
        var tensor = new DenseTensor<float>([1, 3, _options.EmbeddingInputHeight, _options.EmbeddingInputWidth]);

        for (var y = 0; y < _options.EmbeddingInputHeight; y++)
        {
            for (var x = 0; x < _options.EmbeddingInputWidth; x++)
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

    private static float[] Normalize(IReadOnlyCollection<float> values)
    {
        var array = values.ToArray();
        var norm = MathF.Sqrt(array.Sum(x => x * x));
        if (norm == 0)
        {
            return array;
        }

        for (var index = 0; index < array.Length; index++)
        {
            array[index] /= norm;
        }

        return array;
    }
}
