namespace Guardiao.Worker.Edge.Services;

public static class EmbeddingVectorMath
{
    public static float[] Normalize(IReadOnlyCollection<float> values)
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

    public static double CosineSimilarity(IReadOnlyCollection<float> left, IReadOnlyCollection<float> right)
    {
        var leftArray = left.ToArray();
        var rightArray = right.ToArray();
        var length = Math.Min(leftArray.Length, rightArray.Length);
        if (length == 0)
        {
            return 0;
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var index = 0; index < length; index++)
        {
            dot += leftArray[index] * rightArray[index];
            leftNorm += leftArray[index] * leftArray[index];
            rightNorm += rightArray[index] * rightArray[index];
        }

        if (leftNorm == 0 || rightNorm == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }
}
