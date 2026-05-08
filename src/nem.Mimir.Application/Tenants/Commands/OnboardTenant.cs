using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Tenants.Dtos;
using nem.Mimir.Domain.Tenants;

namespace nem.Mimir.Application.Tenants.Commands;

/// <summary>
/// Creates a tenant with platform-default settings.
/// </summary>
public sealed record OnboardTenantCommand(string Name, string? Slug, int? DefaultRateLimit) : ICommand<TenantDto>;

public sealed class OnboardTenantCommandValidator : AbstractValidator<OnboardTenantCommand>
{
    private static readonly Regex SlugPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    public OnboardTenantCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tenant name is required.")
            .MaximumLength(200).WithMessage("Tenant name must not exceed 200 characters.");

        RuleFor(x => x.Slug)
            .Matches(SlugPattern)
            .When(x => !string.IsNullOrWhiteSpace(x.Slug))
            .WithMessage("Tenant slug must contain only lowercase letters, numbers, and hyphens.");

        RuleFor(x => x.DefaultRateLimit)
            .GreaterThan(0)
            .When(x => x.DefaultRateLimit.HasValue)
            .WithMessage("Default rate limit must be greater than zero.");
    }
}

internal sealed class OnboardTenantCommandHandler(
    ITenantStore tenantStore,
    IDateTimeService dateTimeService) : IRequestHandler<OnboardTenantCommand, TenantDto>
{
    private const int PlatformDefaultRateLimit = 100;

    public async Task<TenantDto> Handle(OnboardTenantCommand request, CancellationToken cancellationToken)
    {
        var baseSlug = string.IsNullOrWhiteSpace(request.Slug)
            ? Slugify(request.Name)
            : request.Slug.Trim().ToLowerInvariant();

        var slug = await EnsureUniqueSlugAsync(baseSlug, cancellationToken);
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
            Status = TenantStatus.Active,
            DefaultRateLimit = request.DefaultRateLimit ?? PlatformDefaultRateLimit,
            CreatedAt = dateTimeService.UtcNow,
        };

        await tenantStore.AddAsync(tenant, cancellationToken);
        return tenant.ToDto();
    }

    private async Task<string> EnsureUniqueSlugAsync(string baseSlug, CancellationToken cancellationToken)
    {
        var candidate = baseSlug;
        var suffix = 2;

        while (await tenantStore.ExistsBySlugAsync(candidate, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        var previousHyphen = false;

        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousHyphen = false;
                continue;
            }

            if (builder.Length == 0 || previousHyphen)
            {
                continue;
            }

            builder.Append('-');
            previousHyphen = true;
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "tenant" : slug;
    }
}
