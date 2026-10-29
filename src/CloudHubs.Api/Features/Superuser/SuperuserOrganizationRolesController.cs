using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudHubs.Application.Organizations;

namespace CloudHubs.Api.Features.Superuser;

[Route("su/organization/{organizationId:guid}/roles")]
[Authorize(Roles = "Superuser")]
[AutoValidateAntiforgeryToken]
public class SuperuserOrganizationRolesController(
    OrganizationService organizationService,
    OrganizationRoleService roleService,
    ILogger<SuperuserOrganizationRolesController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await organizationService.GetOrganizationAsync(organizationId, cancellationToken);
        if (organization is null) return NotFound();

        ViewData["Section"] = "Organizations";
        ViewData["Title"] = "Organization roles";
        return View("~/Views/superuser/Pages/OrganizationRoles.cshtml", new OrganizationRolesIndexViewModel
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            Roles = await roleService.GetRolesAsync(organizationId, cancellationToken),
            Members = await roleService.GetMembersAsync(organizationId, cancellationToken)
        });
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await organizationService.GetOrganizationAsync(organizationId, cancellationToken);
        if (organization is null) return NotFound();
        SetPageMetadata("Create organization role");
        return View("~/Views/superuser/Pages/OrganizationRoleForm.cshtml", new OrganizationRoleFormViewModel
        {
            OrganizationId = organizationId
        });
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(
        Guid organizationId,
        OrganizationRoleFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.OrganizationId = organizationId;
        if (!ModelState.IsValid)
        {
            SetPageMetadata("Create organization role");
            return View("~/Views/superuser/Pages/OrganizationRoleForm.cshtml", model);
        }

        try
        {
            await roleService.CreateRoleAsync(
                organizationId,
                model.Name,
                model.Description,
                model.ToPermissions(),
                cancellationToken);
            TempData["Success"] = "Organization role created.";
            return RedirectToAction(nameof(Index), new { organizationId });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to create organization role for {OrganizationId}.", organizationId);
            ModelState.AddModelError(string.Empty, "The role could not be saved. Check the database connection and try again.");
        }

        SetPageMetadata("Create organization role");
        return View("~/Views/superuser/Pages/OrganizationRoleForm.cshtml", model);
    }

    [HttpGet("{roleId:guid}/edit")]
    public async Task<IActionResult> Edit(Guid organizationId, Guid roleId, CancellationToken cancellationToken)
    {
        var role = await roleService.GetRoleAsync(organizationId, roleId, cancellationToken);
        if (role is null) return NotFound();
        SetPageMetadata("Edit organization role");
        return View("~/Views/superuser/Pages/OrganizationRoleForm.cshtml", OrganizationRoleFormViewModel.From(role));
    }

    [HttpPost("{roleId:guid}/edit")]
    public async Task<IActionResult> Edit(
        Guid organizationId,
        Guid roleId,
        OrganizationRoleFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.OrganizationId = organizationId;
        model.Id = roleId;
        if (!ModelState.IsValid)
        {
            SetPageMetadata("Edit organization role");
            return View("~/Views/superuser/Pages/OrganizationRoleForm.cshtml", model);
        }

        try
        {
            if (await roleService.UpdateRoleAsync(
                    organizationId,
                    roleId,
                    model.Name,
                    model.Description,
                    model.ToPermissions(),
                    cancellationToken) is null)
            {
                return NotFound();
            }

            TempData["Success"] = "Organization role updated.";
            return RedirectToAction(nameof(Index), new { organizationId });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to update organization role {RoleId}.", roleId);
            ModelState.AddModelError(string.Empty, "The role could not be saved. Check the database connection and try again.");
        }

        SetPageMetadata("Edit organization role");
        return View("~/Views/superuser/Pages/OrganizationRoleForm.cshtml", model);
    }

    [HttpPost("{roleId:guid}/delete")]
    public async Task<IActionResult> Delete(Guid organizationId, Guid roleId, CancellationToken cancellationToken)
    {
        try
        {
            if (!await roleService.DeleteRoleAsync(organizationId, roleId, cancellationToken)) return NotFound();
            TempData["Success"] = "Organization role deleted.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to delete organization role {RoleId}.", roleId);
            TempData["Error"] = "The role could not be deleted. Check the database connection and try again.";
        }

        return RedirectToAction(nameof(Index), new { organizationId });
    }

    [HttpPost("members/{userId:guid}/role")]
    public async Task<IActionResult> AssignRole(
        Guid organizationId,
        Guid userId,
        Guid? roleId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await roleService.AssignRoleAsync(organizationId, userId, roleId, cancellationToken))
            {
                return NotFound();
            }

            TempData["Success"] = "Member role updated.";
        }
        catch (ArgumentException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to assign a role to member {UserId} in organization {OrganizationId}.", userId, organizationId);
            TempData["Error"] = "The member role could not be saved. Check the database connection and try again.";
        }

        return RedirectToAction(nameof(Index), new { organizationId });
    }

    private void SetPageMetadata(string title)
    {
        ViewData["Section"] = "Organizations";
        ViewData["Title"] = title;
    }
}
