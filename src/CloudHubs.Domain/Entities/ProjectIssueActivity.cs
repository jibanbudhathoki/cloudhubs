using CloudHubs.Domain.Common;

namespace CloudHubs.Domain.Entities;

public class ProjectIssueActivity : BaseEntity
{
    public Guid IssueId { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ProjectIssue Issue { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
