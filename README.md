# dotnet-architecture-patterns

Twelve backend patterns from a production .NET 8 driving-school management system.
The **group-join flow** (student requests → instructor accepts → payment → confirmed)
threads through all of them end-to-end.

Stack: **.NET 8 · ASP.NET Core · EF Core 8 · MediatR · FluentValidation · PostgreSQL**

---

## Try it

```bash
docker compose up          # PostgreSQL + API on :5000
```

Swagger UI at **http://localhost:5000/swagger**

To explore authorization, click **Authorize** and enter a comma-separated list of
permission claim names as the token value (no real OIDC server required):

| Token value               | What it unlocks                              |
| ------------------------- | -------------------------------------------- |
| `joinrequests:read`       | GET `/{id}`                                  |
| `joinrequests:student`    | POST `/` (submit)                            |
| `joinrequests:instructor` | GET `/` (pending list) · POST `/{id}/accept` |

No token → **401**. Wrong permission → **403**. The full auth pipeline runs end-to-end.

---

## Patterns

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
11. [Dual Authentication Handlers](#11-dual-authentication-handlers)
12. [Swagger Authorization Metadata](#12-swagger-authorization-metadata)

---

## 1. Result Pattern

No exceptions for business failures — every operation returns `Result` or `Result<T>`.

```csharp
public async Task<Result> Handle(Command request, CancellationToken ct)
{
    var joinRequest = await _db.GroupJoinRequests.FindAsync(request.RequestId, ct);
    if (joinRequest is null)
        return Result.NotFound();           // ← not NotFoundException

    joinRequest.Accept(instructorId, request.AgreedPrice, request.AgreedCurrency);
    await _db.SaveChangesAsync(ct);
    return Result.Success();
}
```

Controllers map `Result` to HTTP status codes directly — no middleware magic needed.

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

Domain events accumulate in memory on the `Entity` base class, then
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
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        try { return await next(); }
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

The generator produces EF Core value converters and JSON serialization automatically.

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

**Write path** — `DomainEventDispatcherInterceptor` serialises events to `OutboxMessage`
rows in the **same transaction**:

```csharp
var domainEvents = context.ChangeTracker
    .Entries<Entity>()
    .SelectMany(e => e.Entity.PopDomainEvents())
    .ToList();

context.Set<OutboxMessage>().AddRange(domainEvents.Select(e => new OutboxMessage {
    Id        = Guid.NewGuid(),
    EventType = e.GetType().AssemblyQualifiedName!,
    Payload   = JsonConvert.SerializeObject(e),
    CreatedAt = DateTime.UtcNow,
}));
// base.SavingChangesAsync() → business data + outbox rows commit atomically
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
    await publisher.Publish(domainEvent, ct);   // → INotificationHandler<T>
    message.SentAt = DateTime.UtcNow;
}
```

> **Files:**
> [`src/Infrastructure/Persistence/EF/DomainEventDispatcherInterceptor.cs`](src/Infrastructure/Persistence/EF/DomainEventDispatcherInterceptor.cs)
> · [`src/Infrastructure/Outbox/OutboxProcessor.cs`](src/Infrastructure/Outbox/OutboxProcessor.cs)

---

## 8. EF Core Interceptor Chain

Four `SaveChangesInterceptor` implementations compose via `base.SavingChangesAsync()`,
each with a single responsibility:

```
SaveChanges called
  │
  ├─ SoftDeleteInterceptor         EntityState.Deleted → calls MarkDeleted() + Modified
  │                                domain entity has zero EF knowledge
  ├─ AuditInterceptor              stamps CreatedAt / UpdatedAt
  ├─ DateTimeInterceptor           normalises DateTimeKind.Unspecified → UTC (PostgreSQL safety)
  └─ DomainEventDispatcherInterceptor  pops domain events → OutboxMessage rows
```

```csharp
// SoftDeleteInterceptor — redirect pattern: no DELETE SQL emitted
private static void Apply(DbContext context)
{
    foreach (var entry in context.ChangeTracker.Entries())
    {
        if (entry.State != EntityState.Deleted) continue;
        if (entry.Entity is not AuditableEntity entity) continue;

        entity.MarkDeleted(DateTime.UtcNow);
        entry.State = EntityState.Modified;
    }
}
```

> **Files:**
> [`src/Infrastructure/Persistence/EF/SoftDeleteInterceptor.cs`](src/Infrastructure/Persistence/EF/SoftDeleteInterceptor.cs)
> · [`src/Infrastructure/Persistence/EF/AuditInterceptor.cs`](src/Infrastructure/Persistence/EF/AuditInterceptor.cs)
> · [`src/Infrastructure/Persistence/EF/DateTimeInterceptor.cs`](src/Infrastructure/Persistence/EF/DateTimeInterceptor.cs)

---

## 9. Global Query Filter with TPH Guard

A generic helper applies the soft-delete filter to every entity inheriting from
`AuditableEntity` — without listing each type manually.

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
        if (entityType.BaseType != null) continue;          // TPH guard

        var parameter = Expression.Parameter(entityType.ClrType);
        var body      = ReplacingExpressionVisitor.Replace(
            filter.Parameters.Single(), parameter, filter.Body);

        modelBuilder
            .Entity(entityType.ClrType)
            .HasQueryFilter(Expression.Lambda(body, parameter));
    }
}

