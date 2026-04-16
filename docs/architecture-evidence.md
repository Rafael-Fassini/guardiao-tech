# Architecture Evidence

**HEXAGONAL ARCHITECTURE**

## Current Folder Tree
```text
src/
  Guardiao.Api/
    Contracts/
    Controllers/
    Program.cs
  Guardiao.Application/
    DTOs/
    Ports/
      Inbound/
      Outbound/
    Services/
    UseCases/
    Facades/
  Guardiao.Domain/
    Entities/
    Factories/
    Strategies/
    Ports/
  Guardiao.Infrastructure/
    Persistence/
    Repositories/
    Adapters/
tests/
  Guardiao.UnitTests/
    Application/
    Domain/
  Guardiao.IntegrationTests/
    Api/
```

## Objective Proof Points
- `ICreateInstitutionUseCase` (`src/Guardiao.Application/Ports/Inbound/ICreateInstitutionUseCase.cs`) is an inbound port used by the API.
- `CreateInstitutionUseCase` (`src/Guardiao.Application/UseCases/CreateInstitutionUseCase.cs`) orchestrates validation and persistence without ASP.NET/EF references.
- `IInstitutionRepositoryPort` (`src/Guardiao.Application/Ports/Outbound/IInstitutionRepositoryPort.cs`) is an outbound port owned by Application.
- `InstitutionRepository` (`src/Guardiao.Infrastructure/Repositories/InstitutionRepository.cs`) is an infrastructure adapter implementing the outbound persistence port.
- `InstitutionsController` (`src/Guardiao.Api/Controllers/InstitutionsController.cs`) is only an HTTP entry point; it maps request to command and calls use case.
- `Institution` entity (`src/Guardiao.Domain/Entities/Institution.cs`) has no dependency on infrastructure/framework packages.
- `IDetectionEventAdapter` (`src/Guardiao.Domain/Ports/IDetectionEventAdapter.cs`) is a domain integration port for normalized detection payloads.
- `NormalizedDetectionEventAdapter` (`src/Guardiao.Infrastructure/Adapters/NormalizedDetectionEventAdapter.cs`) is a concrete adapter that converts normalized payload into a domain `DetectionEvent`.
- DI in `Program.cs` (`src/Guardiao.Api/Program.cs`) wires ports to adapters explicitly (`ICreateInstitutionUseCase -> CreateInstitutionUseCase`, `IInstitutionRepositoryPort -> InstitutionRepository`).

## What To Screenshot
- Folder tree above.
- Port files in `Guardiao.Application/Ports` and `Guardiao.Domain/Ports`.
- Adapter files in `Guardiao.Infrastructure/Repositories` and `Guardiao.Infrastructure/Adapters`.
- `InstitutionsController` showing HTTP-only behavior.
- `Program.cs` showing port-to-adapter DI wiring.
