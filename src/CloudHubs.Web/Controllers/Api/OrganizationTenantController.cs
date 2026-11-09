using System.Runtime.ExceptionServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudHubs.Application.Modules.Docs.Interfaces;
using CloudHubs.Application.Modules.Organizations.DTOs;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Application.Modules.Projects.DTOs;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.Web.Security;

namespace CloudHubs.Web.Controllers.Api;

[ApiController]
[Route("api/organizations/{organizationId:guid}")]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyDefaults.AuthenticationScheme}", Roles = "OrganizationMember")]
public class OrganizationTenantController(
    OrganizationTenantService tenantService,
    IDocsService docsService,
    IOrganizationRepository organizationRepository,
    ILogger<OrganizationTenantController> logger) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(Guid organizationId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var organization = await organizationRepository.GetByIdAsync(organizationId, cancellationToken);
            if (organization is null) return NotFound();

            var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
            var projects = await tenantService.GetProjectsAsync(organizationId, userId, cancellationToken);
            var members = await tenantService.GetMembersAsync(organizationId, userId, cancellationToken);
            var docs = await docsService.GetDocsAsync(organizationId, userId, cancellationToken);

            return Ok(new
            {
                organizationId = organization.Id,
                name = organization.Name,
                slug = organization.Slug,
                email = organization.Email,
                status = organization.Status.ToString(),
                plan = new
                {
                    name = plan?.Name ?? "Basic",
                    hasApiAccess = plan?.HasApiAccess ?? false,
                    maxProjects = plan?.MaxProjects ?? 10,
                    maxMembers = plan?.MaxMembers ?? 20,
                    storageLimitGb = plan?.StorageLimitGb ?? 10
                },
                metrics = new
                {
                    projectsCount = projects.Count,
                    membersCount = members.Count,
                    docsCount = docs.Count
                },
                createdAt = organization.CreatedAt,
                updatedAt = organization.UpdatedAt
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleFailure(exception, "Unable to load organization overview.", organizationId);
        }
    }

    [HttpGet("docs")]
    public async Task<IActionResult> GetDocs(Guid organizationId, CancellationToken cancellationToken)
    {
        try
        {
            var docs = await docsService.GetDocsAsync(organizationId, GetUserId(), cancellationToken);
            return Ok(docs.Select(d => new
            {
                id = d.Id,
                title = d.Title,
                slug = d.Slug,
                category = d.Category,
                description = d.Description,
                baseUrl = d.BaseUrl,
                hasLiveApiKey = !string.IsNullOrWhiteSpace(d.ApiKey),
                hasTestApiKey = !string.IsNullOrWhiteSpace(d.TestApiKey),
                hasSecretKey = !string.IsNullOrWhiteSpace(d.SecretKey),
                hasPostmanCollection = !string.IsNullOrWhiteSpace(d.PostmanCollection),
                hasEnvironmentVariables = !string.IsNullOrWhiteSpace(d.EnvironmentVariables),
                projectId = d.ProjectId,
                createdAt = d.CreatedAt,
                updatedAt = d.UpdatedAt
            }));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleFailure(exception, "Unable to load documentation profiles.", organizationId);
        }
    }

    [HttpGet("docs/{docId:guid}")]
    public async Task<IActionResult> GetDocDetails(Guid organizationId, Guid docId, CancellationToken cancellationToken)
    {
        try
        {
            var doc = await docsService.GetDocByIdAsync(organizationId, docId, GetUserId(), cancellationToken);
            if (doc is null) return NotFound();

            return Ok(new
            {
                id = doc.Id,
                title = doc.Title,
                slug = doc.Slug,
                category = doc.Category,
                description = doc.Description,
                baseUrl = doc.BaseUrl,
                apiKey = doc.ApiKey,
                testApiKey = doc.TestApiKey,
                secretKey = doc.SecretKey,
                content = doc.Content,
                environmentVariables = doc.EnvironmentVariables,
                postmanCollection = doc.PostmanCollection,
                projectId = doc.ProjectId,
                createdAt = doc.CreatedAt,
                updatedAt = doc.UpdatedAt
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleFailure(exception, "Unable to load documentation profile details.", organizationId);
        }
    }

    [HttpGet("resources")]
    public async Task<IActionResult> GetResources(Guid organizationId, CancellationToken cancellationToken)
    {
        try
        {
            var projects = await tenantService.GetProjectsAsync(organizationId, GetUserId(), cancellationToken);
            return Ok(projects.Select(p => new
            {
                projectId = p.Id,
                projectName = p.Name,
                slug = p.Slug,
                templateId = p.TemplateId,
                rootPath = p.RootPath,
                isActive = p.IsActive,
                createdAt = p.CreatedAt
            }));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleFailure(exception, "Unable to load organization project resources.", organizationId);
        }
    }
    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects(Guid organizationId, CancellationToken cancellationToken)
    {
        try
        {
            var projects = await tenantService.GetProjectsAsync(organizationId, GetUserId(), cancellationToken);
            return Ok(projects.Select(ToProjectResponse));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleFailure(exception, "Unable to load organization projects.", organizationId);
        }
    }

    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject(
        Guid organizationId,
        [FromBody] CreateOrganizationProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await tenantService.CreateProjectAsync(
                organizationId,
                GetUserId(),
                request.Name,
                request.Slug,
                request.Description,
                request.RootPath,
                request.TemplateId,
                cancellationToken);
            return CreatedAtAction(nameof(GetProjects), new { organizationId }, ToProjectResponse(project));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleFailure(exception, "Unable to create the organization project.", organizationId);
        }
    }

    [HttpPut("projects/{projectId:guid}")]
    public async Task<IActionResult> UpdateProject(
        Guid organizationId,
        Guid projectId,
        [FromBody] CreateOrganizationProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await tenantService.UpdateProjectAsync(
                organizationId,
                projectId,
                GetUserId(),
                request.Name,
                request.Slug,
                request.Description,
                request.RootPath,
                request.TemplateId,
                cancellationToken);
            return project is null ? NotFound() : Ok(ToProjectResponse(project));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleFailure(exception, "Unable to update the organization project.", organizationId);
        }
    }

    [HttpDelete("projects/{projectId:guid}")]
    public async Task<IActionResult> DeleteProject(
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await tenantService.DeleteProjectAsync(organizationId, projectId, GetUserId(), cancellationToken))
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleFailure(exception, "Unable to delete the organization project.", organizationId);
        }
    }

    [HttpGet("members")]
    public async Task<IActionResult> GetMembers(Guid organizationId, CancellationToken cancellationToken)
    {
        try
        {
            var members = await tenantService.GetMembersAsync(organizationId, GetUserId(), cancellationToken);
            return Ok(members.Select(ToMemberResponse));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleFailure(exception, "Unable to load organization members.", organizationId);
        }
    }

    [HttpPost("members")]
    public async Task<IActionResult> AddMember(
        Guid organizationId,
        [FromBody] AddOrganizationMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrganizationMemberRole>(request.Role, true, out var role)
            || role == OrganizationMemberRole.Owner
            || !Enum.IsDefined(role))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Choose a valid non-owner organization role."
            });
        }

        try
        {
            var membership = await tenantService.AddMemberAsync(
                organizationId,
                GetUserId(),
                request.Email,
                role,
                cancellationToken);
            return Ok(ToMemberResponse(membership));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleFailure(exception, "Unable to add the organization member.", organizationId);
        }
    }

    [HttpPut("members/{memberId:guid}")]
    public async Task<IActionResult> UpdateMemberRole(
        Guid organizationId,
        Guid memberId,
        [FromBody] UpdateOrganizationMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrganizationMemberRole>(request.Role, true, out var role)
            || role == OrganizationMemberRole.Owner
            || !Enum.IsDefined(role))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Choose a valid non-owner organization role."
            });
        }

        try
        {
            var actorId = GetUserId();
            var updated = await tenantService.UpdateMemberRoleAsync(
                organizationId,
                actorId,
                memberId,
                role,
                request.CustomRoleId,
                cancellationToken);
            return updated is null ? NotFound() : Ok(ToMemberResponse(updated));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleFailure(exception, "Unable to update the organization member role.", organizationId);
        }
    }

    [HttpDelete("members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await tenantService.RemoveMemberAsync(organizationId, GetUserId(), memberId, cancellationToken))
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleFailure(exception, "Unable to remove the organization member.", organizationId);
        }
    }

    private IActionResult HandleFailure(Exception exception, string genericMessage, Guid organizationId)
    {
        if (exception is OperationCanceledException)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
        if (exception is KeyNotFoundException)
        {
            return NotFound();
        }
        if (exception is UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = exception.Message
            });
        }
        if (exception is ArgumentException)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = exception.Message
            });
        }
        if (exception is InvalidOperationException)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = exception.Message
            });
        }

        logger.LogError(exception, "{Message} Organization {OrganizationId}.", genericMessage, organizationId);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: genericMessage);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The access token has no valid user ID.");
    }

    private static OrganizationMemberResponse ToMemberResponse(OrganizationMembership membership) =>
        new(
            membership.UserId,
            membership.User.LoginId,
            membership.User.Name,
            membership.User.Email,
            membership.Role.ToString(),
            membership.CustomRole?.Name);

    private static OrganizationProjectResponse ToProjectResponse(Project project) =>
        new(project.Id, project.Name, project.Slug, project.Description, project.TemplateId, project.IsActive, project.CreatedAt);
}
