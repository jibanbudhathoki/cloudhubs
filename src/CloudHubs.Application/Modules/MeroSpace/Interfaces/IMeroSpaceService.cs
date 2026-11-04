using CloudHubs.Application.Modules.MeroSpace.DTOs;
using CloudHubs.Application.Modules.Projects.Interfaces;

namespace CloudHubs.Application.Modules.MeroSpace.Interfaces;

public interface IMeroSpaceService
{
    Task<IReadOnlyCollection<MeroSpaceWorkspaceDto>> GetWorkspacesAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<MeroSpaceWorkspaceDto?> GetWorkspaceAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<ProjectFileItem> GetWorkspaceFileTreeAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<string> ReadFileAsync(Guid organizationId, Guid projectId, Guid userId, string relativePath, CancellationToken cancellationToken = default);
    Task WriteFileAsync(Guid organizationId, Guid projectId, Guid userId, string relativePath, string content, CancellationToken cancellationToken = default);
    Task CreateItemAsync(Guid organizationId, Guid projectId, Guid userId, string relativePath, bool isDirectory, CancellationToken cancellationToken = default);
    Task RenameItemAsync(Guid organizationId, Guid projectId, Guid userId, string oldRelativePath, string newRelativePath, CancellationToken cancellationToken = default);
    Task DeleteItemAsync(Guid organizationId, Guid projectId, Guid userId, string relativePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectSearchResult>> SearchAsync(Guid organizationId, Guid projectId, Guid userId, string query, CancellationToken cancellationToken = default);
    Task<string> RunCommandAsync(Guid organizationId, Guid projectId, Guid userId, string? command, CancellationToken cancellationToken = default);
    Task<ProcessRunResult> StartProjectProcessAsync(Guid organizationId, Guid projectId, Guid userId, string? command = null, CancellationToken cancellationToken = default);
    Task<bool> StopProjectProcessAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MeroSpaceCommentDto>> GetCommentsAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<MeroSpaceCommentDto> AddCommentAsync(Guid organizationId, Guid projectId, Guid userId, string authorName, string authorEmail, CreateMeroSpaceCommentRequest request, CancellationToken cancellationToken = default);
    Task<MeroSpaceCommentDto?> ReplyCommentAsync(Guid organizationId, Guid projectId, Guid userId, Guid commentId, string authorName, string authorEmail, string content, CancellationToken cancellationToken = default);
    Task<bool> ToggleResolveCommentAsync(Guid organizationId, Guid projectId, Guid userId, Guid commentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCommentAsync(Guid organizationId, Guid projectId, Guid userId, Guid commentId, CancellationToken cancellationToken = default);
    Task<byte[]> ExportProjectZipAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken = default);
}
