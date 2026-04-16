using Guardiao.Domain.Factories;
using Xunit;

namespace Guardiao.UnitTests.Domain;

public class DetectionEventFactoryTests
{
    [Fact]
    public void Create_ShouldThrow_WhenCameraSourceIdIsEmpty()
    {
        var factory = new DetectionEventFactory();

        Assert.Throws<ArgumentException>(() =>
            factory.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Create_ShouldReturnEntity_WhenInputIsValid()
    {
        var factory = new DetectionEventFactory();
        var cameraSourceId = Guid.NewGuid();

        var result = factory.Create(cameraSourceId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(cameraSourceId, result.CameraSourceId);
    }
}
