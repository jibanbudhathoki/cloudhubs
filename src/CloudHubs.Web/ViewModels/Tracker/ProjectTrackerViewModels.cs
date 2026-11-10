using CloudHubs.Application.Modules.ProjectTracker.DTOs;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Web.ViewModels.Tracker;

public sealed class ProjectTrackerViewModel
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string OrganizationSlug { get; set; } = string.Empty;
    public string OrganizationRole { get; set; } = string.Empty;
    public string SubscriptionPlanName { get; set; } = "Basic";
    public bool IsProOrCustom { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public bool CanViewMembers { get; set; } = true;
    public bool CanViewProjects { get; set; } = true;
    public IReadOnlyCollection<Organization> Organizations { get; set; } = [];

    public IReadOnlyList<ProjectIssueDto> Issues { get; set; } = [];
    public ProjectTrackerMetricsDto Metrics { get; set; } = new();
    public IReadOnlyCollection<Project> Projects { get; set; } = [];
    public IReadOnlyCollection<User> OrganizationMembers { get; set; } = [];

    public Guid? SelectedProjectId { get; set; }
    public IssueStatus? SelectedStatus { get; set; }
    public IssuePriority? SelectedPriority { get; set; }
    public Guid? SelectedAssigneeId { get; set; }
    public string? Search { get; set; }
    public string ActiveView { get; set; } = "kanban"; // "kanban" or "list"
}

public sealed class CreateIssueFormModel
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

public sealed class UpdateIssueFormModel
{
    public Guid IssueId { get; set; }
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

public sealed class UpdateIssueStatusModel
{
    public Guid IssueId { get; set; }
    public IssueStatus Status { get; set; }
}

public sealed class AddIssueCommentModel
{
    public Guid IssueId { get; set; }
    public string CommentText { get; set; } = string.Empty;
}

