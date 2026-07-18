# dotnet-architecture-patterns

Ten backend patterns from a production .NET 8 driving-school management system.
The **group-join flow** (student requests → instructor accepts → payment → confirmed)
threads through all of them end-to-end.

Stack: **.NET 8 · ASP.NET Core · EF Core 8 · MediatR · FluentValidation · PostgreSQL · Vue 3**

---

## Table of Contents

1. [Result Pattern](#1-result-pattern)
2. [Domain Entity with Factory Method](#2-domain-entity-with-factory-method)
3. [CQRS + MediatR Pipeline](#3-cqrs--mediatr-pipeline)
4. [DomainException Behavior](#4-domainexception-behavior)
5. [Strongly-Typed IDs](#5-strongly-typed-ids)
6. [Process Manager](#6-process-manager)
7. [Transactional Outbox](#7-transactional-outbox)
8. [EF Core Interceptor Chain](#8-ef-core-interceptor-chain)
9. [Global Query Filter with TPH Guard](#9-global-query-filter-with-tph-guard)
10. [Zero-Attribute Authorization Convention](#10-zero-attribute-authorization-convention)

---

## 1. Result Pattern

No exceptions for business failures — every operation returns `Result` or `Result<T>`.

```csharp
public async Task<Result> Handle(Command request, CancellationToken ct)
{
    var joinRequest = await _db.GroupJoinRequests.FindAsync(request.RequestId, ct);
    if (joinRequest is null)
        return Result.Failure("Join request not found");   // ← not NotFoundException

    joinRequest.Accept(instructorId, request.AgreedPrice, request.AgreedCurrency);
    await _db.SaveChangesAsync(ct);
    return Result.Success();
}
```

Controllers map `Result` to HTTP via `ResultExtensions.ToActionResult()`.

> **File:** [`src/Domain/Abstractions/Result.cs`](src/Domain/Abstractions/Result.cs)

---

## 2. Domain Entity with Factory Method

All aggregates have a `private` EF Core constructor and a `public static Create()` factory
that raises domain events.

```csharp
public class GroupJoinRequest : AuditableEntity<GroupJoinRequestId>
{
    private GroupJoinRequest() : base(default) { }    // EF Core — never called in business code

    public static GroupJoinRequest Create(StudentId studentId, GroupId groupId)
    {
        var req = new GroupJoinRequest(new GroupJoinRequestId(Guid.NewGuid()), studentId, groupId);
        req.RaiseDomainEvent(new GroupJoinRequestSubmitted(req.Id, studentId, groupId));
        return req;
    }

    public void Accept(InstructorId by, decimal price, string currency)
    {
        if (Status != JoinRequestStatus.PendingApproval)
            throw new DomainException("Only a pending request can be accepted.");

        Status = JoinRequestStatus.PendingPayment;
        RaiseDomainEvent(new GroupJoinRequestAccepted(Id, StudentId, GroupId));
    }
}
```

Domain events accumulate in memory on `Entity` base class, then the
`DomainEventDispatcherInterceptor` writes them to the Outbox atomically on `SaveChanges`.

> **Files:**
> [`src/Domain/Aggregates/GroupJoinRequest/GroupJoinRequest.cs`](src/Domain/Aggregates/GroupJoinRequest/GroupJoinRequest.cs)
> · [`src/Domain/Abstractions/Entity.cs`](src/Domain/Abstractions/Entity.cs)

---

## 3. CQRS + MediatR Pipeline

Each operation is a `record Command` / `record Query`. Cross-cutting concerns are pipeline
behaviors — handlers contain only coordination logic.

```
Request
  │
  ▼
AuthorizationBehavior   ← checks ICurrentUser + IPermissionService
  │
  ▼
ValidationBehavior      ← runs all FluentValidation IValidator<TRequest>
  │
  ▼
DomainExceptionBehavior ← catches DomainException → Result.Failure (see §4)
  │
  ▼
Handler                 ← pure coordination; domain rules live in aggregates
```

Authorization is declared **on the command itself**, not on the controller:

```csharp
public record Command(GroupJoinRequestId RequestId, decimal AgreedPrice, string Currency)
    : IRequest<Result>, IAuthorizedRequest
{
    public string? RequiredPermission => Permissions.JoinRequests.InstructorWrite;
}
```

> **Files:**
> [`src/Application/Common/Behaviors/AuthorizationBehavior.cs`](src/Application/Common/Behaviors/AuthorizationBehavior.cs)
> · [`src/Application/Common/Behaviors/ValidationBehavior.cs`](src/Application/Common/Behaviors/ValidationBehavior.cs)
> · [`src/Application/GroupJoinRequests/AcceptGroupJoinRequest/`](src/Application/GroupJoinRequests/AcceptGroupJoinRequest/)

---

## 4. DomainException Behavior

Aggregates throw `DomainException` for invariant violations. The pipeline behavior
converts these to `Result.Failure()` — handlers never need try/catch.

```csharp
public sealed class DomainExceptionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        try
        {
            return await next();
        }
        catch (DomainException ex)
        {
            // Works for both Result and Result<T> via reflection —
            // avoids duplicating the catch block per generic type argument.
            if (typeof(TResponse) == typeof(Result))
                return (TResponse)(object)Result.Failure(ex.Message);

            if (typeof(TResponse).IsGenericType &&
                typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var fail = typeof(Result<>)
                    .MakeGenericType(typeof(TResponse).GenericTypeArguments[0])
                    .GetMethod(nameof(Result.Failure))!;
                return (TResponse)fail.Invoke(null, [ex.Message])!;
            }

            throw;
        }
    }
}
```

> **File:** [`src/Application/Common/Behaviors/DomainExceptionBehavior.cs`](src/Application/Common/Behaviors/DomainExceptionBehavior.cs)

---

## 5. Strongly-Typed IDs

Every entity gets its own ID type via the [`StronglyTypedId`](https://github.com/andrewlock/StronglyTypedId)
source generator. Passing a `StudentId` where a `GroupId` is expected is a compile error.

```csharp
[StronglyTypedId] public partial struct GroupJoinRequestId;
[StronglyTypedId] public partial struct StudentId;
[StronglyTypedId] public partial struct GroupId;
```

The generator produces EF Core value converters, JSON serialization, and a Swagger schema
filter that renders each ID as `uuid` in the OpenAPI spec. On the frontend,
`npm run openapi:generate` regenerates TypeScript types from that spec automatically.

> **File:** [`src/Domain/Ids/StronglyTypedIds.cs`](src/Domain/Ids/StronglyTypedIds.cs)

---

## 6. Process Manager

The group-join flow spans three aggregates (`GroupJoinRequest`, `Payment`, `Student`).
A `GroupJoinProcessManager` tracks the cross-aggregate state machine so orchestration
logic doesn't pollute any single aggregate.

```
GroupJoinRequestAccepted  →  creates Payment + PM (AwaitingPayment)
PaymentCompleted          →  PM.MarkCompleted()
                             joinRequest.Confirm()
                             student.AssignToGroup()
GroupJoinRequestRejected  →  PM.StartCompensation()
                             payment.Cancel() or RequestRefund()
```

State transitions are enforced by the PM itself:

```csharp
public void StartAwaitingPayment(PaymentId paymentId)
{
    if (State != ProcessManagerState.Created)
        throw new InvalidOperationException("PM must be in Created state.");

    PaymentId = paymentId;
    State     = ProcessManagerState.AwaitingPayment;
}
```

> **Files:**
> [`src/Domain/Aggregates/GroupJoinProcessManager/GroupJoinProcessManager.cs`](src/Domain/Aggregates/GroupJoinProcessManager/GroupJoinProcessManager.cs)
> · [`src/Application/GroupJoinProcessManagers/Notifications/OnPaymentCompleted.cs`](src/Application/GroupJoinProcessManagers/Notifications/OnPaymentCompleted.cs)

---

## 7. Transactional Outbox

Domain events survive a crash between commit and publish. Two components:

**Write path** — `DomainEventDispatcherInterceptor` (EF Core `SaveChangesInterceptor`)
serialises events to `OutboxMessage` rows in the **same transaction**:

```csharp
var domainEvents = context.ChangeTracker
    .Entries<Entity>()
    .SelectMany(e => e.Entity.PopDomainEvents())
    .ToList();

var messages = domainEvents.Select(e => new OutboxMessage {
    Id        = Guid.NewGuid(),
    EventType = e.GetType().AssemblyQualifiedName!,
    Payload   = JsonConvert.SerializeObject(e),
    CreatedAt = DateTime.UtcNow,
});

context.Set<OutboxMessage>().AddRange(messages);
// base.SavingChangesAsync() → both business data and outbox messages commit atomically
```

**Read path** — `OutboxProcessor` (`BackgroundService`, every 10 s):

```csharp
var messages = await db.OutboxMessages
    .Where(m => m.SentAt == null && m.RetryCount < MaxRetries)
    .OrderBy(m => m.CreatedAt)
    .Take(BatchSize)
    .ToListAsync(ct);

foreach (var message in messages)
{
    var eventType   = Type.GetType(message.EventType);
    var domainEvent = (IDomainEvent)JsonConvert.DeserializeObject(message.Payload, eventType)!;
    await publisher.Publish(domainEvent, ct);    // → INotificationHandler<T>
    message.SentAt = DateTime.UtcNow;
}
```

> **Files:**
> [`src/Infrastructure/Persistence/EF/DomainEventDispatcherInterceptor.cs`](src/Infrastructure/Persistence/EF/DomainEventDispatcherInterceptor.cs)
> · [`src/Infrastructure/Outbox/OutboxProcessor.cs`](src/Infrastructure/Outbox/OutboxProcessor.cs)

---

## 8. EF Core Interceptor Chain

Four `SaveChangesInterceptor` implementations, each with a single responsibility,
composing via `base.SavingChangesAsync()`:

```
SaveChanges called
  │
  ├─ SoftDeleteInterceptor    intercepts EntityState.Deleted
  │                           → calls MarkDeleted() + sets state to Modified
  │                           (domain entity has zero EF knowledge)
  │
  ├─ AuditInterceptor         stamps CreatedAt / UpdatedAt from ICurrentUser
  │                           skips soft-deleted entries (already stamped by MarkDeleted)
  │
  ├─ DomainEventDispatcherInterceptor  pops domain events → OutboxMessage rows
  │
  └─ DateTimeInterceptor      normalises DateOnly / TimeOnly UTC offsets
```

```csharp
// SoftDeleteInterceptor — the redirect pattern
private void Apply(DbContext context)
{
    foreach (var entry in context.ChangeTracker.Entries())
    {
        if (entry.State != EntityState.Deleted) continue;
        if (entry.Entity is not AuditableEntity entity) continue;

        entity.MarkDeleted(DateTime.UtcNow);
        entry.State = EntityState.Modified;   // ← no DELETE SQL is emitted
    }
}
```

> **Files:**
> [`src/Infrastructure/Persistence/EF/SoftDeleteInterceptor.cs`](src/Infrastructure/Persistence/EF/SoftDeleteInterceptor.cs)
> · [`src/Infrastructure/Persistence/EF/AuditInterceptor.cs`](src/Infrastructure/Persistence/EF/AuditInterceptor.cs)

---

## 9. Global Query Filter with TPH Guard

A generic helper applies the soft-delete filter (`!e.IsDeleted`) to every entity type
inheriting from `AuditableEntity` — without listing each type manually.

The non-obvious detail: EF Core only allows `HasQueryFilter` on the **root** of a
Table-per-Hierarchy (TPH) hierarchy. The guard `entityType.BaseType != null` skips
derived types, preventing a runtime exception.

```csharp
public static void ApplyGlobalFilter<TBase>(
    this ModelBuilder             modelBuilder,
    Expression<Func<TBase, bool>> filter)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (!typeof(TBase).IsAssignableFrom(entityType.ClrType)) continue;

        // TPH: filter may only be set on the root type.
        if (entityType.BaseType != null) continue;

        var parameter = Expression.Parameter(entityType.ClrType);
        var body      = ReplacingExpressionVisitor.Replace(
            filter.Parameters.Single(), parameter, filter.Body);

        modelBuilder
            .Entity(entityType.ClrType)
            .HasQueryFilter(Expression.Lambda(body, parameter));
    }
}

// Usage in OnModelCreating:
// modelBuilder.ApplyGlobalFilter<AuditableEntity>(e => !e.IsDeleted);
```

> **File:** [`src/Infrastructure/Persistence/EF/ModelBuilderExtensions.cs`](src/Infrastructure/Persistence/EF/ModelBuilderExtensions.cs)

---

## 10. Zero-Attribute Authorization Convention

An `IActionModelConvention` automatically applies `[Authorize]` to any controller action
whose bound parameter implements `IAuthorizedRequest`. Result: **zero `[Authorize]` attributes**
in the codebase. Authorization intent lives on the command record.

```csharp
public class AuthorizeByRequestConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        var requiresAuth = action.Parameters.Any(p =>
            typeof(IAuthorizedRequest).IsAssignableFrom(p.ParameterType));

        if (requiresAuth)
            action.Filters.Add(new AuthorizeFilter());
    }
}

// Registered once in Program.cs:
// builder.Services.AddControllers(o =>
//     o.Conventions.Add(new AuthorizeByRequestConvention()));
```

> **File:** [`src/API/AuthorizeByRequestConvention.cs`](src/API/AuthorizeByRequestConvention.cs)

---

## End-to-end: group-join flow

```
Student submits request
  → GroupJoinRequest.Create()             [§2] factory raises GroupJoinRequestSubmitted
  → SaveChanges                           [§7] event written to outbox atomically
                                          [§8] AuditInterceptor stamps CreatedAt

Instructor accepts
  → AcceptGroupJoinRequest.Command        [§3] IAuthorizedRequest → permission check
  → AuthorizationBehavior                 [§3] verifies joinrequests:instructor claim
  → ValidationBehavior                    [§3] FluentValidation runs
  → Handler calls joinRequest.Accept()    [§2] state transition + raises event
  → DomainExceptionBehavior               [§4] catches guard violations → Result.Failure
  → SaveChanges                           [§7] event written to outbox
                                          [§8] AuditInterceptor stamps UpdatedAt

OnGroupJoinRequestAccepted (Outbox → MediatR)
  → creates Payment                       [§5] PaymentId is a strongly-typed ID
  → creates GroupJoinProcessManager       [§6] PM enters AwaitingPayment state

Student pays
  → Payment.Complete() raises PaymentCompleted
  → SaveChanges                           [§7] outbox

OnPaymentCompleted (Outbox → MediatR)
  → pm.MarkCompleted()                    [§6] PM enforces valid state transition
  → joinRequest.Confirm()
  → student.AssignToGroup()
  → single SaveChanges                    atomic — all three aggregates in one tx
```

---

## Running locally

```bash
# Full stack (PostgreSQL + API + frontend)
docker compose up

# API only (port 5000)
dotnet run --project src/API

# Frontend dev server (port 5173, proxies /api to :5000)
cd frontend && npm install && npm run dev
```

After changing a C# DTO or adding an endpoint, regenerate the TypeScript types:

```bash
cd frontend
npm run openapi:generate   # reads openapi.json → overwrites src/api/schemas.ts
```

---

## Frontend type-safety flow

```
C# record (GroupJoinRequestDto)
  │  Swagger / Swashbuckle
  ▼
openapi.json  ←  committed snapshot, updated when API contract changes
  │  npm run openapi:generate  (openapi-typescript)
  ▼
src/api/schemas.ts  ←  READ ONLY — never edit by hand
  │  import type { components }
  ▼
groupJoinRequestsService.ts  →  groupJoinRequestsStore.ts  →  GroupJoinRequestCard.vue
```

If a C# property is renamed, `openapi:generate` updates `schemas.ts`, and every
TypeScript call-site that used the old name becomes a **compile error** — no
runtime surprises.

> **Files:**
> [`frontend/src/api/schemas.ts`](frontend/src/api/schemas.ts)
> · [`frontend/src/services/groupJoinRequestsService.ts`](frontend/src/services/groupJoinRequestsService.ts)
> · [`frontend/src/stores/groupJoinRequestsStore.ts`](frontend/src/stores/groupJoinRequestsStore.ts)
> · [`frontend/src/components/GroupJoinRequestCard.vue`](frontend/src/components/GroupJoinRequestCard.vue)

---

## Stack

| Layer          | Technology                                            |
| -------------- | ----------------------------------------------------- |
| API            | ASP.NET Core 8                                        |
| Application    | MediatR 12, FluentValidation 11                       |
| Domain         | Plain C#, strongly-typed IDs (readonly record struct) |
| Infrastructure | EF Core 8, PostgreSQL, Newtonsoft.Json                |
| Frontend       | Vue 3, TypeScript, Pinia, openapi-typescript          |
| DevOps         | Docker Compose (postgres + api + frontend)            |
