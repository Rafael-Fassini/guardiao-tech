namespace Guardiao.Worker.Edge.Pipeline;

public sealed class FrameSampler
{
    private readonly TimeSpan _interval;
    private DateTime _nextEligibleUtc = DateTime.MinValue;

    public FrameSampler(int targetFps)
    {
        _interval = TimeSpan.FromSeconds(1d / targetFps);
    }

    public bool ShouldProcess(DateTime capturedAtUtc)
    {
        if (capturedAtUtc < _nextEligibleUtc)
        {
            return false;
        }

        _nextEligibleUtc = capturedAtUtc.Add(_interval);
        return true;
    }
}

public sealed class FrameSamplerFactory
{
    public FrameSampler Create(int targetFps) => new(targetFps);
}
