using Domain.Abstractions;
using NetArchTest.Rules;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Rules specific to the Domain layer:
/// - Entities must have a private parameterless constructor (EF Core requirement)
/// - Domain events must be immutable (records with no setters)
/// - No public property setters on aggregate roots
/// </summary>
public class DomainRulesTests
{
    private static readonly Assembly DomainAssembly = typeof(Result).Assembly;

    [Fact]
    public void Entities_ShouldHave_PrivateParameterlessConstructor()
    {
        var entityTypes = Types.InAssembly(DomainAssembly)
            .That().Inherit(typeof(AuditableEntity))
            .GetTypes();

        var violators = entityTypes
            .Where(t => !t.IsAbstract)
            .Where(t =>
            {
                var ctor = t.GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                return ctor is null || ctor.IsPublic;
            })
            .Select(t => t.FullName)
            .ToList();

        if (violators.Count > 0)
            throw new Xunit.Sdk.XunitException(
                $"Entities must have a private parameterless constructor (required by EF Core).\n" +
                $"Missing in:\n  {string.Join("\n  ", violators)}");
    }

    [Fact]
    public void DomainEvents_ShouldBeImmutable_Records()
    {
        // IDomainEvent implementations should be records — no mutable state.
        var domainEventTypes = Types.InAssembly(DomainAssembly)
            .That().ImplementInterface(typeof(IDomainEvent))
            .GetTypes();

        var violators = domainEventTypes
            .Where(t => !t.IsAbstract)
            .Where(t =>
            {
                // Records use init-only setters — those are immutable and must not be flagged.
                // A property is truly mutable only when its setter is NOT init-only.
                var mutableProps = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p =>
                    {
                        var setter = p.GetSetMethod();
                        if (setter is null || !setter.IsPublic) return false;
                        // init-only setters carry IsExternalInit as a required modifier.
                        var mods = setter.ReturnParameter.GetRequiredCustomModifiers();
                        return !mods.Contains(typeof(IsExternalInit));
                    })
                    .ToList();
                return mutableProps.Count > 0;
            })
            .Select(t => t.FullName)
            .ToList();

        if (violators.Count > 0)
            throw new Xunit.Sdk.XunitException(
                $"Domain events must be immutable (use record with init-only properties).\n" +
                $"Mutable events:\n  {string.Join("\n  ", violators)}");
    }

    [Fact]
    public void AggregateRoots_ShouldNotHave_PublicPropertySetters()
    {
        var aggregateTypes = Types.InAssembly(DomainAssembly)
            .That().Inherit(typeof(AuditableEntity))
            .GetTypes()
            .Where(t => !t.IsAbstract);

        var violators = new List<string>();

        foreach (var type in aggregateTypes)
        {
            var publicSetters = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.GetSetMethod()?.IsPublic == true)
                .Select(p => $"{type.Name}.{p.Name}")
                .ToList();

            violators.AddRange(publicSetters);
        }

        if (violators.Count > 0)
            throw new Xunit.Sdk.XunitException(
                $"Aggregate properties must use private set or init.\n" +
                $"Public setters found:\n  {string.Join("\n  ", violators)}");
    }
}
