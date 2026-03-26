namespace nem.Mimir.Application.Common.Interfaces;

public interface IImageGenerationService
{
    Task<string?> GenerateAsync(
        string prompt,
        string? negativePrompt,
        string model,
        string size,
        CancellationToken cancellationToken = default);
}
