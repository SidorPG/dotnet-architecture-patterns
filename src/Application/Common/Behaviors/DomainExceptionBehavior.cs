using Domain.Abstractions;
using MediatR;

namespace Application.Common.Behaviors;

/// <summary>
/// Third pipeline behavior. Catches DomainException thrown inside aggregates and
/// converts it to Result.Failure() so the API layer receives a structured error
/// rather than an unhandled 500.
///
/// Handlers never need try/catch — the pipeline absorbs domain guard violations.
///
/// Pipeline order:
///   AuthorizationBehavior → ValidationBehavior → DomainExceptionBehavior → Handler
/// </summary>
public sealed class DomainExceptionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 ct)
    {
        try
        {
            return await next();
        }
        catch (DomainException ex)
        {
            // Works for both Result and Result<T> via reflection —
            // avoids duplicating the catch block per generic type.
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

// Domain guard: thrown by aggregates for invariant violations, not for "not found" cases.
public class DomainException(string message) : Exception(message);
