using CloudHubs.Domain.Organizations;

namespace CloudHubs.Application.Organizations;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<AuditLogEntry>> SearchAsync(
        string? query,
        Guid? organizationId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<int> CountAsync(
        string? query,
        Guid? organizationId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);
}
