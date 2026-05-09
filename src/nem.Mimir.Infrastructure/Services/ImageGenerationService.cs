namespace nem.Mimir.Infrastructure.Services;

using nem.Mimir.Application.Common.Interfaces;

internal sealed class ImageGenerationService : IImageGenerationService
{
    public Task<string?> GenerateAsync(
        string prompt,
        string? negativePrompt,
        string model,
        string size,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return Task.FromResult<string?>(null);

        var encodedPrompt = Uri.EscapeDataString(prompt.Trim());
        var imageUrl = $"https://mediahub.local/generated/{Guid.NewGuid():N}?model={Uri.EscapeDataString(model)}&size={Uri.EscapeDataString(size)}&prompt={encodedPrompt}";
        return Task.FromResult<string?>(imageUrl);
    }
}
