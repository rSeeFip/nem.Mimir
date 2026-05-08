using FluentValidation;
using MediatR;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Tenants;
using nem.Mimir.Domain.Tenants;

namespace nem.Mimir.Application.Tenants.Commands;

public sealed record UpdateTenantConfigurationCommand(
    string TenantId,
    int? RateLimitPerMinute,
    string[]? AllowedModels,
    string[]? AllowedTools,
    Dictionary<string, string>? FeatureFlags) : ICommand;

public sealed class UpdateTenantConfigurationCommandValidator : AbstractValidator<UpdateTenantConfigurationCommand>
{
    public UpdateTenantConfigurationCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required.");

        RuleFor(x => x.RateLimitPerMinute)
            .GreaterThan(0)
            .When(x => x.RateLimitPerMinute.HasValue)
            .WithMessage("RateLimitPerMinute must be greater than zero.");
    }
}

internal sealed class UpdateTenantConfigurationCommandHandler(
    ITenantConfigurationService tenantConfigurationService,
    IDateTimeService dateTimeService) : IRequestHandler<UpdateTenantConfigurationCommand>
{
    public async Task Handle(UpdateTenantConfigurationCommand request, CancellationToken cancellationToken)
    {
        var existing = await tenantConfigurationService.GetAsync(request.TenantId, cancellationToken);

        var configuration = existing ?? new TenantConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
        };

        if (request.RateLimitPerMinute.HasValue)
            configuration.RateLimitPerMinute = request.RateLimitPerMinute.Value;

        if (request.AllowedModels is not null)
            configuration.AllowedModels = request.AllowedModels;

        if (request.AllowedTools is not null)
            configuration.AllowedTools = request.AllowedTools;

        if (request.FeatureFlags is not null)
            configuration.FeatureFlags = request.FeatureFlags;

        configuration.UpdatedAt = dateTimeService.UtcNow;

        await tenantConfigurationService.UpdateAsync(configuration, cancellationToken);
    }
}