// OnModelCreating:
// modelBuilder.ApplyGlobalFilter<AuditableEntity>(e => !e.IsDeleted);
```

> **File:** [`src/Infrastructure/Persistence/EF/ModelBuilderExtensions.cs`](src/Infrastructure/Persistence/EF/ModelBuilderExtensions.cs)

---

## 10. Zero-Attribute Authorization Convention

An `IActionModelConvention` automatically applies `[Authorize]` to any controller action
whose bound parameter implements `IAuthorizedRequest`. Authorization intent lives on the
command record — controllers stay annotation-free.

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

// Registered once:
// builder.Services.AddControllers(o =>
//     o.Conventions.Add(new AuthorizeByRequestConvention()));
```

> **Note:** The convention works when actions accept command/query records directly.
> When actions take separate DTO types (as in this demo's `SubmitBody`, `AcceptBody`),
> `[Authorize]` is placed on the controller class and `[RequiredPermission]` on each action —
> see §12 for how Swagger picks that up.

> **File:** [`src/API/AuthorizeByRequestConvention.cs`](src/API/AuthorizeByRequestConvention.cs)

---

## 11. Dual Authentication Handlers

Two `AuthenticationHandler<T>` implementations sit side by side — one for development,
one for production. Same structural contract, different validation logic.

```
src/Infrastructure/Identity/
  ├── DemoAuthenticationHandler.cs   ← Development: no OIDC server required
  └── JwtAuthenticationHandler.cs    ← Production:  OIDC discovery + signature validation
```

**`DemoAuthenticationHandler`** — parses the Bearer token value as comma-separated
permission claim names:

```csharp
// Token: "joinrequests:instructor,joinrequests:read"
// → ClaimsIdentity with two claims of those types
// No header → AuthenticateResult.NoResult() → 401
foreach (var perm in token.Split(',', ...))
    claims.Add(new Claim(perm, "true"));
```

**`JwtAuthenticationHandler`** — fetches JWKS from the OIDC discovery document and
validates the JWT signature, expiry, issuer, and audience:

```csharp
var config = await manager.GetConfigurationAsync(ct);  // cached, auto-refreshes on key rotation

var parameters = new TokenValidationParameters
{
    ValidIssuer       = Options.Authority,
    ValidAudience     = Options.Audience,
    IssuerSigningKeys = config.SigningKeys,
    ValidateLifetime  = true,
};
var principal = _tokenHandler.ValidateToken(token, parameters, out _);
```

> `ConfigurationManager<OpenIdConnectConfiguration>` handles JWKS caching and transparent
> key refresh — the same mechanism `AddJwtBearer` uses internally.
> In a typical project the production branch is just `authBuilder.AddJwtBearer(o => { ... })`.

DI selects the handler based on configuration and environment:

```csharp
if (!string.IsNullOrWhiteSpace(jwtAuthority))
    authBuilder.AddScheme<JwtAuthenticationOptions, JwtAuthenticationHandler>("Bearer", o => ...);
else if (environment.IsDevelopment())
    authBuilder.AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>("Bearer", _ => { });
```

> **Files:**
> [`src/Infrastructure/Identity/DemoAuthenticationHandler.cs`](src/Infrastructure/Identity/DemoAuthenticationHandler.cs)
> · [`src/Infrastructure/Identity/JwtAuthenticationHandler.cs`](src/Infrastructure/Identity/JwtAuthenticationHandler.cs)
> · [`src/Infrastructure/Identity/PermissionService.cs`](src/Infrastructure/Identity/PermissionService.cs)

---

## 12. Swagger Authorization Metadata

An `IOperationFilter` reads `[Authorize]` and a custom `[RequiredPermission]` attribute
to annotate each endpoint in the generated OpenAPI spec — padlock icon, 401/403 responses,
and the required claim name — without duplicating that information in XML doc comments.

```csharp
public class AuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorize = context.MethodInfo
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
            || (context.MethodInfo.DeclaringType?
                .GetCustomAttributes<AuthorizeAttribute>(inherit: false).Any() ?? false);

        if (!hasAuthorize) return;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement { [bearerRef] = [] }); // padlock

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });

        var permission = context.MethodInfo.GetCustomAttribute<RequiredPermissionAttribute>();
        if (permission is null) return;

        operation.Description += $"\nRequires JWT claim: `{permission.Permission}`";
        operation.Responses.TryAdd("403", new OpenApiResponse {
            Description = $"Forbidden — JWT lacks the `{permission.Permission}` claim"
        });
    }
}
```

Applied on the controller:

```csharp
[Authorize]
public class GroupJoinRequestsController : ControllerBase
{
    [HttpGet]
    [RequiredPermission(Permissions.JoinRequests.InstructorWrite)]
    public async Task<IActionResult> GetPending(...) { ... }

    [HttpPost("{id:guid}/accept")]
    [RequiredPermission(Permissions.JoinRequests.InstructorWrite)]
    public async Task<IActionResult> Accept(...) { ... }

    [HttpPost]
    [RequiredPermission(Permissions.JoinRequests.StudentWrite)]
    public async Task<IActionResult> Submit(...) { ... }
}
```

> **Files:**
> [`src/API/Swagger/AuthorizationOperationFilter.cs`](src/API/Swagger/AuthorizationOperationFilter.cs)
> · [`src/API/Swagger/RequiredPermissionAttribute.cs`](src/API/Swagger/RequiredPermissionAttribute.cs)

---

## Integration tests

10 tests against a **real PostgreSQL container** via Testcontainers — no mocks in the
database or HTTP layer. The factory boots the full ASP.NET Core pipeline (migrations
included) and is shared across tests to avoid per-test container startup overhead.

```csharp
public class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Auth:Authority"] = ""   // → DemoAuthenticationHandler
            }));

    public HttpClient CreateClientWithPermissions(params string[] permissions)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", string.Join(",", permissions));
        return client;
    }
}
```

Test coverage includes the authorization layer:

```csharp
[Fact]
public async Task AnyEndpoint_WithoutToken_Returns401()
    => Assert.Equal(HttpStatusCode.Unauthorized,
        (await _factory.CreateClient().GetAsync("/api/v1/group-join-requests")).StatusCode);

[Fact]
public async Task GetPending_WithStudentPermissionOnly_Returns403()
    => Assert.Equal(HttpStatusCode.Forbidden,
        (await _factory.CreateClientWithPermissions(Permissions.JoinRequests.StudentWrite)
            .GetAsync("/api/v1/group-join-requests")).StatusCode);
```

> **Files:**
> [`tests/Integration.Tests/IntegrationTestFactory.cs`](tests/Integration.Tests/IntegrationTestFactory.cs)
> · [`tests/Integration.Tests/GroupJoinRequestsEndpointTests.cs`](tests/Integration.Tests/GroupJoinRequestsEndpointTests.cs)

---

## End-to-end: group-join flow

```
Student submits request
  → GroupJoinRequest.Create()             [§2] factory raises GroupJoinRequestSubmitted
  → SaveChanges                           [§7] event written to outbox atomically
                                          [§8] AuditInterceptor stamps CreatedAt

Instructor accepts
  → AcceptGroupJoinRequest.Command        [§3] IAuthorizedRequest → permission declared
  → AuthorizationBehavior                 [§3] verifies joinrequests:instructor claim
  → ValidationBehavior                    [§3] FluentValidation runs
  → Handler calls joinRequest.Accept()    [§2] state transition + raises domain event
  → DomainExceptionBehavior               [§4] guard violation → Result.Failure, not exception
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
  → single SaveChanges                    atomic — all three aggregates in one transaction
```

---

## Stack

| Layer          | Technology                                          |
| -------------- | --------------------------------------------------- |
| API            | ASP.NET Core 8, Swashbuckle                         |
| Application    | MediatR 12, FluentValidation 11                     |
| Domain         | Plain C#, StronglyTypedId source generator          |
| Infrastructure | EF Core 8, PostgreSQL, Dapper, Newtonsoft.Json      |
| Auth           | Custom AuthenticationHandler pair (Demo + JWT/OIDC) |
| Tests          | xUnit, Testcontainers, WebApplicationFactory        |
| DevOps         | Docker Compose                                      |
