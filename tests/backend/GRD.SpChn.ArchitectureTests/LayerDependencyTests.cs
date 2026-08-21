using System.Xml.Linq;

namespace GRD.SpChn.ArchitectureTests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_projects_have_no_outgoing_project_or_package_dependencies()
    {
        var projects = GetServiceProjects("*.Domain.csproj");

        Assert.NotEmpty(projects);

        foreach (var project in projects)
        {
            var document = XDocument.Load(project);
            var dependencies = document
                .Descendants()
                .Where(element =>
                    element.Name.LocalName is "ProjectReference" or "PackageReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(value => value is not null)
                .ToArray();

            Assert.True(
                dependencies.Length == 0,
                $"Domain project '{project}' must not depend on outer layers: " +
                string.Join(", ", dependencies));
        }
    }

    [Fact]
    public void Application_projects_do_not_reference_infrastructure_or_api_projects()
    {
        AssertReferencesDoNotContain(
            GetServiceProjects("*.Application.csproj"),
            ".Infrastructure",
            ".Api");
    }

    [Fact]
    public void Infrastructure_projects_do_not_reference_api_projects()
    {
        AssertReferencesDoNotContain(
            GetServiceProjects("*.Infrastructure.csproj"),
            ".Api");
    }

    [Fact]
    public void Services_do_not_reference_another_services_projects()
    {
        var root = FindRepositoryRoot();
        var servicesRoot = Path.Combine(root, "src", "backend", "Services");
        var projects = Directory.EnumerateFiles(
            servicesRoot,
            "*.csproj",
            SearchOption.AllDirectories);

        foreach (var project in projects)
        {
            var owner = Path.GetRelativePath(servicesRoot, project)
                .Split(Path.DirectorySeparatorChar)[0];
            var document = XDocument.Load(project);
            var referencedServiceProjects = document
                .Descendants()
                .Where(element => element.Name.LocalName == "ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => Path.GetFullPath(
                    Path.Combine(Path.GetDirectoryName(project)!, value!)))
                .Where(path => path.StartsWith(
                    servicesRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));

            foreach (var referencedProject in referencedServiceProjects)
            {
                var referencedOwner = Path.GetRelativePath(servicesRoot, referencedProject)
                    .Split(Path.DirectorySeparatorChar)[0];
                Assert.True(
                    string.Equals(owner, referencedOwner, StringComparison.OrdinalIgnoreCase),
                    $"Service '{owner}' must communicate with '{referencedOwner}' through " +
                    $"an integration contract, not project reference '{referencedProject}'.");
            }
        }
    }

    private static void AssertReferencesDoNotContain(
        IEnumerable<string> projects,
        params string[] forbiddenSegments)
    {
        var projectPaths = projects.ToArray();
        Assert.NotEmpty(projectPaths);

        foreach (var project in projectPaths)
        {
            var document = XDocument.Load(project);
            var references = document
                .Descendants()
                .Where(element => element.Name.LocalName == "ProjectReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .ToArray();

            foreach (var forbiddenSegment in forbiddenSegments)
            {
                Assert.DoesNotContain(
                    references,
                    reference => reference.Contains(
                        forbiddenSegment,
                        StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private static IEnumerable<string> GetServiceProjects(string pattern)
    {
        var root = FindRepositoryRoot();
        var services = Path.Combine(root, "src", "backend", "Services");

        return Directory.EnumerateFiles(
            services,
            pattern,
            SearchOption.AllDirectories);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GRD.SpChn.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the GRD solution root from the test output directory.");
    }
}
