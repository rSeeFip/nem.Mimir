namespace nem.Mimir.Domain.Entities;

using ImageGenerationId = nem.Contracts.Identity.ImageGenerationId;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;

public sealed class ImageGeneration : BaseAuditableEntity<ImageGenerationId>
{
    public Guid UserId { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public string? NegativePrompt { get; private set; }
    public string Model { get; private set; } = string.Empty;
    public string Size { get; private set; } = string.Empty;
    public string? Quality { get; private set; }
    public int NumberOfImages { get; private set; }
    public ImageGenerationStatus Status { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private ImageGeneration() { }

    public static ImageGeneration Create(
        Guid userId,
        string prompt,
        string? negativePrompt,
        string model,
        string size,
        string? quality,
        int numberOfImages = 1)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model cannot be empty.", nameof(model));

        if (string.IsNullOrWhiteSpace(size))
            throw new ArgumentException("Size cannot be empty.", nameof(size));

        if (numberOfImages <= 0)
            throw new ArgumentOutOfRangeException(nameof(numberOfImages), numberOfImages, "Number of images must be greater than zero.");

        return new ImageGeneration
        {
            Id = ImageGenerationId.New(),
            UserId = userId,
            Prompt = prompt.Trim(),
            NegativePrompt = string.IsNullOrWhiteSpace(negativePrompt) ? null : negativePrompt.Trim(),
            Model = model.Trim(),
            Size = size.Trim(),
            Quality = string.IsNullOrWhiteSpace(quality) ? null : quality.Trim(),
            NumberOfImages = numberOfImages,
            Status = ImageGenerationStatus.Pending,
        };
    }

    public void MarkProcessing()
    {
        if (Status == ImageGenerationStatus.Completed)
            throw new InvalidOperationException("Completed image generation cannot transition to processing.");

        Status = ImageGenerationStatus.Processing;
    }

    public void MarkCompleted(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new ArgumentException("Image URL cannot be empty.", nameof(imageUrl));

        Status = ImageGenerationStatus.Completed;
        ImageUrl = imageUrl.Trim();
        ErrorMessage = null;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty.", nameof(errorMessage));

        Status = ImageGenerationStatus.Failed;
        ErrorMessage = errorMessage.Trim();
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
