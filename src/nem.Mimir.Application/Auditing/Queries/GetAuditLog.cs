using nem.Mimir.Application.Common.Mappings;
using FluentValidation;
using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Auditing.Queries;

/// <summary>
/// Query to retrieve paginated audit log entries with optional filtering.
/// </summary>
/// <param name="UserId">Optional user identifier to filter audit entries by.</param>
/// <param name="Action">Optional action name to filter audit entries by.</param>
/// <param name="From">Optional start date for the audit log date range filter.</param>
/// <param name="To">Optional end date for the audit log date range filter.</param>
/// <param name="PageNumber">The page number to retrieve (default 1).</param>
/// <param name="PageSize">The number of entries per page (default 20).</param>
public sealed record GetAuditLogQuery(
    UserId? UserId = null,
    string? Action = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PaginatedList<AuditEntryDto>>;

/// <summary>
/// Validates the <see cref="GetAuditLogQuery"/> ensuring pagination parameters are within acceptable ranges.
/// </summary>
public sealed class GetAuditLogQueryValidator : AbstractValidator<GetAuditLogQuery>
{
    public GetAuditLogQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

internal sealed class GetAuditLogQueryHandler : IRequestHandler<GetAuditLogQuery, PaginatedList<AuditEntryDto>>
{
    private readonly IAuditRepository _auditRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public GetAuditLogQueryHandler(
        IAuditRepository auditRepository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
    {
        _auditRepository = auditRepository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<PaginatedList<AuditEntryDto>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        EnsureAdminRole();

        var result = request.UserId.HasValue
            ? await _auditRepository.GetByUserIdAsync(request.UserId.Value, request.PageNumber, request.PageSize, cancellationToken)
            : await _auditRepository.GetAllAsync(request.PageNumber, request.PageSize, cancellationToken);

        var dtoItems = result.Items.Select(_mapper.MapToAuditEntryDto).ToList();

        return new PaginatedList<AuditEntryDto>(dtoItems, result.PageNumber, result.TotalPages, result.TotalCount);
    }

    private void EnsureAdminRole()
    {
        if (!_currentUserService.IsAuthenticated)
        {
            throw new ForbiddenAccessException("User is not authenticated.");
        }

        if (!_currentUserService.Roles.Contains("Admin"))
        {
            throw new ForbiddenAccessException("Only administrators can access the audit log.");
        }
    }
}
