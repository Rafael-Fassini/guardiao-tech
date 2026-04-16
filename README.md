# Plataforma Guardiao / AEGIS Tech

Backend platform for institutional protection workflows (MVP), focused on safe handling of sensitive case data and operational incident support.

## Project Overview
- Stack: C# / ASP.NET Core (.NET 8)
- Database: PostgreSQL via EF Core
- Optional support (planned): Redis
- Scope: backend-first MVP
- Constraint: no video streaming or facial recognition inside backend core; backend receives normalized detection events from a future local gateway

## Adopted Architecture
Hexagonal Architecture was adopted to keep business rules independent from framework and infrastructure details, which is critical for a sensitive, evolving domain.

### Why Hexagonal Here
- Protects domain rules from HTTP/EF coupling.
- Simplifies testing by using ports and replacing adapters with fakes/mocks.
- Supports future edge gateway integration without changing core business logic.
- Fits MVP simplicity while preserving maintainability.

### Layer/Project Responsibilities
- `src/Guardiao.Domain` (Core): entities, factories, incident strategy, domain ports.
- `src/Guardiao.Application` (Use cases): inbound/outbound ports, use cases, DTOs, validation, orchestration facade.
- `src/Guardiao.Infrastructure` (Adapters): EF Core context, repository adapters, integration adapters.
- `src/Guardiao.Api` (Entry points): controllers, request contracts, middleware/DI wiring.

### Ports and Adapters (Concrete)
- Inbound port: `ICreateInstitutionUseCase` in `src/Guardiao.Application/Ports/Inbound`.
- Outbound port: `IInstitutionRepositoryPort` in `src/Guardiao.Application/Ports/Outbound`.
- Inbound adapter: `InstitutionsController` in `src/Guardiao.Api/Controllers`.
- Outbound adapter: `InstitutionRepository` in `src/Guardiao.Infrastructure/Repositories`.
- Domain integration port: `IDetectionEventAdapter` in `src/Guardiao.Domain/Ports`.
- Domain adapter implementation: `NormalizedDetectionEventAdapter` in `src/Guardiao.Infrastructure/Adapters`.

### How Data Enters and Flows
- HTTP requests enter through API controllers.
- Controllers map transport models to application commands and invoke use cases.
- Use cases depend on ports, not EF Core.
- Infrastructure adapters implement these ports and persist data via `GuardiaoDbContext`.
- External normalized detection events are designed to be converted by `IDetectionEventAdapter` implementations before domain/application rule orchestration.

## Current Folder Structure
```text
src/
  Guardiao.Api/
  Guardiao.Application/
  Guardiao.Domain/
  Guardiao.Infrastructure/
tests/
  Guardiao.UnitTests/
  Guardiao.IntegrationTests/
docs/
```

## Implemented Endpoints (Current)
- `POST /api/institutions`
- `GET /api/institutions/{id}` (placeholder response for MVP)
- `GET /health`

## Implemented Domain Components
- Entities: Institution, CameraSource, ProtectedPerson, RestrictedPerson, ProtectiveCase, DetectionEvent, Incident, AuditLog
- Factory: DetectionEventFactory, IncidentFactory
- Strategy: SimpleIncidentRuleStrategy
- Application Facade: EventIngestionFacade

## Tests
### Existing tests
- Unit tests:
  - `DetectionEventFactoryTests`: validates required camera source id.
  - `SimpleIncidentRuleStrategyTests`: validates incident creation decision rule.
  - `CreateInstitutionUseCaseTests`: validates use-case orchestration and input validation.
- Integration test:
  - `InstitutionsControllerIntegrationTests`: validates institution creation endpoint (`201 Created`) using in-memory EF Core.

### What tests validate
- Core domain rules still work after refactor.
- Application use case respects validation and uses outbound port.
- API remains functional after port/adapter boundary changes.

## Security Notes
- Input validation via DataAnnotations and application validation service.
- No hardcoded runtime secrets in code (connection string is environment-configurable).
- Architecture keeps sensitive business rules separated from transport/persistence details.
- Backend remains ready for auditable evolution (audit entity already present).

## Documentation for Professor Evidence
- Architecture details: `ARCHITECTURE.md`
- Screenshot evidence: `docs/architecture-evidence.md`
- Implementation status: `docs/current-implementation-status.md`
- Design patterns used: `docs/design-patterns-used.md`

## How to Run
### Prerequisites
- .NET 8 SDK
- PostgreSQL 15+

### Configure database
Update `src/Guardiao.Api/appsettings.Development.json` with your PostgreSQL credentials.

### Commands
```bash
dotnet restore
dotnet test
dotnet run --project src/Guardiao.Api
```

### Useful URLs
- Swagger: `http://localhost:5000/swagger`
- Health: `http://localhost:5000/health`
