using System.Reflection;
using NetArchTest.Rules;

namespace Dsp.ArchitectureTests;

/// <summary>
/// Enforces the clean architecture rules for every service:
/// Domain depends on nothing external; Application depends on Domain and contracts only;
/// no service references another service's code.
/// </summary>
public class CleanArchitectureTests
{
    private sealed record Service(string Name, Assembly Domain, Assembly Application, Assembly Infrastructure);

    private static readonly Service[] Services =
    [
        new("Identity", typeof(Dsp.Identity.Domain.PlatformUser).Assembly,
            typeof(Dsp.Identity.Application.CitizenSignInService).Assembly,
            typeof(Dsp.Identity.Infrastructure.DependencyInjection).Assembly),
        new("EServices", typeof(Dsp.EServices.Domain.Applications.ServiceApplication).Assembly,
            typeof(Dsp.EServices.Application.ApplicationService).Assembly,
            typeof(Dsp.EServices.Infrastructure.DependencyInjection).Assembly),
        new("Payments", typeof(Dsp.Payments.Domain.Payment).Assembly,
            typeof(Dsp.Payments.Application.PaymentService).Assembly,
            typeof(Dsp.Payments.Infrastructure.DependencyInjection).Assembly),
        new("Documents", typeof(Dsp.Documents.Domain.StoredDocument).Assembly,
            typeof(Dsp.Documents.Application.DocumentService).Assembly,
            typeof(Dsp.Documents.Infrastructure.DependencyInjection).Assembly),
        new("Notifications", typeof(Dsp.Notifications.Domain.Notification).Assembly,
            typeof(Dsp.Notifications.Application.NotificationService).Assembly,
            typeof(Dsp.Notifications.Infrastructure.DependencyInjection).Assembly),
        new("Platform", typeof(Dsp.Platform.Domain.Language).Assembly,
            typeof(Dsp.Platform.Application.LanguageService).Assembly,
            typeof(Dsp.Platform.Infrastructure.DependencyInjection).Assembly),
        new("AiOrchestrator", typeof(Dsp.AiOrchestrator.Domain.ChatMessage).Assembly,
            typeof(Dsp.AiOrchestrator.Application.ChatService).Assembly,
            typeof(Dsp.AiOrchestrator.Infrastructure.DependencyInjection).Assembly),
    ];

    private static readonly string[] Frameworks =
    [
        "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Elsa", "RabbitMQ", "Amazon", "Npgsql",
        "Microsoft.Data.SqlClient", "Yarp", "System.Net.Http",
    ];

    public static TheoryData<string> ServiceNames()
    {
        var data = new TheoryData<string>();
        foreach (var s in Services) data.Add(s.Name);
        return data;
    }

    private static Service Get(string name) => Services.Single(s => s.Name == name);

    private static void AssertNoDependency(Assembly assembly, IEnumerable<string> forbidden, string because)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden.ToArray()).GetResult();
        Assert.True(result.IsSuccessful,
            $"{assembly.GetName().Name} {because}. Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Domain_has_no_framework_or_outer_layer_dependencies(string name)
    {
        var s = Get(name);
        AssertNoDependency(s.Domain,
            Frameworks.Concat([$"Dsp.{name}.Application", $"Dsp.{name}.Infrastructure", "Dsp.BuildingBlocks"]),
            "must stay free of frameworks and outer layers");
    }

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Application_does_not_depend_on_infrastructure_or_frameworks(string name)
    {
        var s = Get(name);
        AssertNoDependency(s.Application,
            Frameworks.Concat([$"Dsp.{name}.Infrastructure", "Dsp.BuildingBlocks"]),
            "must depend only on its domain, contracts and abstractions");
    }

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Services_do_not_reference_each_other(string name)
    {
        var others = Services.Where(o => o.Name != name).Select(o => $"Dsp.{o.Name}").ToArray();
        var s = Get(name);
        foreach (var assembly in new[] { s.Domain, s.Application, s.Infrastructure })
            AssertNoDependency(assembly, others, "must talk to other services through APIs or events only");
    }

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Repositories_and_clients_live_in_infrastructure(string name)
    {
        var s = Get(name);
        var result = Types.InAssembly(s.Infrastructure)
            .That().Inherit(typeof(Microsoft.EntityFrameworkCore.DbContext))
            .Should().ResideInNamespaceStartingWith($"Dsp.{name}.Infrastructure")
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Use_cases_take_every_port_as_a_required_dependency(string name)
    {
        // Optional or defaulted ports hide missing wiring until runtime (see AGENTS.md).
        var optional = Get(name).Application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsPublic)
            .SelectMany(t => t.GetConstructors().SelectMany(c => c.GetParameters()
                .Where(p => p.ParameterType.IsInterface && p.HasDefaultValue &&
                            p.ParameterType.Namespace?.StartsWith("Dsp.", StringComparison.Ordinal) == true)
                .Select(p => $"{t.Name}({p.ParameterType.Name} {p.Name})")))
            .ToList();
        Assert.True(optional.Count == 0, $"Optional dependencies: {string.Join(", ", optional)}");
    }
}
