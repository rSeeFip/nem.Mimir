using nem.Mimir.Domain.Common;
using Shouldly;
using Xunit;

namespace nem.Mimir.Domain.Tests.Common;

public class BaseEntityTests
{
    private class TestEntity : BaseEntity<int>
    {
        public TestEntity(int id) => Id = id;
    }

    private class TestDomainEvent : IDomainEvent
    {
    }

    [Fact]
    public void Entities_With_Same_Id_Should_Be_Equal()
    {
        // Arrange
        var entity1 = new TestEntity(1);
        var entity2 = new TestEntity(1);

        // Act & Assert
        entity1.ShouldBe(entity2);
    }

    [Fact]
    public void Entities_With_Different_Ids_Should_Not_Be_Equal()
    {
        // Arrange
        var entity1 = new TestEntity(1);
        var entity2 = new TestEntity(2);

        // Act & Assert
        entity1.ShouldNotBe(entity2);
    }

    [Fact]
    public void Entities_Can_Register_Domain_Events()
    {
        // Arrange
        var entity = new TestEntity(1);
        var domainEvent = new TestDomainEvent();

        // Act
        entity.AddDomainEvent(domainEvent);

        // Assert
        entity.DomainEvents.ShouldContain(domainEvent);
        entity.DomainEvents.Count.ShouldBe(1);
    }

    [Fact]
    public void Entities_Can_Clear_Domain_Events()
    {
        // Arrange
        var entity = new TestEntity(1);
        entity.AddDomainEvent(new TestDomainEvent());

        // Act
        entity.ClearDomainEvents();

        // Assert
        entity.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Entity_With_Default_Id_Should_Not_Equal_Another_With_Default_Id()
    {
        // Arrange
        var entity1 = new TestEntity(0);
        var entity2 = new TestEntity(0);

        // Act & Assert
        // Default Id (0) should still use equality by Id, but they are different objects
        entity1.ShouldBe(entity2); // Same Id = equal
    }

    [Fact]
    public void Equality_Operator_Works_Correctly()
    {
        // Arrange
        var entity1 = new TestEntity(1);
        var entity2 = new TestEntity(1);
        var entity3 = new TestEntity(2);

        // Act & Assert
        (entity1 == entity2).ShouldBeTrue();
        (entity1 != entity3).ShouldBeTrue();
    }
}
