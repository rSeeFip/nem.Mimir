using nem.Mimir.Domain.Plugins;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Plugins;

public class PluginMetadataTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_Metadata()
    {
        // Arrange & Act
        var metadata = PluginMetadata.Create("test-plugin", "Test Plugin", "1.0.0", "A test plugin");

        // Assert
        metadata.Id.ShouldBe("test-plugin");
        metadata.Name.ShouldBe("Test Plugin");
        metadata.Version.ShouldBe("1.0.0");
        metadata.Description.ShouldBe("A test plugin");
    }

    [Fact]
    public void Create_With_Null_Id_Should_Throw()
    {
        Should.Throw<ArgumentNullException>(() =>
            PluginMetadata.Create(null!, "Name", "1.0.0", "Desc"));
    }

    [Fact]
    public void Create_With_Empty_Id_Should_Throw()
    {
        Should.Throw<ArgumentException>(() =>
            PluginMetadata.Create("", "Name", "1.0.0", "Desc"));
    }

    [Fact]
    public void Create_With_Whitespace_Id_Should_Throw()
    {
        Should.Throw<ArgumentException>(() =>
            PluginMetadata.Create("   ", "Name", "1.0.0", "Desc"));
    }

    [Fact]
    public void Create_With_Null_Name_Should_Throw()
    {
        Should.Throw<ArgumentNullException>(() =>
            PluginMetadata.Create("id", null!, "1.0.0", "Desc"));
    }

    [Fact]
    public void Create_With_Empty_Name_Should_Throw()
    {
        Should.Throw<ArgumentException>(() =>
            PluginMetadata.Create("id", "", "1.0.0", "Desc"));
    }

    [Fact]
    public void Create_With_Null_Version_Should_Throw()
    {
        Should.Throw<ArgumentNullException>(() =>
            PluginMetadata.Create("id", "Name", null!, "Desc"));
    }

    [Fact]
    public void Create_With_Empty_Version_Should_Throw()
    {
        Should.Throw<ArgumentException>(() =>
            PluginMetadata.Create("id", "Name", "", "Desc"));
    }

    [Fact]
    public void Create_With_Null_Description_Should_Use_Empty_String()
    {
        // A null description is allowed — defaults to empty string
        var metadata = PluginMetadata.Create("id", "Name", "1.0.0", null!);
        metadata.Description.ShouldBe(string.Empty);
    }

    [Fact]
    public void Metadata_With_Same_Values_Should_Be_Equal()
    {
        var a = PluginMetadata.Create("id", "Name", "1.0.0", "Desc");
        var b = PluginMetadata.Create("id", "Name", "1.0.0", "Desc");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Metadata_With_Different_Id_Should_Not_Be_Equal()
    {
        var a = PluginMetadata.Create("id-a", "Name", "1.0.0", "Desc");
        var b = PluginMetadata.Create("id-b", "Name", "1.0.0", "Desc");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void ToString_Should_Return_Meaningful_Representation()
    {
        var metadata = PluginMetadata.Create("test-plugin", "Test Plugin", "1.0.0", "A test plugin");

        var result = metadata.ToString();

        result.ShouldContain("test-plugin");
        result.ShouldContain("1.0.0");
    }
}
