using CloudHubs.Domain.Common;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Domain.Entities;

public class ProjectIssue : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public string IssueKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IssueType IssueType { get; set; } = IssueType.Task;
    public IssueStatus Status { get; set; } = IssueStatus.ToDo;
    public IssuePriority Priority { get; set; } = IssuePriority.Medium;
    public Guid ReporterId { get; set; }
    public Guid? AssigneeId { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? SpentHours { get; set; }
    public int OrderIndex { get; set; }

    // Navigation
    public virtual Organization Organization { get; set; } = null!;
    public virtual Project Project { get; set; } = null!;
    public virtual User Reporter { get; set; } = null!;
    public virtual User? Assignee { get; set; }
    public virtual ICollection<ProjectIssueComment> Comments { get; set; } = [];
    public virtual ICollection<ProjectIssueActivity> Activities { get; set; } = [];
}

