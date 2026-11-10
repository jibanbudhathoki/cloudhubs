using CloudHubs.Application.Modules.ProjectTracker.DTOs;
using CloudHubs.Application.Modules.ProjectTracker.Interfaces;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CloudHubs.Infrastructure.Modules.ProjectTracker;

public sealed class ProjectTrackerService(CloudHubsDbContext dbContext) : IProjectTrackerService
{
    public async Task<IReadOnlyList<ProjectIssueDto>> GetIssuesAsync(
        Guid organizationId,
        Guid? projectId = null,
        IssueStatus? status = null,
        IssuePriority? priority = null,
        Guid? assigneeId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ProjectIssues
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId);

        if (projectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == projectId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(x => x.Priority == priority.Value);
        }

        if (assigneeId.HasValue)
        {
            query = query.Where(x => x.AssigneeId == assigneeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(s) ||
                                     x.IssueKey.ToLower().Contains(s) ||
                                     (x.Description != null && x.Description.ToLower().Contains(s)));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProjectIssueDto
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                ProjectId = x.ProjectId,
                ProjectName = x.Project.Name,
                IssueKey = x.IssueKey,
                Title = x.Title,
                Description = x.Description,
                IssueType = x.IssueType,
                Status = x.Status,
                Priority = x.Priority,
                ReporterId = x.ReporterId,
                ReporterName = !string.IsNullOrEmpty(x.Reporter.Name) ? x.Reporter.Name : (x.Reporter.Email ?? "User"),
                ReporterEmail = x.Reporter.Email ?? string.Empty,
                AssigneeId = x.AssigneeId,
                AssigneeName = x.Assignee != null ? (!string.IsNullOrEmpty(x.Assignee.Name) ? x.Assignee.Name : (x.Assignee.Email ?? "User")) : null,
                AssigneeEmail = x.Assignee != null ? (x.Assignee.Email ?? string.Empty) : null,
                DueDate = x.DueDate,
                EstimatedHours = x.EstimatedHours,
                SpentHours = x.SpentHours,
                OrderIndex = x.OrderIndex,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                CommentsCount = x.Comments.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectIssueDetailDto?> GetIssueDetailAsync(
        Guid organizationId,
        Guid issueId,
        CancellationToken cancellationToken = default)
    {
        var issue = await dbContext.ProjectIssues
            .AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.Reporter)
            .Include(x => x.Assignee)
            .Include(x => x.Comments).ThenInclude(c => c.User)
            .Include(x => x.Activities).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == issueId, cancellationToken);

        if (issue == null) return null;

        var dto = new ProjectIssueDto
        {
            Id = issue.Id,
            OrganizationId = issue.OrganizationId,
            ProjectId = issue.ProjectId,
            ProjectName = issue.Project.Name,
            IssueKey = issue.IssueKey,
            Title = issue.Title,
            Description = issue.Description,
            IssueType = issue.IssueType,
            Status = issue.Status,
            Priority = issue.Priority,
            ReporterId = issue.ReporterId,
            ReporterName = !string.IsNullOrEmpty(issue.Reporter.Name) ? issue.Reporter.Name : (issue.Reporter.Email ?? "User"),
            ReporterEmail = issue.Reporter.Email ?? string.Empty,
            AssigneeId = issue.AssigneeId,
            AssigneeName = issue.Assignee != null ? (!string.IsNullOrEmpty(issue.Assignee.Name) ? issue.Assignee.Name : (issue.Assignee.Email ?? "User")) : null,
            AssigneeEmail = issue.Assignee != null ? (issue.Assignee.Email ?? string.Empty) : null,
            DueDate = issue.DueDate,
            EstimatedHours = issue.EstimatedHours,
            SpentHours = issue.SpentHours,
            OrderIndex = issue.OrderIndex,
            CreatedAt = issue.CreatedAt,
            UpdatedAt = issue.UpdatedAt,
            CommentsCount = issue.Comments.Count
        };

