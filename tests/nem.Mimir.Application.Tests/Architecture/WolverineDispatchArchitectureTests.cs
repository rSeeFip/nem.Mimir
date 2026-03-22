using System.Text.RegularExpressions;
using Shouldly;

namespace nem.Mimir.Application.Tests.Architecture;

public sealed partial class WolverineDispatchArchitectureTests
{
    [Fact]
    public void DispatchPatternDetector_ShouldFlagKnownWolverineDispatchApis()
    {
        const string sample = """
                              using Wolverine;

                              public sealed class BadDispatch
                              {
                                  private readonly ICommandBus _bus;

                                  public BadDispatch(ICommandBus bus)
                                  {
                                      _bus = bus;
                                  }

                                  public Task HandleAsync(object command)
                                      => _bus.InvokeAsync(command);
                              }
                              """;

        var violations = FindForbiddenWolverineDispatchPatterns(sample);

        violations.ShouldNotBeEmpty();
        violations.ShouldContain(v => v.Contains("ICommandBus", StringComparison.Ordinal));
        violations.ShouldContain(v => v.Contains("InvokeAsync(", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplicationAndDomain_ShouldNotUseWolverineForCommandQueryDispatch()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guardedDirectories = new[]
        {
            Path.Combine(repositoryRoot, "src", "nem.Mimir.Application"),
            Path.Combine(repositoryRoot, "src", "nem.Mimir.Domain"),
        };

        var violations = new List<string>();

        foreach (var directory in guardedDirectories)
        {
            foreach (var filePath in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(filePath);
                var fileViolations = FindForbiddenWolverineDispatchPatterns(source);

                foreach (var violation in fileViolations)
                {
                    var relativePath = Path.GetRelativePath(repositoryRoot, filePath);
                    violations.Add($"{relativePath}: {violation}");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var appProjectPath = Path.Combine(current.FullName, "src", "nem.Mimir.Application", "nem.Mimir.Application.csproj");
            if (File.Exists(appProjectPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root containing src/nem.Mimir.Application.");
    }

    private static IReadOnlyList<string> FindForbiddenWolverineDispatchPatterns(string source)
    {
        var violations = new List<string>();

        if (ForbiddenICommandBusRegex().IsMatch(source))
        {
            violations.Add("contains ICommandBus");
        }

        if (ForbiddenInvokeAsyncRegex().IsMatch(source))
        {
            violations.Add("contains InvokeAsync(");
        }

        if (ForbiddenWolverineHandlerRegex().IsMatch(source))
        {
            violations.Add("contains [WolverineHandler]");
        }

        return violations;
    }

    [GeneratedRegex(@"\bICommandBus\b", RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenICommandBusRegex();

    [GeneratedRegex(@"\bInvokeAsync\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenInvokeAsyncRegex();

    [GeneratedRegex(@"\[\s*WolverineHandler\b", RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenWolverineHandlerRegex();
}
