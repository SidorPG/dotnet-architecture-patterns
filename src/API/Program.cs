using API;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApiLayer(builder.Configuration);

var app = builder.Build();

app.UseInfrastructure();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Maps domain-level exceptions thrown by pipeline behaviors to HTTP status codes.
app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    ctx.Response.StatusCode = ex switch
    {
        UnauthorizedAccessException                       => StatusCodes.Status401Unauthorized,
        Application.Common.Behaviors.ForbiddenException  => StatusCodes.Status403Forbidden,
        _                                                 => StatusCodes.Status500InternalServerError,
    };
    if (ctx.Response.StatusCode == 500 && ex is not null)
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
}));

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