        var comments = issue.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c => new ProjectIssueCommentDto
            {
                Id = c.Id,
                IssueId = c.IssueId,
                UserId = c.UserId,
                UserName = !string.IsNullOrEmpty(c.User.Name) ? c.User.Name : (c.User.Email ?? "User"),
                UserEmail = c.User.Email ?? string.Empty,
                CommentText = c.CommentText,
                CreatedAt = c.CreatedAt
            })
            .ToList();

        var activities = issue.Activities
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ProjectIssueActivityDto
            {
                Id = a.Id,
                IssueId = a.IssueId,
                UserId = a.UserId,
                UserName = !string.IsNullOrEmpty(a.User.Name) ? a.User.Name : (a.User.Email ?? "User"),
                Action = a.Action,
                Details = a.Details,
                CreatedAt = a.CreatedAt
            })
            .ToList();

        return new ProjectIssueDetailDto
        {
            Issue = dto,
            Comments = comments,
            Activities = activities
        };
    }

    public async Task<ProjectIssueDto> CreateIssueAsync(
        Guid organizationId,
        Guid reporterId,
        CreateIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.OrganizationId == organizationId, cancellationToken)
            ?? throw new InvalidOperationException("Project not found in current organization.");

        var reporter = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == reporterId, cancellationToken)
            ?? throw new InvalidOperationException("Reporter user not found.");

        // Generate Prefix & Key
        var words = project.Name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var prefix = words.Length >= 2
            ? $"{char.ToUpper(words[0][0])}{char.ToUpper(words[1][0])}"
            : (project.Name.Length >= 3 ? project.Name[..3].ToUpperInvariant() : "PRJ");

        var issueCount = await dbContext.ProjectIssues
            .CountAsync(x => x.ProjectId == request.ProjectId, cancellationToken);
        var issueKey = $"{prefix}-{issueCount + 1}";

        var issue = new ProjectIssue
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ProjectId = request.ProjectId,
            IssueKey = issueKey,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            IssueType = request.IssueType,
            Status = IssueStatus.ToDo,
            Priority = request.Priority,
            ReporterId = reporterId,
            AssigneeId = request.AssigneeId,
            DueDate = request.DueDate,
            EstimatedHours = request.EstimatedHours,
            SpentHours = 0,
            OrderIndex = issueCount,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.ProjectIssues.Add(issue);

        var reporterName = !string.IsNullOrEmpty(reporter.Name) ? reporter.Name : (reporter.Email ?? "User");
        var activity = new ProjectIssueActivity
        {
            Id = Guid.NewGuid(),
            IssueId = issue.Id,
            UserId = reporterId,
            Action = "Created",
            Details = $"Ticket {issueKey} created by {reporterName}.",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ProjectIssueActivities.Add(activity);

        await dbContext.SaveChangesAsync(cancellationToken);

        string? assigneeName = null;
        string? assigneeEmail = null;
        if (request.AssigneeId.HasValue)
        {
            var assignee = await dbContext.Users.FindAsync([request.AssigneeId.Value], cancellationToken);
            assigneeName = assignee != null ? (!string.IsNullOrEmpty(assignee.Name) ? assignee.Name : assignee.Email) : null;
            assigneeEmail = assignee?.Email;
        }

        return new ProjectIssueDto
        {
            Id = issue.Id,
            OrganizationId = issue.OrganizationId,
            ProjectId = issue.ProjectId,
            ProjectName = project.Name,
            IssueKey = issue.IssueKey,
            Title = issue.Title,
            Description = issue.Description,
            IssueType = issue.IssueType,
            Status = issue.Status,
            Priority = issue.Priority,
            ReporterId = issue.ReporterId,
            ReporterName = reporterName,
            ReporterEmail = reporter.Email ?? string.Empty,
            AssigneeId = issue.AssigneeId,
            AssigneeName = assigneeName,
            AssigneeEmail = assigneeEmail,
            DueDate = issue.DueDate,
            EstimatedHours = issue.EstimatedHours,
            SpentHours = issue.SpentHours,
            OrderIndex = issue.OrderIndex,
            CreatedAt = issue.CreatedAt,
            UpdatedAt = issue.UpdatedAt,
            CommentsCount = 0
        };
    }

    public async Task<ProjectIssueDto> UpdateIssueAsync(
        Guid organizationId,
        Guid issueId,
        Guid userId,
        UpdateIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        var issue = await dbContext.ProjectIssues
            .Include(x => x.Project)
            .Include(x => x.Reporter)
            .Include(x => x.Assignee)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == issueId, cancellationToken)
            ?? throw new InvalidOperationException("Issue not found.");

        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        var actorName = user != null ? (!string.IsNullOrEmpty(user.Name) ? user.Name : (user.Email ?? "User")) : "User";

        var changes = new List<string>();
        if (issue.Title != request.Title.Trim()) changes.Add("Title changed");
        if (issue.Status != request.Status) changes.Add($"Status changed from {issue.Status} to {request.Status}");
        if (issue.Priority != request.Priority) changes.Add($"Priority changed to {request.Priority}");
        if (issue.AssigneeId != request.AssigneeId) changes.Add("Assignee updated");
        if (issue.SpentHours != request.SpentHours) changes.Add($"Hours logged ({request.SpentHours ?? 0}h spent)");

        issue.Title = request.Title.Trim();
        issue.Description = request.Description?.Trim();
        issue.IssueType = request.IssueType;
        issue.Status = request.Status;
        issue.Priority = request.Priority;
        issue.AssigneeId = request.AssigneeId;
        issue.DueDate = request.DueDate;
        issue.EstimatedHours = request.EstimatedHours;
        issue.SpentHours = request.SpentHours;
        issue.UpdatedAt = DateTime.UtcNow;

        if (changes.Count != 0)
        {
            var activity = new ProjectIssueActivity
            {
                Id = Guid.NewGuid(),
                IssueId = issue.Id,
                UserId = userId,
                Action = "Updated",
                Details = $"{string.Join(", ", changes)} by {actorName}.",
                CreatedAt = DateTime.UtcNow
            };
            dbContext.ProjectIssueActivities.Add(activity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        string? assigneeName = null;
        string? assigneeEmail = null;
        if (issue.AssigneeId.HasValue)
        {
            var assignee = await dbContext.Users.FindAsync([issue.AssigneeId.Value], cancellationToken);
            assigneeName = assignee != null ? (!string.IsNullOrEmpty(assignee.Name) ? assignee.Name : assignee.Email) : null;
            assigneeEmail = assignee?.Email;
        }

        var commentsCount = await dbContext.ProjectIssueComments.CountAsync(c => c.IssueId == issue.Id, cancellationToken);

        return new ProjectIssueDto
        {
            Id = issue.Id,
            OrganizationId = issue.OrganizationId,
            ProjectId = issue.ProjectId,
            ProjectName = issue.Project.Name,
            IssueKey = issue.IssueKey,
            Title = issue.Title,
            Description = issue.Description,
            IssueType = issue.IssueType,
            Status = issue.Status,
            Priority = issue.Priority,
            ReporterId = issue.ReporterId,
            ReporterName = !string.IsNullOrEmpty(issue.Reporter.Name) ? issue.Reporter.Name : (issue.Reporter.Email ?? "User"),
            ReporterEmail = issue.Reporter.Email ?? string.Empty,
            AssigneeId = issue.AssigneeId,
            AssigneeName = assigneeName,
            AssigneeEmail = assigneeEmail,
            DueDate = issue.DueDate,
            EstimatedHours = issue.EstimatedHours,
            SpentHours = issue.SpentHours,
            OrderIndex = issue.OrderIndex,
            CreatedAt = issue.CreatedAt,
            UpdatedAt = issue.UpdatedAt,
            CommentsCount = commentsCount
        };
    }

    public async Task<bool> UpdateIssueStatusAsync(
        Guid organizationId,
        Guid issueId,
        Guid userId,
        IssueStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var issue = await dbContext.ProjectIssues
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == issueId, cancellationToken);

        if (issue == null) return false;

        var oldStatus = issue.Status;
        if (oldStatus == newStatus) return true;

        issue.Status = newStatus;
        issue.UpdatedAt = DateTime.UtcNow;

        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        var actorName = user != null ? (!string.IsNullOrEmpty(user.Name) ? user.Name : (user.Email ?? "User")) : "User";

        var activity = new ProjectIssueActivity
        {
            Id = Guid.NewGuid(),
            IssueId = issue.Id,
            UserId = userId,
            Action = "Status Moved",
            Details = $"Moved from {oldStatus} to {newStatus} by {actorName}.",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ProjectIssueActivities.Add(activity);

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ProjectIssueCommentDto> AddCommentAsync(
        Guid organizationId,
        Guid issueId,
        Guid userId,
        string commentText,
        CancellationToken cancellationToken = default)
    {
        var issue = await dbContext.ProjectIssues
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == issueId, cancellationToken)
            ?? throw new InvalidOperationException("Issue not found.");

        var user = await dbContext.Users.FindAsync([userId], cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var comment = new ProjectIssueComment
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            UserId = userId,
            CommentText = commentText.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.ProjectIssueComments.Add(comment);

        var userName = !string.IsNullOrEmpty(user.Name) ? user.Name : (user.Email ?? "User");
        var activity = new ProjectIssueActivity
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            UserId = userId,
            Action = "Comment Added",
            Details = $"{userName} commented on this ticket.",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ProjectIssueActivities.Add(activity);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProjectIssueCommentDto
        {
            Id = comment.Id,
            IssueId = comment.IssueId,
            UserId = comment.UserId,
            UserName = userName,
            UserEmail = user.Email ?? string.Empty,
            CommentText = comment.CommentText,
            CreatedAt = comment.CreatedAt
        };
    }

    public async Task<bool> DeleteIssueAsync(
        Guid organizationId,
        Guid issueId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var issue = await dbContext.ProjectIssues
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == issueId, cancellationToken);

        if (issue == null) return false;

        dbContext.ProjectIssues.Remove(issue);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ProjectTrackerMetricsDto> GetMetricsAsync(
        Guid organizationId,
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ProjectIssues
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId);

        if (projectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == projectId.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var backlog = await query.CountAsync(x => x.Status == IssueStatus.Backlog, cancellationToken);
        var todo = await query.CountAsync(x => x.Status == IssueStatus.ToDo, cancellationToken);
        var inProgress = await query.CountAsync(x => x.Status == IssueStatus.InProgress, cancellationToken);
        var inReview = await query.CountAsync(x => x.Status == IssueStatus.InReview, cancellationToken);
        var done = await query.CountAsync(x => x.Status == IssueStatus.Done, cancellationToken);
        var bugs = await query.CountAsync(x => x.IssueType == IssueType.Bug, cancellationToken);

        var estHours = await query.SumAsync(x => x.EstimatedHours ?? 0, cancellationToken);
        var spentHours = await query.SumAsync(x => x.SpentHours ?? 0, cancellationToken);

        return new ProjectTrackerMetricsDto
        {
            TotalIssues = total,
            OpenIssues = backlog + todo,
            InProgressIssues = inProgress,
            InReviewIssues = inReview,
            DoneIssues = done,
            BugCount = bugs,
            TotalEstimatedHours = estHours,
            TotalSpentHours = spentHours
        };
    }
}

