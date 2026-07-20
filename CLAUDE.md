# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

Demo of 12 backend patterns from a .NET 8 driving-school system.
The group-join flow (submit → accept → payment → confirmed) threads through all of them.

## Commands

```bash
# Build
dotnet build ArchitecturePatterns.sln

# Run API (port 5000, Swagger at /swagger)
dotnet run --project src/API

# Tests
dotnet test tests/Integration.Tests   # integration — spins up a real Postgres container
dotnet test tests/Application.Tests   # unit
dotnet test tests/Architecture.Tests  # ArchUnit dependency rules

# Full stack
docker compose up
```

## Architecture

```
src/
  Domain/         # Entities, value objects, domain events — zero external deps
  Application/    # CQRS handlers (MediatR), pipeline behaviors, FluentValidation
  Infrastructure/ # EF Core, PostgreSQL, auth handlers, outbox processor
  API/            # Thin controllers, Swagger config, MVC conventions
tests/
  Domain.Tests/        # Domain logic unit tests
  Application.Tests/   # Handler unit tests
  Architecture.Tests/  # Enforce layer dependency rules
  Integration.Tests/   # HTTP-level tests against a real Postgres container (Testcontainers)
```

Dependency direction: `API → Application → Domain`, `Infrastructure → Application & Domain`.
Never import Infrastructure from Application or Domain.

## Key patterns — where to find them

| Pattern                         | Location                                                                                               |
| ------------------------------- | ------------------------------------------------------------------------------------------------------ |
| Result<T>                       | `src/Domain/Abstractions/Result.cs`                                                                    |
| Domain entity + factory         | `src/Domain/Aggregates/GroupJoinRequest/GroupJoinRequest.cs`                                           |
| MediatR pipeline behaviors      | `src/Application/Common/Behaviors/`                                                                    |
| IAuthorizedRequest              | `src/Application/Common/Authorization/`                                                                |
| Strongly-typed IDs              | `src/Domain/Ids/StronglyTypedIds.cs`                                                                   |
| Process Manager                 | `src/Domain/Aggregates/GroupJoinProcessManager/`                                                       |
| Transactional Outbox            | `src/Infrastructure/Outbox/` + `src/Infrastructure/Persistence/EF/DomainEventDispatcherInterceptor.cs` |
| EF Core interceptor chain       | `src/Infrastructure/Persistence/EF/`                                                                   |
| Global query filter + TPH guard | `src/Infrastructure/Persistence/EF/ModelBuilderExtensions.cs`                                          |
| MVC auth convention             | `src/API/AuthorizeByRequestConvention.cs`                                                              |
| Demo auth handler               | `src/Infrastructure/Identity/DemoAuthenticationHandler.cs`                                             |
| JWT auth handler                | `src/Infrastructure/Identity/JwtAuthenticationHandler.cs`                                              |
| Swagger auth metadata           | `src/API/Swagger/AuthorizationOperationFilter.cs`                                                      |

## Conventions

- **No exceptions for business failures** — return `Result` / `Result<T>`, never throw from handlers.
- **Domain events on aggregates** — call `RaiseDomainEvent()` inside entity methods; the interceptor picks them up on `SaveChanges`.
- **Authorization on commands** — `IAuthorizedRequest.RequiredPermission` declares the required JWT claim; `AuthorizationBehavior` enforces it. Keep controllers annotation-light.
- **New entity** — needs a `private` parameterless constructor for EF Core and a `static Create()` factory.
- **New command/query** — add `IAuthorizedRequest` if it requires auth; add a `IValidator<TCommand>` for input validation.
- **Permission strings** — add to `src/Application/Common/Authorization/Permissions.cs`, not inline.

## Auth in demo mode

`Auth:Authority` is empty in `docker-compose.yml` → `DemoAuthenticationHandler` is registered.
Pass permission claim names as the Bearer token value, comma-separated:

```
Authorization: Bearer joinrequests:instructor,joinrequests:read
```

No token → 401. Wrong claims → 403 (enforced by `AuthorizationBehavior` in the MediatR pipeline).

## Database

PostgreSQL on port 65432 (docker-compose). Connection string in `docker-compose.yml`.
EF Core code-first migrations in `src/Infrastructure/Persistence/Migrations/`.

```bash
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/API
dotnet ef database update          --project src/Infrastructure --startup-project src/API
```
