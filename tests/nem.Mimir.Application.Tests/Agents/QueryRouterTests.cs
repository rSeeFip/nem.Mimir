using nem.Mimir.Application.Agents;
using Shouldly;

namespace nem.Mimir.Application.Tests.Agents;

public sealed class QueryRouterTests
{
    private readonly QueryRouter _router = new();

    [Fact]
    public void AnalyzeIntent_KnowledgeQuery_ShouldRouteToKnowHub()
    {
        var result = _router.AnalyzeIntent("Search for documentation about the knowledge base");

        result.Category.ShouldBe("knowledge");
        result.SuggestedService.ShouldBe("KnowHub");
        result.Confidence.ShouldBeGreaterThan(0.0);
    }

    [Fact]
    public void AnalyzeIntent_AssetQuery_ShouldRouteToAssetCore()
    {
        var result = _router.AnalyzeIntent("Upload this file to asset storage");

        result.Category.ShouldBe("assets");
        result.SuggestedService.ShouldBe("AssetCore");
        result.Confidence.ShouldBeGreaterThanOrEqualTo(0.3);
    }

    [Fact]
    public void AnalyzeIntent_MediaQuery_ShouldRouteToMediaHub()
    {
        var result = _router.AnalyzeIntent("Play the video stream and transcode the audio");

        result.Category.ShouldBe("media");
        result.SuggestedService.ShouldBe("MediaHub");
        result.Confidence.ShouldBeGreaterThanOrEqualTo(0.3);
    }

    [Fact]
    public void AnalyzeIntent_CodeQuery_ShouldRouteToMimir()
    {
        var result = _router.AnalyzeIntent("Execute this code script and run the build");

        result.Category.ShouldBe("code");
        result.SuggestedService.ShouldBe("Mimir");
    }

    [Fact]
    public void AnalyzeIntent_GeneralQuery_ShouldRouteToMimir()
    {
        var result = _router.AnalyzeIntent("Hello, can you help me explain something?");

        result.Category.ShouldBe("general");
        result.SuggestedService.ShouldBe("Mimir");
    }

    [Fact]
    public void AnalyzeIntent_NoKeywordsMatched_ShouldDefaultToGeneral()
    {
        var result = _router.AnalyzeIntent("xyzzy foobar baz");

        result.Category.ShouldBe("general");
        result.Confidence.ShouldBe(0.5);
        result.SuggestedService.ShouldBe("Mimir");
    }

    [Fact]
    public void AnalyzeIntent_EmptyQuery_ShouldThrowArgumentException()
    {
        Should.Throw<ArgumentException>(() => _router.AnalyzeIntent(""));
    }

    [Fact]
    public void AnalyzeIntent_NullQuery_ShouldThrowArgumentException()
    {
        Should.Throw<ArgumentNullException>(() => _router.AnalyzeIntent(null!));
    }

    [Fact]
    public void AnalyzeIntent_ConfidenceClampedToRange()
    {
        // A query with a single keyword in one category should still be >= 0.3
        var result = _router.AnalyzeIntent("find");

        result.Confidence.ShouldBeGreaterThanOrEqualTo(0.3);
        result.Confidence.ShouldBeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void GetServiceForCategory_KnownCategory_ReturnsCorrectService()
    {
        QueryRouter.GetServiceForCategory("knowledge").ShouldBe("KnowHub");
        QueryRouter.GetServiceForCategory("assets").ShouldBe("AssetCore");
        QueryRouter.GetServiceForCategory("media").ShouldBe("MediaHub");
        QueryRouter.GetServiceForCategory("code").ShouldBe("Mimir");
        QueryRouter.GetServiceForCategory("general").ShouldBe("Mimir");
    }

    [Fact]
    public void GetServiceForCategory_UnknownCategory_DefaultsToMimir()
    {
        QueryRouter.GetServiceForCategory("nonexistent").ShouldBe("Mimir");
    }

    [Fact]
    public void GetKnownCategories_ReturnsAllCategories()
    {
        var categories = QueryRouter.GetKnownCategories();

        categories.ShouldContain("knowledge");
        categories.ShouldContain("assets");
        categories.ShouldContain("media");
        categories.ShouldContain("code");
        categories.ShouldContain("general");
        categories.Count.ShouldBe(5);
    }
}
