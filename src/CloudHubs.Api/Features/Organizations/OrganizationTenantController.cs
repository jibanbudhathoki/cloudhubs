using System.Runtime.ExceptionServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudHubs.Application.Organizations;
using CloudHubs.Contracts.Projects;
using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Projects;

namespace CloudHubs.Api.Features.Organizations;

[ApiController]
[Route("api/organizations/{organizationId:guid}")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
public class OrganizationTenantController(
    OrganizationTenantService tenantService,
    ILogger<OrganizationTenantController> logger) : ControllerBase
{
    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects(Guid organizationId, CancellationToken cancellationToken)
    {
        try
        {
            var projects = await tenantService.GetProjectsAsync(organizationId, GetUserId(), cancellationToken);
            return Ok(projects.Select(ToProjectResponse));
        }
        catch (Exception exception)
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
        catch (Exception exception)
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
        catch (Exception exception)
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
        catch (Exception exception)
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
        catch (Exception exception)
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
        catch (Exception exception)
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
        catch (Exception exception)
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
        catch (Exception exception)
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
