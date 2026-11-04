using CloudHubs.Domain.Common;

namespace CloudHubs.Domain.Entities;

public class AuditLogEntry : BaseEntity
{
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public Guid? OrganizationId { get; set; }
    public string Details { get; set; } = string.Empty;
}
