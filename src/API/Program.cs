using API;
using Application.Common.Behaviors;
using Application.Common.Interfaces;
using FluentValidation;
using Infrastructure.Identity;
using Infrastructure.Outbox;
using Infrastructure.Persistence;
using Infrastructure.Persistence.EF;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

// Interceptors that need ICurrentUser are resolved lazily from the DI scope.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options
        .UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(
            sp.GetRequiredService<SoftDeleteInterceptor>(),
            sp.GetRequiredService<AuditInterceptor>(),
            new DomainEventDispatcherInterceptor()));

// Register interceptors so EF Core can resolve them through the DI scope.
builder.Services.AddScoped<SoftDeleteInterceptor>();
builder.Services.AddScoped<AuditInterceptor>();

// IApplicationDbContext → AppDbContext (Infrastructure)
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// ── MediatR pipeline ──────────────────────────────────────────────
// Order matters: Auth → Validation → DomainException → Handler
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
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// ── Auth ──────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", o =>
    {
        o.Authority = builder.Configuration["Auth:Authority"];
        o.Audience  = builder.Configuration["Auth:Audience"];
    });
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
