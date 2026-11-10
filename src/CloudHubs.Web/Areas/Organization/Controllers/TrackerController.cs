using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Application.Modules.ProjectTracker.DTOs;
using CloudHubs.Application.Modules.ProjectTracker.Interfaces;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.Infrastructure.Data;
using CloudHubs.Web.Security;
using CloudHubs.Web.ViewModels.Tracker;

namespace CloudHubs.Web.Areas.Organization.Controllers;

[Area("Organization")]
[Route("organization/tracker")]
[Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
[AutoValidateAntiforgeryToken]
public class TrackerController(
    IProjectTrackerService trackerService,
    IOrganizationRoleRepository roleRepository,
    OrganizationPermissionService permissionService,
    CloudHubsDbContext dbContext) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] Guid? projectId,
        [FromQuery] IssueStatus? status,
        [FromQuery] IssuePriority? priority,
        [FromQuery] Guid? assigneeId,
        [FromQuery] string? search,
        [FromQuery] string? view,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships
            .FirstOrDefault(c => c.OrganizationId == organizationId && c.Organization.IsAvailableForUse());
        if (membership is null)
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        var canViewMembers = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ViewMembers, cancellationToken);
        var canViewProjects = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ViewProjects, cancellationToken);

        var organization = await dbContext.Organizations
            .Include(o => o.Subscription).ThenInclude(s => s!.Plan)
            .Include(o => o.AccountSubscription).ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        var plan = organization?.AccountSubscription?.Plan ?? organization?.Subscription?.Plan;
        var planName = plan?.Name ?? "Basic";
        var isProOrCustom = plan != null && (plan.HasApiAccess || planName is "Pro" or "Custom");

        var activeView = (view ?? "kanban").ToLowerInvariant();
        if (!isProOrCustom && activeView == "kanban")
        {
            // Fallback default view to list on basic plan, but keep awareness
            activeView = "list";
        }

        var issues = await trackerService.GetIssuesAsync(
            organizationId, projectId, status, priority, assigneeId, search, cancellationToken);

        var metrics = await trackerService.GetMetricsAsync(organizationId, projectId, cancellationToken);

        var projects = await dbContext.Projects
            .Where(p => p.OrganizationId == organizationId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var members = await dbContext.OrganizationMemberships
            .Include(m => m.User)
            .Where(m => m.OrganizationId == organizationId)
            .Select(m => m.User)
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);

        var availableOrgs = memberships
            .Where(m => m.Organization.IsAvailableForUse())
            .Select(m => m.Organization)
            .ToList();

        var vm = new ProjectTrackerViewModel
        {
            OrganizationId = organizationId,
            OrganizationName = organization?.Name ?? membership.Organization.Name,
            OrganizationSlug = organization?.Slug ?? membership.Organization.Slug,
            OrganizationRole = membership.Role.ToString(),
            SubscriptionPlanName = planName,
            IsProOrCustom = isProOrCustom,
            UserName = !string.IsNullOrEmpty(user?.Name) ? user!.Name : (user?.Email ?? "User"),
            UserEmail = user?.Email ?? string.Empty,
            CanViewMembers = canViewMembers,
            CanViewProjects = canViewProjects,
            Organizations = availableOrgs,
            Issues = issues,
            Metrics = metrics,
            Projects = projects,
            OrganizationMembers = members,
            SelectedProjectId = projectId,
            SelectedStatus = status,
            SelectedPriority = priority,
            SelectedAssigneeId = assigneeId,
            Search = search,
            ActiveView = activeView
        };

        return View(vm);
    }

    [HttpGet("issue/{id:guid}")]
    public async Task<IActionResult> GetIssueDetail(Guid id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return Unauthorized();
        }

        var detail = await trackerService.GetIssueDetailAsync(organizationId, id, cancellationToken);
        if (detail == null) return NotFound(new { error = "Issue not found" });

        return Json(detail);
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateIssue([FromForm] CreateIssueFormModel form, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        if (string.IsNullOrWhiteSpace(form.Title))
        {
            TempData["ErrorMessage"] = "Ticket title cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        if (form.ProjectId == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Please select a project for this ticket.";
            return RedirectToAction(nameof(Index));
        }

        var request = new CreateIssueRequest
        {
            ProjectId = form.ProjectId,
            Title = form.Title,
            Description = form.Description,
            IssueType = form.IssueType,
            Priority = form.Priority,
            AssigneeId = form.AssigneeId == Guid.Empty ? null : form.AssigneeId,
            DueDate = form.DueDate.HasValue ? DateTime.SpecifyKind(form.DueDate.Value, DateTimeKind.Utc) : null,
            EstimatedHours = form.EstimatedHours
        };

        await trackerService.CreateIssueAsync(organizationId, userId, request, cancellationToken);
        TempData["SuccessMessage"] = "Ticket created successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("update-status")]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateIssueStatusModel model, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return Unauthorized();
        }

        var organization = await dbContext.Organizations
            .Include(o => o.Subscription).ThenInclude(s => s!.Plan)
            .Include(o => o.AccountSubscription).ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        var success = await trackerService.UpdateIssueStatusAsync(
            organizationId, model.IssueId, userId, model.Status, cancellationToken);

        if (!success) return BadRequest(new { error = "Unable to update status" });

        return Ok(new { success = true, newStatus = model.Status.ToString() });
    }

    [HttpPost("comment")]
    public async Task<IActionResult> AddComment([FromBody] AddIssueCommentModel model, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return Unauthorized();
        }

        var organization = await dbContext.Organizations
            .Include(o => o.Subscription).ThenInclude(s => s!.Plan)
            .Include(o => o.AccountSubscription).ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        var plan = organization?.AccountSubscription?.Plan ?? organization?.Subscription?.Plan;
        var isProOrCustom = plan != null && (plan.HasApiAccess || plan.Name is "Pro" or "Custom");

        if (!isProOrCustom)
        {
            return BadRequest(new { error = "Team ticket discussions and comments require a Pro or Custom package. Please upgrade to unlock." });
        }

        if (string.IsNullOrWhiteSpace(model.CommentText))
        {
            return BadRequest(new { error = "Comment cannot be empty" });
        }

        var comment = await trackerService.AddCommentAsync(
            organizationId, model.IssueId, userId, model.CommentText, cancellationToken);

        return Ok(new { success = true, comment });
    }

    [HttpPost("update")]
    public async Task<IActionResult> UpdateIssue([FromForm] UpdateIssueFormModel form, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var request = new UpdateIssueRequest
        {
            Title = form.Title,
            Description = form.Description,
            IssueType = form.IssueType,
            Status = form.Status,
            Priority = form.Priority,
            AssigneeId = form.AssigneeId == Guid.Empty ? null : form.AssigneeId,
            DueDate = form.DueDate.HasValue ? DateTime.SpecifyKind(form.DueDate.Value, DateTimeKind.Utc) : null,
            EstimatedHours = form.EstimatedHours,
            SpentHours = form.SpentHours
        };

        await trackerService.UpdateIssueAsync(organizationId, form.IssueId, userId, request, cancellationToken);
        TempData["SuccessMessage"] = "Ticket updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:guid}")]
    public async Task<IActionResult> DeleteIssue(Guid id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        await trackerService.DeleteIssueAsync(organizationId, id, userId, cancellationToken);
        TempData["SuccessMessage"] = "Ticket deleted successfully.";
        return RedirectToAction(nameof(Index));
    }
}

