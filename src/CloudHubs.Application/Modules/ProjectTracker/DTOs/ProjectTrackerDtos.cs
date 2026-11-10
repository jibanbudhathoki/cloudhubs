using CloudHubs.Domain.Enums;

namespace CloudHubs.Application.Modules.ProjectTracker.DTOs;

public sealed class ProjectIssueDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string IssueKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IssueType IssueType { get; set; }
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
    public Guid ReporterId { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public string ReporterEmail { get; set; } = string.Empty;
    public Guid? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public string? AssigneeEmail { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? SpentHours { get; set; }
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int CommentsCount { get; set; }
}

public sealed class ProjectIssueDetailDto
{
    public ProjectIssueDto Issue { get; set; } = null!;
    public List<ProjectIssueCommentDto> Comments { get; set; } = [];
    public List<ProjectIssueActivityDto> Activities { get; set; } = [];
}

public sealed class ProjectIssueCommentDto
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string CommentText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class ProjectIssueActivityDto
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class CreateIssueRequest
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IssueType IssueType { get; set; } = IssueType.Task;
    public IssuePriority Priority { get; set; } = IssuePriority.Medium;
    public Guid? AssigneeId { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? EstimatedHours { get; set; }
}

public sealed class UpdateIssueRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IssueType IssueType { get; set; }
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
    public Guid? AssigneeId { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? SpentHours { get; set; }
}

public sealed class ProjectTrackerMetricsDto
{
    public int TotalIssues { get; set; }
    public int OpenIssues { get; set; }
    public int InProgressIssues { get; set; }
    public int InReviewIssues { get; set; }
    public int DoneIssues { get; set; }
    public int BugCount { get; set; }
    public decimal TotalEstimatedHours { get; set; }
    public decimal TotalSpentHours { get; set; }
    public int CompletionPercentage => TotalIssues == 0 ? 0 : (int)Math.Round((double)DoneIssues / TotalIssues * 100);
}

