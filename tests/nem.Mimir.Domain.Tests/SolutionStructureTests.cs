using System.Xml.Linq;
using Shouldly;

namespace nem.Mimir.Domain.Tests;

public class SolutionStructureTests
{
    private static string GetProjectRootPath()
    {
        foreach (var candidate in GetRootCandidates())
        {
            var resolved = ResolveRepositoryRoot(candidate);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        throw new InvalidOperationException("Could not find solution root");
    }

    private static IEnumerable<string> GetRootCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in EnumeratePathAndParents(AppDomain.CurrentDomain.BaseDirectory))
        {
            if (seen.Add(path))
            {
                yield return path;
            }
        }

        foreach (var path in EnumeratePathAndParents(Environment.CurrentDirectory))
        {
            if (seen.Add(path))
            {
                yield return path;
            }
        }

        foreach (var path in new[]
                 {
                     "/workspace/wmreflect/nem.Mimir-typed-ids",
                     "/workspace/wmreflect",
                     "/workspace",
                 })
        {
            if (seen.Add(path))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> EnumeratePathAndParents(string? start)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            yield break;
        }

        var current = new DirectoryInfo(start);
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private static string? ResolveRepositoryRoot(string candidate)
    {
        if (!Directory.Exists(candidate))
        {
            return null;
        }

        if (IsRepositoryRoot(candidate))
        {
            return candidate;
        }

        var nested = Path.Combine(candidate, "nem.Mimir-typed-ids");
        return IsRepositoryRoot(nested) ? nested : null;
    }

    private static bool IsRepositoryRoot(string path)
    {
        return File.Exists(Path.Combine(path, "nem.Mimir.slnx"))
               || File.Exists(Path.Combine(path, "nem.Mimir.sln"));
    }

    [Fact]
    public void DomainProject_ShouldHaveNoProjectReferences()
    {
        // Arrange
        var projectRoot = GetProjectRootPath();
        var domainProjPath = Path.Combine(projectRoot, "src", "nem.Mimir.Domain", "nem.Mimir.Domain.csproj");
        var doc = XDocument.Load(domainProjPath);

        // Act
        var projectReferences = doc.Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        // Assert
        projectReferences.Count.ShouldBe(1);
        projectReferences[0].ToLower().ShouldContain("nem.contracts");
    }

    [Fact]
    public void ApplicationProject_ShouldOnlyReferenceDomainProject()
    {
        // Arrange
        var projectRoot = GetProjectRootPath();
        var appProjPath = Path.Combine(projectRoot, "src", "nem.Mimir.Application", "nem.Mimir.Application.csproj");
        var doc = XDocument.Load(appProjPath);

        // Act
        var projectReferences = doc.Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        // Assert
        projectReferences.Count.ShouldBe(2);
        projectReferences.ShouldContain(r => r.ToLower().Contains("mimir.domain"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("nem.contracts"));
    }

    [Fact]
    public void InfrastructureProject_ShouldOnlyReferenceApplicationProject()
    {
        // Arrange
        var projectRoot = GetProjectRootPath();
        var infProjPath = Path.Combine(projectRoot, "src", "nem.Mimir.Infrastructure", "nem.Mimir.Infrastructure.csproj");
        var doc = XDocument.Load(infProjPath);

        // Act
        var projectReferences = doc.Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        // Assert
        projectReferences.Count.ShouldBe(10);
        projectReferences.ShouldContain(r => r.ToLower().Contains("mimir.application"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("mimir.finance.mcptools"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("nem.contracts"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("nem.contracts.aspnetcore"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("knowhub.abstractions"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("knowhub.distillation"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("knowhub.graphrag"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("knowhub.graph"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("nem.mcp.core"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("nem.workflow.domain"));
    }

    [Fact]
    public void ApiProject_ShouldReferenceInfrastructureAndSyncProjects()
    {
        // Arrange
        var projectRoot = GetProjectRootPath();
        var apiProjPath = Path.Combine(projectRoot, "src", "nem.Mimir.Api", "nem.Mimir.Api.csproj");
        var doc = XDocument.Load(apiProjPath);

        // Act
        var projectReferences = doc.Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        // Assert
        projectReferences.Count.ShouldBe(3);
        projectReferences.ShouldContain(r => r.ToLower().Contains("mimir.infrastructure"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("mimir.sync"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("nem.contracts.aspnetcore"));
    }

    [Fact]
    public void AllSourceProjectsShouldExist()
    {
        // Arrange
        var projectRoot = GetProjectRootPath();
        var projectPaths = new[]
        {
            Path.Combine(projectRoot, "src", "nem.Mimir.Domain", "nem.Mimir.Domain.csproj"),
            Path.Combine(projectRoot, "src", "nem.Mimir.Application", "nem.Mimir.Application.csproj"),
            Path.Combine(projectRoot, "src", "nem.Mimir.Infrastructure", "nem.Mimir.Infrastructure.csproj"),
            Path.Combine(projectRoot, "src", "nem.Mimir.Api", "nem.Mimir.Api.csproj"),
        };

        // Assert
        foreach (var path in projectPaths)
        {
            File.Exists(path).ShouldBeTrue();
        }
    }
}
