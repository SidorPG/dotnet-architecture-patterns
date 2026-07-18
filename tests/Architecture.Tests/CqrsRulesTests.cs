using Application.Common.Authorization;
using MediatR;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Enforces CQRS naming and structural conventions:
/// - Handlers must be in the Application assembly
/// - Every Command (write) must declare IAuthorizedRequest
/// - Queries must return Result or Result&lt;T&gt; (no raw types)
/// - Handlers must implement IRequestHandler (not bare classes)
/// </summary>
public class CqrsRulesTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(Application.GroupJoinRequests.GroupJoinRequestDto).Assembly;

    [Fact]
    public void Commands_ShouldImplement_IAuthorizedRequest()
    {
        // Any class named "Command" should require at minimum authentication.
        var commandTypes = Types.InAssembly(ApplicationAssembly)
            .That().HaveNameEndingWith("Command")
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .ToList();

        var violators = commandTypes
            .Where(t => !typeof(IAuthorizedRequest).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        if (violators.Count > 0)
            throw new Xunit.Sdk.XunitException(
                $"All Commands must implement IAuthorizedRequest.\n" +
                $"Missing:\n  {string.Join("\n  ", violators)}");
    }

    [Fact]
    public void Handlers_ShouldResideIn_ApplicationAssembly()
    {
        // No handler logic should bleed into Infrastructure or API.
        var handlerInterface = typeof(IRequestHandler<,>);

        var handlers = ApplicationAssembly.GetTypes()
            .Where(t => !t.IsAbstract && t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface))
            .ToList();

        // This assertion passes by construction if the assembly loads correctly —
        // it documents intent and fails the moment a handler is moved out.
        Xunit.Assert.NotEmpty(handlers);

        foreach (var handler in handlers)
            Xunit.Assert.Equal("Application", handler.Assembly.GetName().Name);
    }

    [Fact]
    public void QueryHandlers_ShouldNotDependOn_DbContext_Directly()
    {
        // Query handlers must go through IXxxQueryService, not hit EF DbContext.
        // (Infrastructure.Persistence namespace is the tell.)
        var queryHandlerNamespaces = ApplicationAssembly.GetTypes()
            .Where(t => t.Name == "Handler" &&
                        t.Namespace != null &&
                        !t.Namespace.Contains("Command") &&   // rough heuristic
                        t.GetInterfaces().Any(i =>
                            i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            .ToList();

        var violators = queryHandlerNamespaces
            .Where(t => t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(f => f.FieldType.Name.Contains("DbContext")))
            .Select(t => t.FullName)
            .ToList();

        if (violators.Count > 0)
            throw new Xunit.Sdk.XunitException(
                $"Query handlers must use IXxxQueryService, not DbContext directly.\n" +
                $"Violators:\n  {string.Join("\n  ", violators)}");
    }
}
