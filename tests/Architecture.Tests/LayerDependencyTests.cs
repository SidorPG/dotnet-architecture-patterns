using NetArchTest.Rules;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Enforces the one-way dependency rule between layers:
///
///   API  →  Application  →  Domain
///   Infrastructure  →  Application  →  Domain
///
/// Domain has zero external dependencies.
/// Application never imports Infrastructure or API.
/// </summary>
public class LayerDependencyTests
{
    // Assembly anchors — change these if you rename the projects.
    private static readonly System.Reflection.Assembly DomainAssembly         = typeof(Domain.Abstractions.Result).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly    = typeof(Application.GroupJoinRequests.GroupJoinRequestDto).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(Infrastructure.Outbox.OutboxProcessor).Assembly;

    // ── Domain: no upstream dependencies ─────────────────────────

    [Fact]
    public void Domain_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn("Application")
            .GetResult();

        Assert(result, "Domain must not reference Application");
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn("Infrastructure")
            .GetResult();

        Assert(result, "Domain must not reference Infrastructure");
    }

    [Fact]
    public void Domain_ShouldNotDependOn_API()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn("API")
            .GetResult();

        Assert(result, "Domain must not reference API");
    }

    // ── Application: may only depend on Domain ───────────────────

    [Fact]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn("Infrastructure")
            .GetResult();

        Assert(result, "Application must not reference Infrastructure (use interfaces instead)");
    }

    [Fact]
    public void Application_ShouldNotDependOn_API()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn("API")
            .GetResult();

        Assert(result, "Application must not reference API");
    }

    // ── Infrastructure: may depend on Application + Domain ───────

    [Fact]
    public void Infrastructure_ShouldNotDependOn_API()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn("API")
            .GetResult();

        Assert(result, "Infrastructure must not reference API");
    }

    // ── Helper ───────────────────────────────────────────────────

    private static void Assert(TestResult result, string rule)
    {
        if (result.IsSuccessful) return;

        var violators = string.Join("\n  ", result.FailingTypeNames ?? []);
        throw new Xunit.Sdk.XunitException(
            $"Architecture rule violated: {rule}\n\nOffending types:\n  {violators}");
    }
}
