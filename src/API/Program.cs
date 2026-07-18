using Application.Common.Behaviors;
using FluentValidation;
using Infrastructure.Outbox;
using Infrastructure.Persistence.EF;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(
            new SoftDeleteInterceptor(/* ICurrentUser resolved by EF's DI */),
            new AuditInterceptor(/* ICurrentUser */),
            new DomainEventDispatcherInterceptor()));

// ── MediatR pipeline ──────────────────────────────────────────────
// Order matters: Auth → Validation → DomainException → Handler
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Application.AssemblyReference).Assembly);

    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(DomainExceptionBehavior<,>));
});

// ── Validation ────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);

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
builder.Services.AddScoped<Application.Common.Interfaces.ICurrentUser, Infrastructure.Identity.CurrentUser>();
builder.Services.AddScoped<Application.Common.Interfaces.IPermissionService, Infrastructure.Identity.PermissionService>();

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

// Marker type for assembly scanning
namespace Application
{
    public sealed class AssemblyReference;
}
