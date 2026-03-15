using Microsoft.AspNetCore.Http;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Infrastructure.Services;

internal sealed class AuditService : IAuditService
{
    private readonly IAuditRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        IAuditRepository auditRepository,
        IUnitOfWork unitOfWork,
        IHttpContextAccessor httpContextAccessor)
    {
        _auditRepository = auditRepository;
        _unitOfWork = unitOfWork;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        UserId userId,
        string action,
        string entityType,
        string? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

        var auditEntry = AuditEntry.Create(userId, action, entityType, entityId, details, ipAddress);

        await _auditRepository.CreateAsync(auditEntry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
