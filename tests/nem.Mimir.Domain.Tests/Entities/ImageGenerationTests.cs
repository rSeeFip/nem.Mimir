using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public sealed class ImageGenerationTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_ImageGeneration()
    {
        var userId = Guid.NewGuid();

        var imageGeneration = ImageGeneration.Create(
            userId,
            "A scenic mountain at sunset",
            "blurry",
            "dall-e-3",
            "1024x1024",
            "standard",
            1);

        imageGeneration.UserId.ShouldBe(userId);
        imageGeneration.Prompt.ShouldBe("A scenic mountain at sunset");
        imageGeneration.NegativePrompt.ShouldBe("blurry");
        imageGeneration.Model.ShouldBe("dall-e-3");
        imageGeneration.Size.ShouldBe("1024x1024");
        imageGeneration.Quality.ShouldBe("standard");
        imageGeneration.NumberOfImages.ShouldBe(1);
        imageGeneration.Status.ShouldBe(ImageGenerationStatus.Pending);
    }

    [Fact]
    public void MarkProcessing_Should_Set_Status_To_Processing()
    {
        var imageGeneration = ImageGeneration.Create(
            Guid.NewGuid(),
            "A robot in a library",
            null,
            "dall-e-3",
            "1024x1024",
            null,
            1);

        imageGeneration.MarkProcessing();

        imageGeneration.Status.ShouldBe(ImageGenerationStatus.Processing);
    }

    [Fact]
    public void MarkCompleted_Should_Set_Status_Url_And_CompletedAt()
    {
        var imageGeneration = ImageGeneration.Create(
            Guid.NewGuid(),
            "A watercolor cityscape",
            null,
            "dall-e-3",
            "1024x1024",
            null,
            1);

        imageGeneration.MarkCompleted("https://cdn.example.com/image.png");

        imageGeneration.Status.ShouldBe(ImageGenerationStatus.Completed);
        imageGeneration.ImageUrl.ShouldBe("https://cdn.example.com/image.png");
        imageGeneration.ErrorMessage.ShouldBeNull();
        imageGeneration.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public void MarkFailed_Should_Set_Status_Error_And_CompletedAt()
    {
        var imageGeneration = ImageGeneration.Create(
            Guid.NewGuid(),
            "A futuristic skyline",
            null,
            "dall-e-3",
            "1024x1024",
            null,
            1);

        imageGeneration.MarkFailed("Inference timeout");

        imageGeneration.Status.ShouldBe(ImageGenerationStatus.Failed);
        imageGeneration.ErrorMessage.ShouldBe("Inference timeout");
        imageGeneration.CompletedAt.ShouldNotBeNull();
    }
}
