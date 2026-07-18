using Application.Common.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace API;

/// <summary>
/// MVC action model convention that automatically applies [Authorize] to any
/// controller action whose bound parameter implements IAuthorizedRequest.
///
/// Effect: zero [Authorize] attributes in the codebase. Authorization is
/// declared on the command/query record itself, and the convention wires it
/// to the HTTP layer automatically.
///
/// Registered in DI:
///   builder.Services.AddControllers(o =>
///       o.Conventions.Add(new AuthorizeByRequestConvention()));
/// </summary>
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
