# Plataforma Guardião / AEGIS Tech

## Project Overview
A backend platform for proactive protection against violence and feminicide in institutional environments. It manages institutions, cameras, victims, aggressors, protective cases, detection events, incidents, and audit logs, and is ready for future integration with local camera/AI connectors.

## Problem Statement
Institutions need a robust, auditable, and extensible backend to manage protective cases and respond to critical detection events, supporting operational safety and compliance.

## Why C#
C# and ASP.NET Core provide strong domain modeling, auditability, API robustness, security, and maintainability, ideal for critical institutional systems.

## Why Video Is Not Handled Directly
Video and facial recognition are handled by a future local connector/edge AI. The backend receives only normalized detection events, ensuring scalability, privacy, and separation of concerns.

## MVP Scope
- Register institutions, cameras, victims, aggressors, cases
- Link victims/aggressors to cases
- Receive detection events
- Evaluate simple incident rule
- Create incidents
- List incidents
- Health check

## Architecture Overview
- Modular monolith, clean/layered architecture
- Projects: Domain, Application, Infrastructure, API
- PostgreSQL (EF Core), Redis-ready
- Design patterns: Factory, Adapter, Facade, Strategy

## Folder Structure
```
/src
  /Guardiao.Api
  /Guardiao.Application
  /Guardiao.Domain
  /Guardiao.Infrastructure
/tests
  /Guardiao.UnitTests
  /Guardiao.IntegrationTests
```

## Main Entities
- Institution
- CameraSource
- ProtectedPerson
- RestrictedPerson
- ProtectiveCase
- DetectionEvent
- Incident
- AuditLog

## Implemented Design Patterns
- **Factory**: Creation of DetectionEvent and Incident (with validation)
- **Adapter**: Converts external detection event payloads to internal model
- **Facade**: Orchestrates event ingestion, rule evaluation, incident creation, audit
- **Strategy**: Incident rule evaluation (simple rule, extensible)

## API Overview
- POST /api/institutions
- POST /api/cameras
- POST /api/victims
- POST /api/aggressors
- POST /api/cases
- POST /api/events/detections
- GET /api/incidents
- GET /health

## Database Overview
- PostgreSQL, managed via EF Core
- Initial schema covers all MVP entities

## Testing Strategy
- Unit tests for factories, strategies, services
- Integration tests for API and persistence
- xUnit and Moq

## Security Considerations
- No secrets in code
- Input validation on all requests
- DTO boundaries
- Error handling without leaking internals
- Audit trail foundation
- Role-ready structure for future auth
- Health check endpoint

## Current Limitations
- No authentication/authorization yet
- No Docker
- No real-time event streaming
- No video/facial recognition in backend
- Only MVP endpoints/entities

## Next Steps / Roadmap
- Implement authentication/authorization
- Add Redis for caching/event support
- Expand incident rules and notification flows
- Integrate with edge AI connector
- Add more endpoints and domain logic

## How to Run Locally

### Prerequisites
- .NET 8 SDK
- PostgreSQL 15+
- Redis (for future use)

### Database Setup
1. Create a PostgreSQL database and user:
   ```sql
   CREATE DATABASE guardiao_db;
   CREATE USER guardiao_user WITH PASSWORD 'your_password_here';
   GRANT ALL PRIVILEGES ON DATABASE guardiao_db TO guardiao_user;
   ```
2. Update `appsettings.Development.json` with your credentials.

### Restore Packages
```
dotnet restore
```

### Run EF Core Migrations
```
dotnet ef migrations add InitialCreate -p src/Guardiao.Infrastructure -s src/Guardiao.Api
```
```
dotnet ef database update -p src/Guardiao.Infrastructure -s src/Guardiao.Api
```

### Start the API
```
dotnet run --project src/Guardiao.Api
```

### Run Tests
```
dotnet test
```

### Example Requests
- Create Institution:
  ```bash
  curl -X POST http://localhost:5000/api/institutions -H "Content-Type: application/json" -d '{"name":"Univ Example","address":"123 Main St"}'
  ```
- Health Check:
  ```bash
  curl http://localhost:5000/health
  ```

### Expected Local URLs
- Swagger: http://localhost:5000/swagger
- Health: http://localhost:5000/health

### Common Issues
- Connection errors: check DB credentials and network
- Migration errors: ensure DB is created and user has privileges
- Port conflicts: change launch settings or use `--urls`

---
