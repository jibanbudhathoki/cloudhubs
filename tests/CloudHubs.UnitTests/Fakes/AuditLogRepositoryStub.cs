using CloudHubs.Application.Modules.Platform.Interfaces;
using CloudHubs.Domain.Entities;

namespace CloudHubs.UnitTests.Fakes;

internal sealed class AuditLogRepositoryStub : IAuditLogRepository
{
    public AuditLogEntry? AddedEntry { get; private set; }
    public Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        AddedEntry = entry;
        return Task.CompletedTask;
    }
    public Task<IReadOnlyCollection<AuditLogEntry>> SearchAsync(
        string? query,
        Guid? organizationId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<AuditLogEntry>>([]);
    public Task<int> CountAsync(
        string? query,
        Guid? organizationId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default) => Task.FromResult(0);
}
