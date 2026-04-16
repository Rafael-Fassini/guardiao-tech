# Plataforma Guardiao - Hexagonal Architecture

## Chosen Architecture
**Hexagonal Architecture (Ports and Adapters)**

The backend is organized so business rules stay in `Domain` and application orchestration stays in `Application`, while technical details (HTTP, EF Core/PostgreSQL) are implemented as adapters in `Api` and `Infrastructure`.

## Layer Responsibilities

### Domain (Core)
- Location: `src/Guardiao.Domain`
- Contains entities (`Institution`, `ProtectiveCase`, `DetectionEvent`, `Incident`, etc.), domain factories, and strategy rules.
- Contains domain port `IDetectionEventAdapter` for normalized detection payload conversion.
- Has no dependency on EF Core, ASP.NET Core, or API contracts.

### Application
- Location: `src/Guardiao.Application`
- Contains use cases, DTOs, validation, and orchestration (`EventIngestionFacade`).
- Inbound port example: `ICreateInstitutionUseCase`.
- Outbound port example: `IInstitutionRepositoryPort`.
- Depends on `Domain` only.

### Infrastructure (Outbound Adapters)
- Location: `src/Guardiao.Infrastructure`
- Contains EF Core `GuardiaoDbContext` and repository/adapters.
- Adapter example: `InstitutionRepository` implements `IInstitutionRepositoryPort`.
- Adapter example: `NormalizedDetectionEventAdapter` implements `IDetectionEventAdapter`.

### API (Inbound Adapter)
- Location: `src/Guardiao.Api`
- Contains controllers, transport contracts, and DI wiring.
- `InstitutionsController` only maps HTTP request to application command and calls use case.

## Ports and Adapters Mapping
- Inbound port: `src/Guardiao.Application/Ports/Inbound/ICreateInstitutionUseCase.cs`
- Inbound adapter: `src/Guardiao.Api/Controllers/InstitutionsController.cs`
- Outbound port: `src/Guardiao.Application/Ports/Outbound/IInstitutionRepositoryPort.cs`
- Outbound adapter: `src/Guardiao.Infrastructure/Repositories/InstitutionRepository.cs`
- Domain integration port: `src/Guardiao.Domain/Ports/IDetectionEventAdapter.cs`
- Domain adapter implementation: `src/Guardiao.Infrastructure/Adapters/NormalizedDetectionEventAdapter.cs`

## Dependency Direction
Only inward dependency is allowed:

`Api -> Application -> Domain`

`Infrastructure -> Application + Domain`

`Domain -> (no infrastructure/framework dependencies)`

## Why This Matches Guardiao
- The platform handles sensitive operational data and needs clear boundaries.
- Hexagonal architecture keeps critical rules independent from HTTP/DB details.
- It supports future edge AI integration by receiving normalized events via adapters, without putting video/recognition logic in the core.
- It keeps MVP complexity low while preserving maintainability and testability.

## External Detection Events Flow (Current Design)
1. A future edge gateway sends normalized payload to an API endpoint.
2. API maps request to application DTO/use case.
3. `IDetectionEventAdapter` implementation (`NormalizedDetectionEventAdapter`) converts payload into `DetectionEvent` entity.
4. `EventIngestionFacade` orchestrates rule evaluation and incident creation strategy.
5. Persistence adapters in infrastructure store events/incidents and audit records.

This keeps video streaming and facial recognition outside the backend core, as required.
