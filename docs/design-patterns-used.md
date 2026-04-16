# Design Patterns Used

## 1. Factory / Factory Method
- Where:
  - `src/Guardiao.Domain/Factories/DetectionEventFactory.cs`
  - `src/Guardiao.Domain/Factories/IncidentFactory.cs`
- Why:
  - Centralizes entity creation rules and guards required fields.
- Benefit:
  - Reduces duplicated creation logic and keeps validation close to domain concepts.

## 2. Adapter
- Where:
  - `src/Guardiao.Infrastructure/Repositories/InstitutionRepository.cs` (persistence adapter)
  - `src/Guardiao.Infrastructure/Adapters/NormalizedDetectionEventAdapter.cs` (integration adapter)
- Why:
  - Isolates technical protocols and persistence details from core business logic.
- Benefit:
  - Core layers remain independent from EF Core and external payload shapes.

## 3. Facade (Application Orchestration)
- Where:
  - `src/Guardiao.Application/Facades/EventIngestionFacade.cs`
- Why:
  - Provides a single orchestration entry for detection event ingestion flow.
- Benefit:
  - Keeps multi-step orchestration out of controllers and entities.

## 4. Strategy
- Where:
  - `src/Guardiao.Domain/Strategies/IIncidentRuleStrategy.cs`
  - `src/Guardiao.Domain/Strategies/SimpleIncidentRuleStrategy.cs`
- Why:
  - Supports replacing/expanding incident decision rules without changing orchestration.
- Benefit:
  - Enables incremental rule evolution while preserving open/closed principles.

## 5. Ports and Adapters (Hexagonal)
- Where:
  - Inbound port: `src/Guardiao.Application/Ports/Inbound/ICreateInstitutionUseCase.cs`
  - Outbound port: `src/Guardiao.Application/Ports/Outbound/IInstitutionRepositoryPort.cs`
  - Inbound adapter: `src/Guardiao.Api/Controllers/InstitutionsController.cs`
  - Outbound adapter: `src/Guardiao.Infrastructure/Repositories/InstitutionRepository.cs`
- Why:
  - Makes architecture explicit and enforces dependency direction.
- Benefit:
  - Better testability, lower coupling, easier replacement of infrastructure details.
