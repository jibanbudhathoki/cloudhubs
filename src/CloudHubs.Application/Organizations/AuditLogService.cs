using CloudHubs.Domain.Organizations;

namespace CloudHubs.Application.Organizations;

public class AuditLogService(IAuditLogRepository repository)
{
    public Task RecordAsync(
        string actorId,
        string actorName,
        string action,
        string resourceType,
        string resourceId,
        Guid? organizationId,
        string details,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(action) || action.Length > 100)
            throw new ArgumentException("Audit action is required and must be 100 characters or fewer.", nameof(action));
        if (string.IsNullOrWhiteSpace(resourceType) || resourceType.Length > 100)
            throw new ArgumentException("Audit resource type is required and must be 100 characters or fewer.", nameof(resourceType));

        return repository.AddAsync(new AuditLogEntry
        {
            ActorId = Limit(actorId, 200),
            ActorName = Limit(actorName, 200),
            Action = action.Trim(),
            ResourceType = resourceType.Trim(),
            ResourceId = Limit(resourceId, 200),
            OrganizationId = organizationId,
            Details = Limit(details, 2000)
        }, cancellationToken);
    }

    public Task<IReadOnlyCollection<AuditLogEntry>> SearchAsync(
        string? query,
        Guid? organizationId,
        DateTime? from,
        DateTime? to,
        int page,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (from.HasValue && to.HasValue && from > to)
            throw new ArgumentException("The start date must be on or before the end date.", nameof(from));
        var normalizedQuery = query?.Trim();
        if (normalizedQuery?.Length > 200)
            throw new ArgumentException("Audit search must be 200 characters or fewer.", nameof(query));
        return repository.SearchAsync(normalizedQuery, organizationId, from, to, page, 50, cancellationToken);
    }

    public Task<int> CountAsync(
        string? query,
        Guid? organizationId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default) =>
        repository.CountAsync(query?.Trim(), organizationId, from, to, cancellationToken);

    private static string Limit(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];
}
