using Guardiao.Application.DTOs;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.UseCases;
using Guardiao.Domain.Entities;
using Moq;
using Xunit;

namespace Guardiao.UnitTests.Application;

public class CreateInstitutionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldPersistAndReturnDto_WhenInputIsValid()
    {
        var repository = new Mock<IInstitutionRepositoryPort>();
        Institution? persisted = null;

        repository
            .Setup(x => x.AddAsync(It.IsAny<Institution>()))
            .Callback<Institution>(entity => persisted = entity)
            .ReturnsAsync((Institution entity) => entity);

        var useCase = new CreateInstitutionUseCase(repository.Object);
        var command = new CreateInstitutionCommand("Campus A", "Street 1");

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(persisted);
        Assert.Equal("Campus A", result.Name);
        Assert.Equal("Street 1", result.Address);
        repository.Verify(x => x.AddAsync(It.IsAny<Institution>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_WhenNameIsEmpty()
    {
        var repository = new Mock<IInstitutionRepositoryPort>();
        var useCase = new CreateInstitutionUseCase(repository.Object);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await useCase.ExecuteAsync(new CreateInstitutionCommand("", "Address")));
    }
}
