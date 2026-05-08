using FluentValidation;
using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Tenants;

namespace nem.Mimir.Application.Tenants.Commands;

/// <summary>
/// Soft-deletes a tenant and preserves its data for 30 days.
/// </summary>
public sealed record OffboardTenantCommand(Guid TenantId) : ICommand;

public sealed class OffboardTenantCommandValidator : AbstractValidator<OffboardTenantCommand>
{
    public OffboardTenantCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant id is required.");
    }
}

internal sealed class OffboardTenantCommandHandler(
    ITenantStore tenantStore,
    IDateTimeService dateTimeService) : IRequestHandler<OffboardTenantCommand>
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    public async Task Handle(OffboardTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantStore.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), request.TenantId);

        tenant.Offboard(dateTimeService.UtcNow, RetentionPeriod);
        await tenantStore.UpdateAsync(tenant, cancellationToken);
    }
}
