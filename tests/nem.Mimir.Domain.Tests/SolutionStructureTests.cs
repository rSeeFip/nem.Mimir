using System.Xml.Linq;
using Shouldly;

namespace nem.Mimir.Domain.Tests;

public class SolutionStructureTests
{
    private static string GetProjectRootPath()
    {
        var candidates = new List<string>();

        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var di = new DirectoryInfo(currentDir);

        while (di is not null)
        {
            candidates.Add(di.FullName);
            di = di.Parent;
        }

        candidates.Add("/workspace/wmreflect/nem.Mimir");
        candidates.Add("/workspace/wmreflect");

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var root = candidate.EndsWith("nem.Mimir", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : Path.Combine(candidate, "nem.Mimir");

            if (File.Exists(Path.Combine(root, "nem.Mimir.slnx")))
            {
                return root;
            }
        }

        throw new InvalidOperationException("Could not find nem.Mimir solution root");
    }

    private static IReadOnlyList<string> GetMimirProjectReferences(XDocument project)
    {
        return project
            .Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
            .Where(r => r.Contains("nem.Mimir.", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    [Fact]
    public void DomainProject_ShouldHaveNoProjectReferences()
    {
        // Arrange
        var projectRoot = GetProjectRootPath();
        var domainProjPath = Path.Combine(projectRoot, "src", "nem.Mimir.Domain", "nem.Mimir.Domain.csproj");
        var doc = XDocument.Load(domainProjPath);

        // Act
        var projectReferences = doc.Descendants("ProjectReference").Count();

        // Assert
        projectReferences.ShouldBe(0);
    }

    [Fact]
    public void ApplicationProject_ShouldOnlyReferenceDomainProject()
    {
        // Arrange
        var projectRoot = GetProjectRootPath();
        var appProjPath = Path.Combine(projectRoot, "src", "nem.Mimir.Application", "nem.Mimir.Application.csproj");
        var doc = XDocument.Load(appProjPath);

        // Act
        var projectReferences = GetMimirProjectReferences(doc);

        // Assert
        projectReferences.ShouldHaveSingleItem();
        projectReferences[0].ToLower().ShouldContain("nem.mimir.domain");
    }

    [Fact]
    public void InfrastructureProject_ShouldOnlyReferenceApplicationProject()
    {
        // Arrange
        var projectRoot = GetProjectRootPath();
        var infProjPath = Path.Combine(projectRoot, "src", "nem.Mimir.Infrastructure", "nem.Mimir.Infrastructure.csproj");
        var doc = XDocument.Load(infProjPath);

        // Act
        var projectReferences = GetMimirProjectReferences(doc);

        // Assert
        projectReferences.ShouldHaveSingleItem();
        projectReferences[0].ToLower().ShouldContain("nem.mimir.application");
    }

    [Fact]
    public void ApiProject_ShouldReferenceInfrastructureAndSyncProjects()
    {
        // Arrange
        var projectRoot = GetProjectRootPath();
        var apiProjPath = Path.Combine(projectRoot, "src", "nem.Mimir.Api", "nem.Mimir.Api.csproj");
        var doc = XDocument.Load(apiProjPath);

        // Act
        var projectReferences = GetMimirProjectReferences(doc);

        // Assert
        projectReferences.Count.ShouldBe(2);
        projectReferences.ShouldContain(r => r.ToLower().Contains("nem.mimir.infrastructure"));
        projectReferences.ShouldContain(r => r.ToLower().Contains("nem.mimir.sync"));
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
