using API;
using Application.Common.Behaviors;
using Application.Common.Interfaces;
using Application.GroupJoinProcessManagers;
using Application.GroupJoinRequests;
using FluentValidation;
using Infrastructure.Identity;
using Infrastructure.Outbox;
using Infrastructure.Persistence;
using Infrastructure.Persistence.EF;
using Infrastructure.QueryServices;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options
        .UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(
            sp.GetRequiredService<SoftDeleteInterceptor>(),
            sp.GetRequiredService<AuditInterceptor>(),
            new DomainEventDispatcherInterceptor()));

builder.Services.AddScoped<SoftDeleteInterceptor>();
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// ── Query services (Infrastructure implementations) ───────────────
builder.Services.AddScoped<IGroupJoinRequestQueryService,        GroupJoinRequestQueryService>();
builder.Services.AddScoped<IGroupJoinProcessManagerQueryService, GroupJoinProcessManagerQueryService>();

// ── MediatR pipeline ──────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Application.Common.Behaviors.AuthorizationBehavior<,>).Assembly);

    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(DomainExceptionBehavior<,>));
});

// ── Validation ────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssembly(typeof(Application.Common.Behaviors.AuthorizationBehavior<,>).Assembly);

// ── Controllers + Zero-attribute auth convention ──────────────────
builder.Services
    .AddControllers(o => o.Conventions.Add(new AuthorizeByRequestConvention()))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));

// ── Auth ──────────────────────────────────────────────────────────
// JWT is optional for local demo — omit Auth:Authority in config to skip validation.
var jwtAuthority = builder.Configuration["Auth:Authority"];

var authBuilder = builder.Services.AddAuthentication("Bearer");
if (!string.IsNullOrWhiteSpace(jwtAuthority))
{
    authBuilder.AddJwtBearer("Bearer", o =>
    {
        o.Authority = jwtAuthority;
        o.Audience  = builder.Configuration["Auth:Audience"];
    });
}

builder.Services.AddAuthorization();

// ── Swagger ───────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Infrastructure services ───────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

// ── Outbox background processor ───────────────────────────────────
builder.Services.AddHostedService<OutboxProcessor>();

// ─────────────────────────────────────────────────────────────────
var app = builder.Build();

// Apply any pending EF Core migrations on startup.
// Safe to call repeatedly — a no-op when the schema is up to date.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
