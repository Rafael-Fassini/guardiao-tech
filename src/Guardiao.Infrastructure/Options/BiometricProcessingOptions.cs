using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Options;

public sealed class BiometricProcessingOptions
{
    public const string SectionName = "BiometricProcessing";

    public string DetectionModelPath { get; set; } = "models/haarcascade_frontalface_default.xml";
    public double DetectionScaleFactor { get; set; } = 1.1;
    public int DetectionMinNeighbors { get; set; } = 4;
    public int DetectionMinFaceSizePixels { get; set; } = 48;
    public string EmbeddingModelPath { get; set; } = "models/face-embedding.onnx";
    public string EmbeddingInputName { get; set; } = string.Empty;
    public string EmbeddingOutputName { get; set; } = string.Empty;
    public int EmbeddingInputWidth { get; set; } = 112;
    public int EmbeddingInputHeight { get; set; } = 112;
    public double EmbeddingPixelMean { get; set; } = 127.5;
    public double EmbeddingPixelStdDev { get; set; } = 128.0;
    public int OnnxIntraOpThreads { get; set; } = 1;
    public int OnnxInterOpThreads { get; set; } = 1;
}

public sealed class BiometricProcessingOptionsValidator : IValidateOptions<BiometricProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, BiometricProcessingOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DetectionModelPath))
        {
            errors.Add("BiometricProcessing:DetectionModelPath is required.");
        }
        else if (!File.Exists(options.DetectionModelPath))
        {
            errors.Add($"BiometricProcessing:DetectionModelPath was not found at '{options.DetectionModelPath}'.");
        }

        if (options.DetectionScaleFactor <= 1.0)
        {
            errors.Add("BiometricProcessing:DetectionScaleFactor must be greater than 1.");
        }

        if (options.DetectionMinNeighbors < 0)
        {
            errors.Add("BiometricProcessing:DetectionMinNeighbors must be zero or greater.");
        }

        if (options.DetectionMinFaceSizePixels <= 0)
        {
            errors.Add("BiometricProcessing:DetectionMinFaceSizePixels must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.EmbeddingModelPath))
        {
            errors.Add("BiometricProcessing:EmbeddingModelPath is required.");
        }
        else if (!File.Exists(options.EmbeddingModelPath))
        {
            errors.Add($"BiometricProcessing:EmbeddingModelPath was not found at '{options.EmbeddingModelPath}'.");
        }

        if (options.EmbeddingInputWidth <= 0)
        {
            errors.Add("BiometricProcessing:EmbeddingInputWidth must be greater than zero.");
        }

        if (options.EmbeddingInputHeight <= 0)
        {
            errors.Add("BiometricProcessing:EmbeddingInputHeight must be greater than zero.");
        }

        if (options.EmbeddingPixelStdDev <= 0)
        {
            errors.Add("BiometricProcessing:EmbeddingPixelStdDev must be greater than zero.");
        }

        if (options.OnnxIntraOpThreads <= 0)
        {
            errors.Add("BiometricProcessing:OnnxIntraOpThreads must be greater than zero.");
        }

        if (options.OnnxInterOpThreads <= 0)
        {
            errors.Add("BiometricProcessing:OnnxInterOpThreads must be greater than zero.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
