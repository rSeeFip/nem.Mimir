using nem.Mimir.Domain.Entities;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public sealed class KnowledgeCollectionTests
{
    [Fact]
    public void Create_WithValidData_ShouldInitializeCollection()
    {
        var userId = Guid.NewGuid();

        var collection = KnowledgeCollection.Create(userId, "Team Docs", "Internal runbooks");

        collection.Id.ShouldNotBe(default);
        collection.UserId.ShouldBe(userId);
        collection.Name.ShouldBe("Team Docs");
        collection.Description.ShouldBe("Internal runbooks");
        collection.Documents.ShouldBeEmpty();
    }

    [Fact]
    public void AddDocument_WithDuplicateId_ShouldNotAddTwice()
    {
        var collection = KnowledgeCollection.Create(Guid.NewGuid(), "Docs");
        var documentId = Guid.NewGuid();

        collection.AddDocument(documentId, "ops.pdf", "https://storage/ops.pdf", "application/pdf");
        collection.AddDocument(documentId, "ops.pdf", "https://storage/ops.pdf", "application/pdf");

        collection.Documents.Count.ShouldBe(1);
        collection.Documents.Single().DocumentId.ShouldBe(documentId);
    }

    [Fact]
    public void RemoveDocument_WhenPresent_ShouldReturnTrueAndRemove()
    {
        var collection = KnowledgeCollection.Create(Guid.NewGuid(), "Docs");
        var documentId = Guid.NewGuid();
        collection.AddDocument(documentId, "ops.pdf", "https://storage/ops.pdf", "application/pdf");

        var removed = collection.RemoveDocument(documentId);

        removed.ShouldBeTrue();
        collection.Documents.ShouldBeEmpty();
    }
}
