using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using nem.Mimir.Tui.Models;

namespace nem.Mimir.Tui.Services;

/// <summary>
/// Handles REST API calls for model management.
/// </summary>
internal sealed class ModelService
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationService _authService;
    private string _currentModel;

    public ModelService(HttpClient httpClient, AuthenticationService authService, IOptions<TuiSettings> options)
    {
        _httpClient = httpClient;
        _authService = authService;
        _currentModel = options.Value.DefaultModel;
    }

    /// <summary>
    /// Gets or sets the currently selected model.
    /// </summary>
    public string CurrentModel
    {
        get => _currentModel;
        set => _currentModel = value;
    }

    /// <summary>
    /// Retrieves available models from the API.
    /// </summary>
    public async Task<IReadOnlyList<ModelInfoDto>?> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        ConfigureAuth();
        var response = await _httpClient.GetAsync("api/models", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ModelInfoDto>>(
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sets the current model if it's in the allowed list.
    /// </summary>
    /// <param name="modelName">The model name to select.</param>
    /// <returns>True if the model was recognized and set.</returns>
    public bool TrySetModel(string modelName)
    {
        var normalized = modelName.Trim().ToLowerInvariant();
        if (IsKnownModel(normalized))
        {
            _currentModel = normalized;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a model name is a known/allowed model.
    /// </summary>
    internal static bool IsKnownModel(string modelName) => modelName switch
    {
        "phi-4-mini" => true,
        "qwen-2.5-72b" => true,
        "qwen-2.5-coder-32b" => true,
        _ => false,
    };

    private void ConfigureAuth()
    {
        if (_authService.AccessToken is not null)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _authService.AccessToken);
        }
    }
}
