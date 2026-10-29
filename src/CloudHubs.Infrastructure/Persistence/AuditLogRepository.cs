using Microsoft.EntityFrameworkCore;
using CloudHubs.Application.Organizations;
using CloudHubs.Domain.Organizations;

namespace CloudHubs.Infrastructure.Persistence;

public class AuditLogRepository(CloudHubsDbContext dbContext) : IAuditLogRepository
{
    public async Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AuditLogEntry>> SearchAsync(
        string? query,
        Guid? organizationId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await Filter(query, organizationId, from, to)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        string? query,
        Guid? organizationId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default) =>
        Filter(query, organizationId, from, to).CountAsync(cancellationToken);

    private IQueryable<AuditLogEntry> Filter(
        string? query,
        Guid? organizationId,
        DateTime? from,
        DateTime? to)
    {
        var entries = dbContext.AuditLogEntries.AsNoTracking();
        if (organizationId.HasValue)
        {
            entries = entries.Where(entry => entry.OrganizationId == organizationId);
        }
        if (from.HasValue)
        {
            var start = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc);
            entries = entries.Where(entry => entry.OccurredAt >= start);
        }
        if (to.HasValue)
        {
            var end = DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc);
            entries = entries.Where(entry => entry.OccurredAt < end);
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            entries = entries.Where(entry =>
                EF.Functions.ILike(entry.ActorName, $"%{term}%")
                || EF.Functions.ILike(entry.ActorId, $"%{term}%")
                || EF.Functions.ILike(entry.Action, $"%{term}%")
                || EF.Functions.ILike(entry.ResourceType, $"%{term}%")
                || EF.Functions.ILike(entry.ResourceId, $"%{term}%")
                || EF.Functions.ILike(entry.Details, $"%{term}%"));
        }

        return entries;
    }
}
