using nem.Mimir.Domain.Common;
using Shouldly;
using Xunit;

namespace nem.Mimir.Domain.Tests.Common;

public class ValueObjectTests
{
    private class TestValueObject : ValueObject
    {
        public string Name { get; }
        public int Value { get; }

        public TestValueObject(string name, int value)
        {
            Name = name;
            Value = value;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Name;
            yield return Value;
        }
    }

    [Fact]
    public void Value_Objects_With_Same_Components_Should_Be_Equal()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 42);
        var vo2 = new TestValueObject("test", 42);

        // Act & Assert
        vo1.ShouldBe(vo2);
    }

    [Fact]
    public void Value_Objects_With_Different_Components_Should_Not_Be_Equal()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 42);
        var vo2 = new TestValueObject("test", 43);

        // Act & Assert
        vo1.ShouldNotBe(vo2);
    }

    [Fact]
    public void Value_Objects_With_Same_Components_Have_Same_Hash_Code()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 42);
        var vo2 = new TestValueObject("test", 42);

        // Act & Assert
        vo1.GetHashCode().ShouldBe(vo2.GetHashCode());
    }

    [Fact]
    public void Equality_Operator_Works_For_Value_Objects()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 42);
        var vo2 = new TestValueObject("test", 42);
        var vo3 = new TestValueObject("test", 43);

        // Act & Assert
        (vo1 == vo2).ShouldBeTrue();
        (vo1 != vo3).ShouldBeTrue();
    }

    [Fact]
    public void Value_Objects_Handle_Null_Components()
    {
        // Arrange
        var vo1 = new TestValueObject(null!, 42);
        var vo2 = new TestValueObject(null!, 42);

        // Act & Assert
        vo1.ShouldBe(vo2);
    }
}
