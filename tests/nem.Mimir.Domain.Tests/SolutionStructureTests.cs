using System.Xml.Linq;
using Shouldly;

namespace nem.Mimir.Domain.Tests;

public class SolutionStructureTests
{
    private static string GetProjectRootPath()
    {
        // Get the directory of the test assembly and navigate up to solution root
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var di = new DirectoryInfo(currentDir);
        // Navigate from bin/Debug/net10.0 to solution root
        while (di != null && !File.Exists(Path.Combine(di.FullName, "nem.Mimir.slnx")))
        {
            di = di.Parent;
        }
        return di?.FullName ?? throw new InvalidOperationException("Could not find solution root");
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
        var projectReferences = doc.Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

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
        var projectReferences = doc.Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

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
        var projectReferences = doc.Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

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
