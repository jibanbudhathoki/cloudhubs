using CloudHubs.Application.Modules.ProjectTracker.DTOs;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Application.Modules.ProjectTracker.Interfaces;

public interface IProjectTrackerService
{
    Task<IReadOnlyList<ProjectIssueDto>> GetIssuesAsync(
        Guid organizationId,
        Guid? projectId = null,
        IssueStatus? status = null,
        IssuePriority? priority = null,
        Guid? assigneeId = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<ProjectIssueDetailDto?> GetIssueDetailAsync(
        Guid organizationId,
        Guid issueId,
        CancellationToken cancellationToken = default);

    Task<ProjectIssueDto> CreateIssueAsync(
        Guid organizationId,
        Guid reporterId,
        CreateIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectIssueDto> UpdateIssueAsync(
        Guid organizationId,
        Guid issueId,
        Guid userId,
        UpdateIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateIssueStatusAsync(
        Guid organizationId,
        Guid issueId,
        Guid userId,
        IssueStatus newStatus,
        CancellationToken cancellationToken = default);

    Task<ProjectIssueCommentDto> AddCommentAsync(
        Guid organizationId,
        Guid issueId,
        Guid userId,
        string commentText,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteIssueAsync(
        Guid organizationId,
        Guid issueId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ProjectTrackerMetricsDto> GetMetricsAsync(
        Guid organizationId,
        Guid? projectId = null,
        CancellationToken cancellationToken = default);
}

