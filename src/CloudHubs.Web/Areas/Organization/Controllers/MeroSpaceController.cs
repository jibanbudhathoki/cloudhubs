using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudHubs.Application.Modules.MeroSpace.DTOs;
using CloudHubs.Application.Modules.MeroSpace.Interfaces;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Application.Modules.Projects.Interfaces;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.Web.Security;
using CloudHubs.Web.ViewModels.Projects;

namespace CloudHubs.Web.Areas.Organization.Controllers;

[Area("Organization")]
[Route("organization/merospace")]
[Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
public class MeroSpaceController(
    IMeroSpaceService meroSpaceService,
    IOrganizationProjectRepository projectRepository,
    IOrganizationRoleRepository roleRepository,
    OrganizationPermissionService permissionService,
    ILogger<MeroSpaceController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Launch(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var workspaces = await meroSpaceService.GetWorkspacesAsync(organizationId, userId, cancellationToken);
        if (workspaces.Count > 0)
        {
            return RedirectToAction(nameof(Index), new { projectId = workspaces.First().ProjectId });
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships.FirstOrDefault(candidate =>
            candidate.OrganizationId == organizationId && candidate.Organization.IsAvailableForUse());

        return View("Launchpad", new MeroSpaceLaunchpadViewModel
        {
            OrganizationId = organizationId,
            OrganizationName = membership?.Organization.Name ?? "Workspace",
            CanManageProjects = await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken),
            Workspaces = workspaces
        });
    }

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> Index(Guid projectId, CancellationToken cancellationToken)
    {
        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        var workspace = await meroSpaceService.GetWorkspaceAsync(context.Organization!.Id, projectId, context.UserId, cancellationToken);
        if (workspace is null) return NotFound();

        var allWorkspaces = await meroSpaceService.GetWorkspacesAsync(context.Organization.Id, context.UserId, cancellationToken);

        var viewModel = new MeroSpaceViewModel
        {
            ProjectId = workspace.ProjectId,
            OrganizationId = context.Organization.Id,
            OrganizationName = context.Organization.Name,
            OrganizationSlug = context.Organization.Slug,
            ProjectName = workspace.ProjectName,
            ProjectSlug = workspace.ProjectSlug,
            TemplateId = workspace.TemplateId,
            TemplateName = workspace.TemplateName,
            RootPath = workspace.RootPath,
            CanManageProjects = context.CanManage,
            UserName = User.Identity?.Name ?? "Developer",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            AvailableWorkspaces = allWorkspaces
        };

        return View("MeroSpace", viewModel);
    }

    [HttpGet("{projectId:guid}/tree")]
    public async Task<IActionResult> GetTree(Guid projectId, CancellationToken cancellationToken)
    {
        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        try
        {
            var tree = await meroSpaceService.GetWorkspaceFileTreeAsync(context.Organization!.Id, projectId, context.UserId, cancellationToken);
            return Ok(tree);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load file tree for project {ProjectId}.", projectId);
            return StatusCode(500, new { error = "Unable to load project file tree." });
        }
    }

    [HttpGet("{projectId:guid}/file")]
    public async Task<IActionResult> GetFile(Guid projectId, [FromQuery] string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path)) return BadRequest(new { error = "Path is required." });

        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        try
        {
            var content = await meroSpaceService.ReadFileAsync(context.Organization!.Id, projectId, context.UserId, path, cancellationToken);
            return Ok(new { path, content });
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = $"File '{path}' was not found." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read file '{Path}' in project {ProjectId}.", path, projectId);
            return StatusCode(500, new { error = "Unable to read file." });
        }
    }

    [HttpPost("{projectId:guid}/file")]
    public async Task<IActionResult> SaveFile(Guid projectId, [FromBody] SaveFileRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest(new { error = "Path is required." });
        }

        var context = await ResolveContextAsync(projectId, cancellationToken, requireManage: true);
        if (context.FailureResult is not null) return context.FailureResult;

        try
        {
            await meroSpaceService.WriteFileAsync(context.Organization!.Id, projectId, context.UserId, request.Path, request.Content ?? string.Empty, cancellationToken);
            return Ok(new { success = true, path = request.Path });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write file '{Path}' in project {ProjectId}.", request.Path, projectId);
            return StatusCode(500, new { error = "Unable to save file." });
        }
    }

    [HttpPost("{projectId:guid}/create-item")]
    public async Task<IActionResult> CreateItem(Guid projectId, [FromBody] CreateItemRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest(new { error = "Path is required." });
        }

        var context = await ResolveContextAsync(projectId, cancellationToken, requireManage: true);
        if (context.FailureResult is not null) return context.FailureResult;

        try
        {
            await meroSpaceService.CreateItemAsync(context.Organization!.Id, projectId, context.UserId, request.Path, request.IsDirectory, cancellationToken);
            return Ok(new { success = true, path = request.Path });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create item '{Path}' in project {ProjectId}.", request.Path, projectId);
            return StatusCode(500, new { error = "Unable to create item." });
        }
    }

    [HttpPost("{projectId:guid}/rename-item")]
    public async Task<IActionResult> RenameItem(Guid projectId, [FromBody] RenameItemRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.OldPath) || string.IsNullOrWhiteSpace(request.NewPath))
        {
            return BadRequest(new { error = "Old and new paths are required." });
        }

        var context = await ResolveContextAsync(projectId, cancellationToken, requireManage: true);
        if (context.FailureResult is not null) return context.FailureResult;

        try
        {
            await meroSpaceService.RenameItemAsync(context.Organization!.Id, projectId, context.UserId, request.OldPath, request.NewPath, cancellationToken);
            return Ok(new { success = true, oldPath = request.OldPath, newPath = request.NewPath });
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = "Item to rename was not found." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rename item '{OldPath}' in project {ProjectId}.", request.OldPath, projectId);
            return StatusCode(500, new { error = "Unable to rename item." });
        }
    }

    [HttpPost("{projectId:guid}/delete-item")]
    public async Task<IActionResult> DeleteItem(Guid projectId, [FromBody] DeleteItemRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest(new { error = "Path is required." });
        }

        var context = await ResolveContextAsync(projectId, cancellationToken, requireManage: true);
        if (context.FailureResult is not null) return context.FailureResult;

        try
        {
            await meroSpaceService.DeleteItemAsync(context.Organization!.Id, projectId, context.UserId, request.Path, cancellationToken);
            return Ok(new { success = true, path = request.Path });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete item '{Path}' in project {ProjectId}.", request.Path, projectId);
            return StatusCode(500, new { error = "Unable to delete item." });
        }
    }

    [HttpGet("{projectId:guid}/search")]
    public async Task<IActionResult> Search(Guid projectId, [FromQuery] string q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<object>());

        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        try
        {
            var results = await meroSpaceService.SearchAsync(context.Organization!.Id, projectId, context.UserId, q, cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search project {ProjectId} with query '{Query}'.", projectId, q);
            return StatusCode(500, new { error = "Search failed." });
        }
    }

    [HttpPost("{projectId:guid}/run")]
    public async Task<IActionResult> Run(Guid projectId, [FromBody] RunCommandRequest request, CancellationToken cancellationToken)
    {
        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        try
        {
            var result = await meroSpaceService.StartProjectProcessAsync(context.Organization!.Id, projectId, context.UserId, request?.Command, cancellationToken);
            return Ok(new
            {
                success = result.Success,
                status = result.Status,
                pid = result.ProcessId,
                port = result.Port,
                output = result.Output,
                error = result.Error,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run command for project {ProjectId}.", projectId);
            return StatusCode(500, new { error = "Execution failed." });
        }
    }

    [HttpPost("{projectId:guid}/stop")]
    public async Task<IActionResult> Stop(Guid projectId, CancellationToken cancellationToken)
    {
        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        var stopped = await meroSpaceService.StopProjectProcessAsync(context.Organization!.Id, projectId, context.UserId, cancellationToken);
        return Ok(new
        {
            success = stopped,
            status = "stopped",
            message = "Project execution stopped.",
            timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("{projectId:guid}/exec")]
    public async Task<IActionResult> Exec(Guid projectId, [FromBody] RunCommandRequest request, CancellationToken cancellationToken)
    {
        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        var cmd = (request?.Command ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cmd))
        {
            return Ok(new { output = "" });
        }

        try
        {
            if (cmd.StartsWith("cat ", StringComparison.OrdinalIgnoreCase))
            {
                var filePath = cmd[4..].Trim();
                var content = await meroSpaceService.ReadFileAsync(context.Organization!.Id, projectId, context.UserId, filePath, cancellationToken);
                return Ok(new { output = content });
            }

            if (cmd.Equals("ls", StringComparison.OrdinalIgnoreCase) || cmd.Equals("dir", StringComparison.OrdinalIgnoreCase))
            {
                var tree = await meroSpaceService.GetWorkspaceFileTreeAsync(context.Organization!.Id, projectId, context.UserId, cancellationToken);
                var listing = (tree.Children ?? []).Select(c => (c.IsDirectory ? "📁 " : "📄 ") + c.Name);
                return Ok(new { output = string.Join("\n", listing) });
            }

            var output = await meroSpaceService.RunCommandAsync(context.Organization!.Id, projectId, context.UserId, cmd, cancellationToken);
            return Ok(new { output });
        }
        catch (Exception ex)
        {
            return Ok(new { output = $"Error: {ex.Message}" });
        }
    }

    [HttpPost("{projectId:guid}/api-proxy")]
    public async Task<IActionResult> ApiProxy(Guid projectId, [FromBody] ApiProxyRequest request, CancellationToken cancellationToken)
    {
        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        if (request is null || string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new { error = "Url is required." });
        }

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var httpMethod = new HttpMethod(request.Method?.ToUpperInvariant() ?? "GET");
            using var message = new HttpRequestMessage(httpMethod, request.Url);

            if (request.Headers is not null)
            {
                foreach (var (k, v) in request.Headers)
                {
                    if (!k.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        message.Headers.TryAddWithoutValidation(k, v);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(request.Body) && (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put || httpMethod.Method == "PATCH"))
            {
                message.Content = new StringContent(request.Body, System.Text.Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(message, cancellationToken);
            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var respHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

            return Ok(new
            {
                statusCode = (int)response.StatusCode,
                statusText = response.StatusCode.ToString(),
                elapsedMs = sw.ElapsedMilliseconds,
                headers = respHeaders,
                body,
                sizeBytes = System.Text.Encoding.UTF8.GetByteCount(body ?? string.Empty)
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                statusCode = 0,
                statusText = "Connection Failed",
                elapsedMs = 0,
                headers = new Dictionary<string, string>(),
                body = ex.Message
            });
        }
    }

    [HttpGet("{projectId:guid}/download")]
    public async Task<IActionResult> DownloadZip(Guid projectId, CancellationToken cancellationToken)
    {
        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        var project = await projectRepository.GetByIdAsync(context.Organization!.Id, projectId, cancellationToken);
        if (project is null) return NotFound();

        try
        {
            var zipBytes = await meroSpaceService.ExportProjectZipAsync(context.Organization.Id, projectId, context.UserId, cancellationToken);
            return File(zipBytes, "application/zip", $"{project.Slug}-source.zip");
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound(new { error = "Project directory not found on server." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create project ZIP archive for project {ProjectId}.", projectId);
            return StatusCode(500, new { error = "Unable to create ZIP package." });
        }
    }

    [HttpGet("{projectId:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid projectId, CancellationToken cancellationToken)
    {
        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        try
        {
            var comments = await meroSpaceService.GetCommentsAsync(context.Organization!.Id, projectId, context.UserId, cancellationToken);
            return Ok(comments);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load comments for project {ProjectId}.", projectId);
            return StatusCode(500, new { error = "Unable to load comments." });
        }
    }

    [HttpPost("{projectId:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid projectId, [FromBody] CreateMeroSpaceCommentRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Comment content is required." });
        }

        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        var authorName = User.Identity?.Name ?? "Developer";
        var authorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        try
        {
            var comment = await meroSpaceService.AddCommentAsync(
                context.Organization!.Id,
                projectId,
                context.UserId,
                authorName,
                authorEmail,
                request,
                cancellationToken);

            return Ok(comment);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create comment for project {ProjectId}.", projectId);
            return StatusCode(500, new { error = "Unable to save comment." });
        }
    }

    [HttpPost("{projectId:guid}/comments/{commentId:guid}/reply")]
    public async Task<IActionResult> ReplyComment(Guid projectId, Guid commentId, [FromBody] ReplyMeroSpaceCommentRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Reply content is required." });
        }

        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        var authorName = User.Identity?.Name ?? "Developer";
        var authorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        try
        {
            var comment = await meroSpaceService.ReplyCommentAsync(
                context.Organization!.Id,
                projectId,
                context.UserId,
                commentId,
                authorName,
                authorEmail,
                request.Content,
                cancellationToken);

            if (comment is null) return NotFound(new { error = "Comment not found." });
            return Ok(comment);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reply to comment {CommentId} in project {ProjectId}.", commentId, projectId);
            return StatusCode(500, new { error = "Unable to save reply." });
        }
    }

    [HttpPost("{projectId:guid}/comments/{commentId:guid}/toggle-resolve")]
    public async Task<IActionResult> ToggleResolveComment(Guid projectId, Guid commentId, CancellationToken cancellationToken)
    {
        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        try
        {
            var success = await meroSpaceService.ToggleResolveCommentAsync(context.Organization!.Id, projectId, context.UserId, commentId, cancellationToken);
            if (!success) return NotFound(new { error = "Comment not found." });
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle resolve for comment {CommentId} in project {ProjectId}.", commentId, projectId);
            return StatusCode(500, new { error = "Unable to update comment status." });
        }
    }

    [HttpDelete("{projectId:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid projectId, Guid commentId, CancellationToken cancellationToken)
    {
        var context = await ResolveContextAsync(projectId, cancellationToken);
        if (context.FailureResult is not null) return context.FailureResult;

        try
        {
            var success = await meroSpaceService.DeleteCommentAsync(context.Organization!.Id, projectId, context.UserId, commentId, cancellationToken);
            if (!success) return NotFound(new { error = "Comment not found." });
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete comment {CommentId} in project {ProjectId}.", commentId, projectId);
            return StatusCode(500, new { error = "Unable to delete comment." });
        }
    }

    private async Task<(IActionResult? FailureResult, Guid UserId, CloudHubs.Domain.Entities.Organization? Organization, bool CanManage)>
        ResolveContextAsync(Guid projectId, CancellationToken cancellationToken, bool requireManage = false)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return (RedirectToAction("Login", "Portal"), Guid.Empty, null, false);
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships.FirstOrDefault(candidate =>
            candidate.OrganizationId == organizationId && candidate.Organization.IsAvailableForUse());

        if (membership is null)
        {
            return (Forbid(TenantCookieDefaults.AuthenticationScheme), userId, null, false);
        }

        var canView = await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        var canManage = await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken);

        if (!canView || (requireManage && !canManage))
        {
            return (Forbid(TenantCookieDefaults.AuthenticationScheme), userId, null, false);
        }

        var project = await projectRepository.GetByIdAsync(organizationId, projectId, cancellationToken);
        if (project is null)
        {
            return (NotFound(new { error = "Project not found in this organization." }), userId, null, false);
        }

        return (null, userId, membership.Organization, canManage);
    }
}

public sealed record SaveFileRequest(string Path, string Content);
public sealed record CreateItemRequest(string Path, bool IsDirectory);
public sealed record RenameItemRequest(string OldPath, string NewPath);
public sealed record DeleteItemRequest(string Path);
public sealed record RunCommandRequest(string? Command);
public sealed record ApiProxyRequest(string Method, string Url, Dictionary<string, string>? Headers, string? Body);
