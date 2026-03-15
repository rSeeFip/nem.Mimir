using FluentValidation;
using nem.Mimir.Api.Models.OpenAi;
using nem.Mimir.Api.Validators;

namespace nem.Mimir.Api.Models.OpenAi;

/// <summary>
/// Validates <see cref="ChatCompletionRequest"/> for the OpenAI-compatible endpoint.
/// Enforces chat-specific rules: model name format, message limits, per-message length limits,
/// and prompt injection guards.
/// </summary>
public sealed class ChatCompletionRequestValidator : AbstractValidator<ChatCompletionRequest>
{
    /// <summary>Maximum total number of messages in a single completion request.</summary>
    private const int MaxMessageCount = 500;

    /// <summary>Maximum character length per individual message.</summary>
    private const int MaxMessageContentLength = 100_000;

    /// <summary>Valid roles for OpenAI-protocol messages.</summary>
    private static readonly string[] ValidRoles = ["system", "user", "assistant", "tool", "function"];

    public ChatCompletionRequestValidator()
    {
        // Model is required and must be a valid identifier
        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Model is required.")
            .MaximumLength(200).WithMessage("Model identifier must not exceed 200 characters.")
            .ValidModelName();

        // Messages must be a non-empty list within count bounds
        RuleFor(x => x.Messages)
            .NotNull().WithMessage("Messages must not be null.")
            .NotEmpty().WithMessage("Messages must contain at least one message.")
            .Must(msgs => msgs is null || msgs.Count <= MaxMessageCount)
            .WithMessage($"Messages must not exceed {MaxMessageCount} entries.");

        // Each message must have a valid role and non-empty content within the length limit
        RuleForEach(x => x.Messages)
            .ChildRules(msg =>
            {
                msg.RuleFor(m => m.Role)
                    .NotEmpty().WithMessage("Message role is required.")
                    .Must(role => ValidRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                    .WithMessage($"Message role must be one of: {string.Join(", ", ValidRoles)}.");

                msg.RuleFor(m => m.Content)
                    .NotNull().WithMessage("Message content must not be null.")
                    .MaximumLength(MaxMessageContentLength)
                        .WithMessage($"Each message content must not exceed {MaxMessageContentLength:N0} characters.")
                    .NoPromptInjection()
                        .When(m => !string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase));
            });

        // Optional numeric parameters must be within OpenAI-specified ranges
        RuleFor(x => x.Temperature)
            .InclusiveBetween(0.0, 2.0).WithMessage("Temperature must be between 0.0 and 2.0.")
            .When(x => x.Temperature.HasValue);

        RuleFor(x => x.TopP)
            .InclusiveBetween(0.0, 1.0).WithMessage("TopP must be between 0.0 and 1.0.")
            .When(x => x.TopP.HasValue);

        RuleFor(x => x.FrequencyPenalty)
            .InclusiveBetween(-2.0, 2.0).WithMessage("FrequencyPenalty must be between -2.0 and 2.0.")
            .When(x => x.FrequencyPenalty.HasValue);

        RuleFor(x => x.PresencePenalty)
            .InclusiveBetween(-2.0, 2.0).WithMessage("PresencePenalty must be between -2.0 and 2.0.")
            .When(x => x.PresencePenalty.HasValue);

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0).WithMessage("MaxTokens must be a positive integer.")
            .When(x => x.MaxTokens.HasValue);
    }
}
