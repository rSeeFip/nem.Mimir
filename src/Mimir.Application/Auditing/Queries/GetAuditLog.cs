using AutoMapper;
using FluentValidation;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;

namespace Mimir.Application.Auditing.Queries;

public sealed record GetAuditLogQuery(
    Guid? UserId = null,
    string? Action = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PaginatedList<AuditEntryDto>>;

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
    private readonly IMapper _mapper;

    public GetAuditLogQueryHandler(
        IAuditRepository auditRepository,
        ICurrentUserService currentUserService,
        IMapper mapper)
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

        var dtoItems = _mapper.Map<IReadOnlyCollection<AuditEntryDto>>(result.Items);

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
