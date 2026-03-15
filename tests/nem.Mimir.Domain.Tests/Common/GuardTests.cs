using nem.Mimir.Domain.Common;
using Shouldly;
using Xunit;

namespace nem.Mimir.Domain.Tests.Common;

public class GuardTests
{
    [Fact]
    public void Guard_Against_Null_Throws_When_Value_Is_Null()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => Guard.Against.Null<string>(null, nameof(Guard)));
    }

    [Fact]
    public void Guard_Against_Null_Does_Not_Throw_When_Value_Is_Not_Null()
    {
        // Arrange & Act & Assert
        Guard.Against.Null("test", nameof(Guard));
        Guard.Against.Null(42, nameof(Guard));
        Guard.Against.Null(new object(), nameof(Guard));
    }

    [Fact]
    public void Guard_Against_NullOrEmpty_Throws_When_String_Is_Null()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => Guard.Against.NullOrEmpty(null, nameof(Guard)));
    }

    [Fact]
    public void Guard_Against_NullOrEmpty_Throws_When_String_Is_Empty()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => Guard.Against.NullOrEmpty("", nameof(Guard)));
    }

    [Fact]
    public void Guard_Against_NullOrEmpty_Does_Not_Throw_When_String_Has_Content()
    {
        // Arrange & Act & Assert
        Guard.Against.NullOrEmpty("test", nameof(Guard));
        Guard.Against.NullOrEmpty("a", nameof(Guard));
    }

    [Fact]
    public void Guard_Against_NullOrWhiteSpace_Throws_When_String_Is_Null()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => Guard.Against.NullOrWhiteSpace(null, nameof(Guard)));
    }

    [Fact]
    public void Guard_Against_NullOrWhiteSpace_Throws_When_String_Is_Empty()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => Guard.Against.NullOrWhiteSpace("", nameof(Guard)));
    }

    [Fact]
    public void Guard_Against_NullOrWhiteSpace_Throws_When_String_Is_WhiteSpace()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => Guard.Against.NullOrWhiteSpace("   ", nameof(Guard)));
    }

    [Fact]
    public void Guard_Against_NullOrWhiteSpace_Does_Not_Throw_When_String_Has_Content()
    {
        // Arrange & Act & Assert
        Guard.Against.NullOrWhiteSpace("test", nameof(Guard));
        Guard.Against.NullOrWhiteSpace("a", nameof(Guard));
    }

    [Fact]
    public void Guard_Against_OutOfRange_Throws_When_Value_Out_Of_Range()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => Guard.Against.OutOfRange(10, 0, 5, nameof(Guard)));
    }

    [Fact]
    public void Guard_Against_OutOfRange_Does_Not_Throw_When_Value_In_Range()
    {
        // Arrange & Act & Assert
        Guard.Against.OutOfRange(5, 0, 10, nameof(Guard));
        Guard.Against.OutOfRange(0, 0, 10, nameof(Guard));
        Guard.Against.OutOfRange(10, 0, 10, nameof(Guard));
    }

    [Fact]
    public void Guard_Against_Default_Throws_When_Value_Is_Default()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => Guard.Against.Default<int>(0, nameof(Guard)));
        Should.Throw<ArgumentException>(() => Guard.Against.Default<string>(null, nameof(Guard)));
    }

    [Fact]
    public void Guard_Against_Default_Does_Not_Throw_When_Value_Is_Not_Default()
    {
        // Arrange & Act & Assert
        Guard.Against.Default(42, nameof(Guard));
        Guard.Against.Default("test", nameof(Guard));
        Guard.Against.Default(1, nameof(Guard));
    }
}
