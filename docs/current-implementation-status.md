# Current Implementation Status

## 1. What Already Existed Before These Changes
- Solution with 4 projects: API, Application, Domain, Infrastructure.
- Core entities for institutions, cameras, protected/restricted people, protective cases, detection events, incidents, audit logs.
- Domain factory classes (`DetectionEventFactory`, `IncidentFactory`) and incident strategy (`SimpleIncidentRuleStrategy`).
- Application orchestration class `EventIngestionFacade` and basic `ValidationService`.
- Infrastructure `GuardiaoDbContext` and institution persistence class.
- API endpoint for institution creation and health route.
- Empty unit/integration test projects (no test classes implemented).

## 2. What Was Changed
- Refactored institution creation to explicit hexagonal flow:
  - Added inbound port `ICreateInstitutionUseCase`.
  - Added outbound port `IInstitutionRepositoryPort`.
  - Added use case `CreateInstitutionUseCase`.
  - Updated `InstitutionsController` to call use case instead of infrastructure repository directly.
  - Updated DI in `Program.cs` to wire ports and adapters.
- Moved domain adapter contract to explicit domain port location (`Guardiao.Domain/Ports`).
- Added infrastructure adapter `NormalizedDetectionEventAdapter`.
- Strengthened input validation with max-length checks and API DataAnnotations.
- Added automated tests (unit + integration).
- Added architecture evidence and status documentation files.

## 3. What Is Implemented Now
- Hexagonal institution creation flow end-to-end (HTTP -> use case -> persistence adapter).
- Domain business components:
  - Entity model for MVP scope.
  - Incident rule strategy.
  - Domain factories.
- Infrastructure persistence adapter with EF Core/PostgreSQL support.
- Basic normalized detection event adapter ready for edge-gateway integration.
- Automated test coverage for key domain/app rules and one API integration flow.

## 4. What Is Still Missing
- Full CRUD/API coverage for cameras, victims, aggressors, protective cases, detections, incidents.
- Persistence adapters for non-institution aggregates.
- Audit persistence/use case wiring across all critical operations.
- Authentication/authorization and role-based access controls.
- Production-ready exception middleware and privacy-aware structured logging.
- Real gateway endpoint for normalized detection event ingestion.

## 5. Planned Next Steps
- Add use cases and ports for the remaining aggregates.
- Implement detection-event ingestion endpoint that uses adapter + facade + repositories.
- Expand integration tests for key operational flows and error scenarios.
- Add authn/authz and secure operational audit flows.
- Introduce migrations and deployment checklists for consistent environments.

## Implemented So Far
### Features
- Create institution (`POST /api/institutions`).
- Health check (`GET /health`).

### Components
- Use case: `CreateInstitutionUseCase`.
- Inbound port: `ICreateInstitutionUseCase`.
- Outbound port: `IInstitutionRepositoryPort`.
- Outbound adapter: `InstitutionRepository`.
- Domain port: `IDetectionEventAdapter`.
- Domain/application business logic: factories, strategy, facade, validation service.

### Tests
- Unit: detection event factory validation.
- Unit: incident strategy decision rule.
- Unit: institution use case orchestration/validation.
- Integration: institution creation endpoint with in-memory EF Core database.
