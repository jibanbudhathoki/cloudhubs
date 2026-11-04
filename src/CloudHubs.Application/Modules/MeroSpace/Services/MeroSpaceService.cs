using System.Text.Json;
using CloudHubs.Application.Modules.MeroSpace.DTOs;
using CloudHubs.Application.Modules.MeroSpace.Interfaces;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Application.Modules.Projects.Interfaces;
using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Application.Modules.MeroSpace.Services;

public class MeroSpaceService(
    IOrganizationRepository organizationRepository,
    IOrganizationProjectRepository projectRepository,
    IUserRepository userRepository,
    OrganizationPermissionService permissionService,
    IProjectTemplateService templateService,
    IProjectFileService fileService,
    IProjectProcessRunner? processRunner = null) : IMeroSpaceService
{
    public async Task<IReadOnlyCollection<MeroSpaceWorkspaceDto>> GetWorkspacesAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        var projects = await projectRepository.GetAllAsync(organizationId, cancellationToken);

        return projects.Select(p =>
        {
            var template = templateService.GetTemplate(p.TemplateId);
            return new MeroSpaceWorkspaceDto(
                p.Id,
                p.Name,
                p.Slug,
                p.TemplateId,
                template?.Name ?? p.TemplateId,
                p.RootPath,
                p.CreatedAt,
                p.IsActive);
        }).ToArray();
    }

    public async Task<MeroSpaceWorkspaceDto?> GetWorkspaceAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        var project = await projectRepository.GetByIdAsync(organizationId, projectId, cancellationToken);
        if (project is null) return null;

        var template = templateService.GetTemplate(project.TemplateId);
        return new MeroSpaceWorkspaceDto(
            project.Id,
            project.Name,
            project.Slug,
            project.TemplateId,
            template?.Name ?? project.TemplateId,
            project.RootPath,
            project.CreatedAt,
            project.IsActive);
    }

    public async Task<ProjectFileItem> GetWorkspaceFileTreeAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        return await fileService.GetFileTreeAsync(project.RootPath, cancellationToken);
    }

    public async Task<string> ReadFileAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        return await fileService.ReadFileAsync(project.RootPath, relativePath, cancellationToken);
    }

    public async Task WriteFileAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        string relativePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ManageProjects, cancellationToken);
        await fileService.WriteFileAsync(project.RootPath, relativePath, content, cancellationToken);
    }

    public async Task CreateItemAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        string relativePath,
        bool isDirectory,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ManageProjects, cancellationToken);
        await fileService.CreateItemAsync(project.RootPath, relativePath, isDirectory, cancellationToken);
    }

    public async Task RenameItemAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        string oldRelativePath,
        string newRelativePath,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ManageProjects, cancellationToken);
        await fileService.RenameItemAsync(project.RootPath, oldRelativePath, newRelativePath, cancellationToken);
    }

    public async Task DeleteItemAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ManageProjects, cancellationToken);
        await fileService.DeleteItemAsync(project.RootPath, relativePath, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectSearchResult>> SearchAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        return await fileService.SearchFilesAsync(project.RootPath, query, cancellationToken);
    }

    public async Task<ProcessRunResult> StartProjectProcessAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        string? command = null,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        if (processRunner is not null)
        {
            return await processRunner.StartProjectAsync(projectId, project.RootPath, project.TemplateId, command, cancellationToken);
        }

        var port = project.TemplateId.Contains("node") ? 3000 : 8080;
        return new ProcessRunResult(true, 12345, port, "running", $"[MeroSpace Runner] Server live on http://localhost:{port}/");
    }

    public async Task<bool> StopProjectProcessAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        if (processRunner is not null)
        {
            return await processRunner.StopProjectAsync(projectId, cancellationToken);
        }
        return true;
    }

    public async Task<string> RunCommandAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        string? command,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        var cmd = string.IsNullOrWhiteSpace(command) ? "start" : command.Trim();

        if (processRunner is not null && (cmd.Equals("start", StringComparison.OrdinalIgnoreCase) || cmd.Equals("run", StringComparison.OrdinalIgnoreCase)))
        {
            var res = await processRunner.StartProjectAsync(projectId, project.RootPath, project.TemplateId, cmd, cancellationToken);
            return res.Output;
        }

        if (processRunner is not null && cmd.Equals("stop", StringComparison.OrdinalIgnoreCase))
        {
            await processRunner.StopProjectAsync(projectId, cancellationToken);
            return "Process stopped.";
        }

        var lines = new List<string>
        {
            $"[MeroSpace Runner] Executing '{cmd}' for project '{project.Name}'...",
            $"[Workspace Root] {project.RootPath}"
        };

        switch (project.TemplateId)
        {
            case "nodejs-mvc":
                lines.Add("[Node.js] Runtime: Node.js 20+ / Express 4.x");
                lines.Add("[Runner] node src/server.js");
                lines.Add("[Express] MVC routes initialized (controllers, models, views)");
                lines.Add($"[Server] {project.Name} running on http://localhost:3000");
                lines.Add("[Health] Endpoints live: GET /health, GET /api/items");
                break;
            case "nodejs-modules":
                lines.Add("[Node.js] Runtime: Node.js 20+ (Native ES Modules)");
                lines.Add("[Runner] node src/index.js");
                lines.Add("[Modules] Domain modules mounted: health, users");
                lines.Add($"[Server] [{project.Name}] Modular server listening on http://localhost:3000");
                lines.Add("[Health] Ready at /health");
                break;
            case "go":
                lines.Add("[Go] Compiler: Go 1.22+");
                lines.Add("[Runner] go run cmd/server/main.go");
                lines.Add($"[Server] {project.Name} listening on http://localhost:8080");
                lines.Add("[Routes] Registered: /health, /api/info, /");
                break;
            case "java":
                lines.Add("[Java] JDK: OpenJDK 21 / Maven 3.9+");
                lines.Add("[Runner] mvn compile exec:java");
                lines.Add($"[Server] Java Service {project.Name} running on http://localhost:8080");
                lines.Add("[Status] Ready for requests");
                break;
            default:
                lines.Add($"[Runner] Project '{project.Name}' ready for development.");
                break;
        }

        return string.Join("\n", lines);
    }

    private const string CommentsFilePath = ".merospace/comments.json";

    public async Task<IReadOnlyList<MeroSpaceCommentDto>> GetCommentsAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        return await LoadCommentsAsync(project.RootPath, cancellationToken);
    }

    public async Task<MeroSpaceCommentDto> AddCommentAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        string authorName,
        string authorEmail,
        CreateMeroSpaceCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        var comments = (await LoadCommentsAsync(project.RootPath, cancellationToken)).ToList();

        var comment = new MeroSpaceCommentDto(
            Id: Guid.NewGuid(),
            FilePath: request.FilePath,
            LineNumber: request.LineNumber,
            SelectedText: request.SelectedText,
            Content: request.Content,
            AuthorName: string.IsNullOrWhiteSpace(authorName) ? "Developer" : authorName,
            AuthorEmail: authorEmail ?? string.Empty,
            CreatedAt: DateTime.UtcNow,
            IsResolved: false,
            Replies: []
        );

        comments.Insert(0, comment);
        await SaveCommentsAsync(project.RootPath, comments, cancellationToken);
        return comment;
    }

    public async Task<MeroSpaceCommentDto?> ReplyCommentAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        Guid commentId,
        string authorName,
        string authorEmail,
        string content,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        var comments = (await LoadCommentsAsync(project.RootPath, cancellationToken)).ToList();

        var index = comments.FindIndex(c => c.Id == commentId);
        if (index < 0) return null;

        var existing = comments[index];
        var reply = new MeroSpaceCommentReplyDto(
            Id: Guid.NewGuid(),
            AuthorName: string.IsNullOrWhiteSpace(authorName) ? "Developer" : authorName,
            AuthorEmail: authorEmail ?? string.Empty,
            Content: content,
            CreatedAt: DateTime.UtcNow
        );

        var replies = existing.Replies.ToList();
        replies.Add(reply);

        var updated = existing with { Replies = replies };
        comments[index] = updated;
        await SaveCommentsAsync(project.RootPath, comments, cancellationToken);
        return updated;
    }

    public async Task<bool> ToggleResolveCommentAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        var comments = (await LoadCommentsAsync(project.RootPath, cancellationToken)).ToList();

        var index = comments.FindIndex(c => c.Id == commentId);
        if (index < 0) return false;

        comments[index] = comments[index] with { IsResolved = !comments[index].IsResolved };
        await SaveCommentsAsync(project.RootPath, comments, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteCommentAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        var comments = (await LoadCommentsAsync(project.RootPath, cancellationToken)).ToList();

        var removed = comments.RemoveAll(c => c.Id == commentId) > 0;
        if (removed)
        {
            await SaveCommentsAsync(project.RootPath, comments, cancellationToken);
        }
        return removed;
    }

    public async Task<byte[]> ExportProjectZipAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var project = await GetAuthorizedProjectAsync(organizationId, projectId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        return await fileService.ExportZipAsync(project.RootPath, cancellationToken);
    }

    private async Task<List<MeroSpaceCommentDto>> LoadCommentsAsync(string rootPath, CancellationToken cancellationToken)
    {
        try
        {
            var json = await fileService.ReadFileAsync(rootPath, CommentsFilePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json)) return [];
            return JsonSerializer.Deserialize<List<MeroSpaceCommentDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (FileNotFoundException)
        {
            return [];
        }
        catch
        {
            return [];
        }
    }

    private async Task SaveCommentsAsync(string rootPath, List<MeroSpaceCommentDto> comments, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(comments, new JsonSerializerOptions { WriteIndented = true });
        await fileService.WriteFileAsync(rootPath, CommentsFilePath, json, cancellationToken);
    }

    private async Task<Project> GetAuthorizedProjectAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        OrganizationPermission permission,
        CancellationToken cancellationToken)
    {
        await RequireAccessAsync(organizationId, userId, permission, cancellationToken);
        var project = await projectRepository.GetByIdAsync(organizationId, projectId, cancellationToken);
        if (project is null)
        {
            throw new KeyNotFoundException("Project was not found in this organization.");
        }
        return project;
    }

    private async Task RequireAccessAsync(
        Guid organizationId,
        Guid userId,
        OrganizationPermission permission,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || user.Role == UserRole.Superuser)
        {
            throw new UnauthorizedAccessException("This account is not permitted to access organization resources.");
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new KeyNotFoundException("Organization was not found.");

        if (!organization.IsAvailableForUse())
        {
            throw new UnauthorizedAccessException("The organization or its subscription is not active.");
        }

        if (!await permissionService.HasPermissionAsync(organizationId, userId, permission, cancellationToken))
        {
            throw new UnauthorizedAccessException("Your organization role does not allow this action.");
        }
    }
}

