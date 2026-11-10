using CloudHubs.Application.Modules.ProjectTracker.DTOs;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;

namespace CloudHubs.UnitTests.Domain;

public class ProjectTrackerDomainTests
{
    [Fact]
    public void ProjectIssue_Defaults_AreCorrect()
    {
        var issue = new ProjectIssue();

        Assert.Equal(IssueType.Task, issue.IssueType);
        Assert.Equal(IssueStatus.ToDo, issue.Status);
        Assert.Equal(IssuePriority.Medium, issue.Priority);
        Assert.Empty(issue.Comments);
        Assert.Empty(issue.Activities);
    }

    [Fact]
    public void ProjectTrackerMetricsDto_CompletionPercentage_CalculatesCorrectly()
    {
        var emptyMetrics = new ProjectTrackerMetricsDto
        {
            TotalIssues = 0,
            DoneIssues = 0
        };
        Assert.Equal(0, emptyMetrics.CompletionPercentage);

        var partialMetrics = new ProjectTrackerMetricsDto
        {
            TotalIssues = 4,
            DoneIssues = 2
        };
        Assert.Equal(50, partialMetrics.CompletionPercentage);

        var roundedMetrics = new ProjectTrackerMetricsDto
        {
            TotalIssues = 3,
            DoneIssues = 1
        };
        Assert.Equal(33, roundedMetrics.CompletionPercentage);

        var fullMetrics = new ProjectTrackerMetricsDto
        {
            TotalIssues = 5,
            DoneIssues = 5
        };
        Assert.Equal(100, fullMetrics.CompletionPercentage);
    }

    [Fact]
    public void ProjectIssue_CanAddCommentsAndActivities()
    {
        var issue = new ProjectIssue
        {
            Id = Guid.NewGuid(),
            Title = "Implement OAuth Refresh",
            IssueKey = "RMS-1"
        };

        var comment = new ProjectIssueComment
        {
            Id = Guid.NewGuid(),
            IssueId = issue.Id,
            CommentText = "Testing OAuth callback"
        };
        issue.Comments.Add(comment);

        var activity = new ProjectIssueActivity
        {
            Id = Guid.NewGuid(),
            IssueId = issue.Id,
            Action = "Status Moved",
            Details = "Moved from ToDo to InProgress"
        };
        issue.Activities.Add(activity);

        Assert.Single(issue.Comments);
        Assert.Single(issue.Activities);
        Assert.Equal("RMS-1", issue.IssueKey);
    }
}

