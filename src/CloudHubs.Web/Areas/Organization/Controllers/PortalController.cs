using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using CloudHubs.Application.Modules.Organizations.DTOs;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Application.Modules.Subscriptions.Services;
using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.Web.Security;
using CloudHubs.Web.ViewModels.Auth;
using CloudHubs.Web.ViewModels.Organizations;
using CloudHubs.Web.ViewModels.Projects;

namespace CloudHubs.Web.Areas.Organization.Controllers;

[Area("Organization")]
[Route("organization")]
[AutoValidateAntiforgeryToken]
public class PortalController(
    IUserRepository userRepository,
    IPasswordHasher<User> passwordHasher,
    IOrganizationRoleRepository roleRepository,
    OrganizationTenantService tenantService,
    OrganizationPermissionService permissionService,
    OrganizationService organizationService,
    AccountSubscriptionService accountSubscriptionService,
    ILogger<PortalController> logger) : Controller
{
    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login(Guid? oId)
    {
        return View("Login", new OrganizationLoginViewModel
        {
            OrganizationContext = oId?.ToString()
        });
    }

    [HttpPost("/login")]
    [AllowAnonymous]
    [EnableRateLimiting("tenant-login")]
    public async Task<IActionResult> Login(
        OrganizationLoginViewModel model,
        [FromForm(Name = "oId")] string? organizationIdValue,
        CancellationToken cancellationToken)
    {
        model.OrganizationContext = organizationIdValue;
        Guid? organizationId = null;
        if (!string.IsNullOrWhiteSpace(organizationIdValue))
        {
            if (!Guid.TryParse(organizationIdValue, out var parsedOrganizationId))
            {
                ModelState.AddModelError(string.Empty, "The organization sign-in link is invalid. Open the link provided by your organization.");
            }
            else
            {
                organizationId = parsedOrganizationId;
            }
        }

        if (!ModelState.IsValid)
        {
            return View("Login", model);
        }

        var user = await userRepository.GetByEmailAsync(model.Identifier, cancellationToken)
            ?? await userRepository.GetByLoginIdAsync(model.Identifier, cancellationToken);
        if (user is null
            || !user.IsActive
            || user.Role == UserRole.Superuser
            || string.IsNullOrWhiteSpace(user.PasswordHash)
            || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Email or member ID and password are incorrect, or this account has no active organization.");
            model.Password = string.Empty;
            return View("Login", model);
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(user.Id, cancellationToken);
        var activeMemberships = memberships
            .Where(membership => membership.Organization.IsAvailableForUse())
            .ToArray();
        if (activeMemberships.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Email or member ID and password are incorrect, or this account has no active organization.");
            model.Password = string.Empty;
            return View("Login", model);
        }

        var selectedMembership = organizationId is Guid selectedOrganizationId
            ? activeMemberships.FirstOrDefault(membership => membership.OrganizationId == selectedOrganizationId)
            : activeMemberships.Length == 1 ? activeMemberships[0] : null;
        if (organizationId.HasValue && selectedMembership is null)
        {
            ModelState.AddModelError(string.Empty, "This account does not have access to the organization in that sign-in link.");
            model.Password = string.Empty;
            return View("Login", model);
        }

        await SignInAdminAsync(user, selectedMembership?.OrganizationId);

        logger.LogInformation("Organization portal sign-in completed for user {UserId}.", user.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult LegacyLogin(Guid? oId, string? m)
    {
        return RedirectToAction(nameof(Login), new { oId });
    }

    [HttpGet("")]
    [Authorize(
        AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme,
        Roles = "OrganizationMember")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var selectedOrganizationId = User.FindFirstValue("OrganizationId");
        var activeMemberships = memberships
            .Where(membership => membership.Organization.IsAvailableForUse())
            .ToArray();
        if (activeMemberships.Length == 0)
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        OrganizationMembership? currentMembership = null;
        if (selectedOrganizationId is not null)
        {
            if (!Guid.TryParse(selectedOrganizationId, out var organizationId))
            {
                await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(Login));
            }

            currentMembership = activeMemberships.FirstOrDefault(candidate => candidate.OrganizationId == organizationId);
            if (currentMembership is null)
            {
                await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(Login));
            }
        }
        else if (activeMemberships.Length == 1)
        {
            currentMembership = activeMemberships[0];
        }

        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);
        var accountOrganizationCount = accountSubscription is null
            ? 0
            : await accountSubscriptionService.GetOrganizationCountAsync(accountSubscription.Id, cancellationToken);
        var maxOrganizations = accountSubscription?.Plan.MaxOrganizations ?? 0;

        IReadOnlyCollection<Project> projects = [];
        IReadOnlyCollection<OrganizationMembership> members = [];
        var canViewProjects = false;
        var canManageProjects = false;
        var canViewMembers = false;
        var canManageMembers = false;
        OrganizationPortalCurrentOrganizationViewModel? currentOrganization = null;
        if (currentMembership is not null)
        {
            var organization = currentMembership.Organization;
            var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
            canViewProjects = await permissionService.HasPermissionAsync(
                organization.Id, userId, OrganizationPermission.ViewProjects, cancellationToken);
            canManageProjects = await permissionService.HasPermissionAsync(
                organization.Id, userId, OrganizationPermission.ManageProjects, cancellationToken);
            canViewMembers = await permissionService.HasPermissionAsync(
                organization.Id, userId, OrganizationPermission.ViewMembers, cancellationToken);
            canManageMembers = await permissionService.HasPermissionAsync(
                organization.Id, userId, OrganizationPermission.ManageMembers, cancellationToken);

            if (canViewProjects)
            {
                projects = await tenantService.GetProjectsAsync(
                    organization.Id, userId, cancellationToken, requireApiAccess: false);
            }

            if (canViewMembers)
            {
                members = await tenantService.GetMembersAsync(
                    organization.Id, userId, cancellationToken, requireApiAccess: false);
            }

            currentOrganization = new OrganizationPortalCurrentOrganizationViewModel(
                organization.Id,
                organization.Name,
                organization.Email,
                organization.Slug,
                currentMembership.CustomRole?.Name ?? currentMembership.Role.ToString(),
                plan?.Name ?? "No plan",
                organization.AccountSubscription?.ExpiresAt ?? organization.Subscription?.ExpiresAt,
                plan?.MaxProjects ?? 0,
                plan?.MaxMembers ?? 0);
        }

        return View("Index", new OrganizationPortalIndexViewModel
        {
            UserName = User.Identity?.Name ?? "Organization member",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Organizations = activeMemberships.Select(membership => new OrganizationPortalMembershipViewModel(
                    membership.OrganizationId,
                    membership.Organization.Name,
                    membership.Organization.Email,
                    membership.Organization.Slug,
                    membership.CustomRole?.Name ?? membership.Role.ToString())).ToArray(),
            CurrentOrganization = currentOrganization,
            Projects = projects.Select(project => new OrganizationPortalProjectViewModel(
                project.Id, project.Name, project.Slug, project.Description, project.CreatedAt)).ToArray(),
            Members = members.Select(membership => new OrganizationPortalMemberViewModel(
                membership.UserId,
                membership.User.LoginId ?? string.Empty,
                membership.User.Name,
                membership.User.Email ?? string.Empty,
                membership.CustomRole?.Name ?? membership.Role.ToString(),
                membership.Role == OrganizationMemberRole.Owner,
                membership.CustomRole is not null)).ToArray(),
            CanViewProjects = canViewProjects,
            CanManageProjects = canManageProjects,
            CanViewMembers = canViewMembers,
            CanManageMembers = canManageMembers,
            RequiresOrganizationSelection = currentMembership is null && activeMemberships.Length > 0,
            CanCreateOrganization = accountSubscription is not null
                && (maxOrganizations == 0 || accountOrganizationCount < maxOrganizations),
            CanShowOrganizationAction = accountSubscription is not null
                || activeMemberships.Any(membership => membership.Role == OrganizationMemberRole.Owner),
            HasAccountSubscription = accountSubscription is not null,
            AccountOrganizationCount = accountOrganizationCount,
            MaxOrganizations = maxOrganizations
        });
    }

    [HttpPost("projects/create")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> CreateProject(
        OrganizationPortalProjectFormModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter a project name and a valid slug using letters, numbers, hyphens, or underscores.";
            return RedirectToAction(nameof(Index));
        }

        return await RunPortalActionAsync(
            userId => tenantService.CreateProjectAsync(
                model.OrganizationId,
                userId,
                model.Name,
                model.Slug,
                model.Description,
                model.RootPath,
                model.TemplateId,
                cancellationToken,
                requireApiAccess: false),
            "Project created.",
            "The project could not be created.",
            cancellationToken);
    }

    [HttpGet("members")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public Task<IActionResult> Members(CancellationToken cancellationToken) =>
        RenderMembersPageAsync(null, cancellationToken);

    private async Task<IActionResult> RenderMembersPageAsync(
        OrganizationPortalMemberFormModel? memberForm,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction(nameof(Index));
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships
            .FirstOrDefault(candidate =>
                candidate.OrganizationId == organizationId
                && candidate.Organization.IsAvailableForUse());
        if (membership is null
            || !await permissionService.HasPermissionAsync(
                organizationId, userId, OrganizationPermission.ViewMembers, cancellationToken))
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        var organization = membership.Organization;
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);
        var members = await tenantService.GetMembersAsync(
            organizationId, userId, cancellationToken, requireApiAccess: false);

        return View("Members", new OrganizationPortalIndexViewModel
        {
            UserName = User.Identity?.Name ?? "Organization member",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Organizations = memberships
                .Where(candidate => candidate.Organization.IsAvailableForUse())
                .Select(candidate => new OrganizationPortalMembershipViewModel(
                    candidate.OrganizationId,
                    candidate.Organization.Name,
                    candidate.Organization.Email,
                    candidate.Organization.Slug,
                    candidate.CustomRole?.Name ?? candidate.Role.ToString()))
                .ToArray(),
            CurrentOrganization = new OrganizationPortalCurrentOrganizationViewModel(
                organization.Id,
                organization.Name,
                organization.Email,
                organization.Slug,
                membership.CustomRole?.Name ?? membership.Role.ToString(),
                plan?.Name ?? "No plan",
                organization.AccountSubscription?.ExpiresAt ?? organization.Subscription?.ExpiresAt,
                plan?.MaxProjects ?? 0,
                plan?.MaxMembers ?? 0),
            Members = members.Select(item => new OrganizationPortalMemberViewModel(
                item.UserId,
                item.User.LoginId ?? string.Empty,
                item.User.Name,
                item.User.Email ?? string.Empty,
                item.CustomRole?.Name ?? item.Role.ToString(),
                item.Role == OrganizationMemberRole.Owner,
                item.CustomRole is not null)).ToArray(),
            CanViewProjects = await permissionService.HasPermissionAsync(
                organizationId, userId, OrganizationPermission.ViewProjects, cancellationToken),
            CanViewMembers = true,
            CanManageMembers = await permissionService.HasPermissionAsync(
                organizationId, userId, OrganizationPermission.ManageMembers, cancellationToken),
            CanShowOrganizationAction = accountSubscription is not null
                || memberships.Any(item => item.Role == OrganizationMemberRole.Owner),
            HasAccountSubscription = accountSubscription is not null,
            AccountOrganizationCount = accountSubscription is null
                ? 0
                : await accountSubscriptionService.GetOrganizationCountAsync(accountSubscription.Id, cancellationToken),
            MaxOrganizations = accountSubscription?.Plan.MaxOrganizations ?? 0,
            MemberFormEmail = memberForm?.Email ?? string.Empty,
            MemberFormName = memberForm?.Name ?? string.Empty,
            MemberFormRole = memberForm?.Role ?? "Developer",
            MemberFormOpen = memberForm is not null
        });
    }

    [HttpPost("projects/delete")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public Task<IActionResult> DeleteProject(
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        RunPortalActionAsync(
            async userId => await tenantService.DeleteProjectAsync(
                organizationId, projectId, userId, cancellationToken, requireApiAccess: false),
            "Project deleted.",
            "The project could not be deleted.",
            cancellationToken);

    [HttpPost("members/add")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> AddMember(
        OrganizationPortalMemberFormModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid
            || !Enum.TryParse<OrganizationMemberRole>(model.Role, true, out var role)
            || role is OrganizationMemberRole.Owner
            || !Enum.IsDefined(role))
        {
            if (ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty, "Choose Admin, Developer, or Viewer.");
            }

            return await RenderMembersPageAsync(model, cancellationToken);
        }

        try
        {
            var existingUser = await userRepository.GetByEmailAsync(model.Email, cancellationToken);
            OrganizationMembership membership;
            if (existingUser is not null)
            {
                membership = await tenantService.AddMemberAsync(
                    model.OrganizationId,
                    GetSignedInUserId(),
                    model.Email,
                    role,
                    cancellationToken,
                    requireApiAccess: false);
                TempData["Success"] =
                    $"Existing account added. Member ID: {membership.User.LoginId}. Its password was not changed.";
            }
            else
            {
                var member = new User
                {
                    Name = model.Name.Trim(),
                    Email = model.Email.Trim().ToLowerInvariant(),
                    Role = role switch
                    {
                        OrganizationMemberRole.Admin => UserRole.OrganizationAdmin,
                        OrganizationMemberRole.Viewer => UserRole.Viewer,
                        _ => UserRole.Developer
                    },
                    IsActive = true
                };
                member.PasswordHash = passwordHasher.HashPassword(member, model.Password);
                membership = await tenantService.CreateMemberAsync(
                    model.OrganizationId,
                    GetSignedInUserId(),
                    member,
                    role,
                    cancellationToken,
                    requireApiAccess: false);
                TempData["Success"] =
                    $"Member created. Member ID: {membership.User.LoginId}. The password you provided is set.";
            }

            return RedirectToAction(nameof(Members));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is (ArgumentException or InvalidOperationException))
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await RenderMembersPageAsync(model, cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Unable to add member to organization {OrganizationId}.", model.OrganizationId);
            ModelState.AddModelError(string.Empty, "The member could not be added. Please try again later.");
            return await RenderMembersPageAsync(model, cancellationToken);
        }
    }

    [HttpPost("members/update-role")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> UpdateMemberRole(
        Guid organizationId,
        Guid memberId,
        string role,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrganizationMemberRole>(role, true, out var parsedRole)
            || parsedRole is OrganizationMemberRole.Owner
            || !Enum.IsDefined(parsedRole))
        {
            TempData["Error"] = "Choose Admin, Developer, or Viewer.";
            return RedirectToAction(nameof(Members));
        }

        return await RunPortalActionAsync(
            async userId => await tenantService.UpdateMemberRoleAsync(
                organizationId,
                userId,
                memberId,
                parsedRole,
                customRoleId: null,
                cancellationToken,
                requireApiAccess: false),
            "Member role updated.",
            "The member role could not be updated.",
            cancellationToken,
            nameof(Members));
    }

    [HttpPost("members/remove")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public Task<IActionResult> RemoveMember(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken) =>
        RunPortalActionAsync(
            async userId => await tenantService.RemoveMemberAsync(
                organizationId, userId, memberId, cancellationToken, requireApiAccess: false),
            "Member removed from the organization.",
            "The member could not be removed.",
            cancellationToken,
            nameof(Members));

    [HttpPost("workspaces")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> ShowWorkspaces(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        if (user is null
            || !user.IsActive
            || user.Role == UserRole.Superuser
            || !memberships.Any(membership => membership.Organization.IsAvailableForUse()))
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        await SignInAdminAsync(user, organizationId: null);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("create")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> CreateOrganization(CancellationToken cancellationToken)
    {
        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);
        if (accountSubscription is null)
        {
            return View("SubscriptionRequired");
        }

        var organizationCount = await accountSubscriptionService.GetOrganizationCountAsync(
            accountSubscription.Id, cancellationToken);
        var maxOrganizations = accountSubscription.Plan.MaxOrganizations;
        return View("Create", new OrganizationPortalCreateViewModel
        {
            ContactEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            PlanName = accountSubscription.Plan.Name,
            ExpiresAt = accountSubscription.ExpiresAt,
            OrganizationCount = organizationCount,
            MaxOrganizations = maxOrganizations,
            CanCreateOrganization = maxOrganizations == 0 || organizationCount < maxOrganizations
        });
    }

    [HttpPost("create")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
    public async Task<IActionResult> CreateOrganization(
        OrganizationPortalCreateViewModel model,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(Login));
            }

            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            var accountSubscription = await accountSubscriptionService.GetOrCreateAccountSubscriptionFromOwnedOrganizationAsync(
                userId, cancellationToken);
            if (user is null || accountSubscription is null || !accountSubscription.IsAvailable())
            {
                return View("SubscriptionRequired");
            }

            model.PlanName = accountSubscription.Plan.Name;
            model.ExpiresAt = accountSubscription.ExpiresAt;
            model.MaxOrganizations = accountSubscription.Plan.MaxOrganizations;
            model.OrganizationCount = await accountSubscriptionService.GetOrganizationCountAsync(
                accountSubscription.Id, cancellationToken);
            model.CanCreateOrganization = model.MaxOrganizations == 0
                || model.OrganizationCount < model.MaxOrganizations;
            if (!ModelState.IsValid)
            {
                return View("Create", model);
            }

            if (!model.CanCreateOrganization)
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"Your {model.PlanName} plan allows up to {model.MaxOrganizations} organizations. Upgrade your plan to create more.");
                return View("Create", model);
            }

            var organization = await organizationService.CreateOrganizationAsync(
                new CreateOrganizationRequest(model.Name, model.ContactEmail, string.Empty, string.Empty, string.Empty),
                cancellationToken,
                [new OrganizationMembership { User = user, Role = OrganizationMemberRole.Owner }],
                accountSubscription.PlanId,
                accountSubscription);

            await SignInAdminAsync(user, organization.Id);
            TempData["Success"] = "Organization created under your account subscription.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is (ArgumentException or InvalidOperationException))
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                && await accountSubscriptionService.GetOrCreateAccountSubscriptionFromOwnedOrganizationAsync(
                    userId, cancellationToken) is { } subscription)
            {
                model.PlanName = subscription.Plan.Name;
                model.ExpiresAt = subscription.ExpiresAt;
                model.MaxOrganizations = subscription.Plan.MaxOrganizations;
                model.OrganizationCount = await accountSubscriptionService.GetOrganizationCountAsync(
                    subscription.Id, cancellationToken);
                model.CanCreateOrganization = model.MaxOrganizations == 0
                    || model.OrganizationCount < model.MaxOrganizations;
            }
            return View("Create", model);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to create organization for account {UserId}.", User.FindFirstValue(ClaimTypes.NameIdentifier));
            ModelState.AddModelError(string.Empty, "The organization could not be created. Please try again later.");
            return View("Create", model);
        }
    }

    [HttpPost("select")]
    [Authorize(
        AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme,
        Roles = "OrganizationMember")]
    public async Task<IActionResult> SelectOrganization(Guid organizationId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || user.Role == UserRole.Superuser)
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships.FirstOrDefault(candidate =>
            candidate.OrganizationId == organizationId && candidate.Organization.IsAvailableForUse());
        if (membership is null)
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        await SignInAdminAsync(user, membership.OrganizationId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInAdminAsync(User user, Guid? organizationId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role, "OrganizationMember")
        };
        if (organizationId is Guid selectedOrganizationId)
        {
            claims.Add(new Claim("OrganizationId", selectedOrganizationId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, TenantCookieDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            TenantCookieDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });
    }

    private async Task<AccountSubscription?> GetCurrentAccountSubscriptionAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return null;
        }

        var subscription = await accountSubscriptionService.GetOrCreateAccountSubscriptionFromOwnedOrganizationAsync(
            userId, cancellationToken);
        return subscription is { } accountSubscription && accountSubscription.IsAvailable()
            ? accountSubscription
            : null;
    }

    private Guid GetSignedInUserId()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            throw new UnauthorizedAccessException("Your session has expired. Sign in again.");
        }

        return userId;
    }

    private async Task<IActionResult> RunPortalActionAsync(
        Func<Guid, Task> action,
        string successMessage,
        string genericError,
        CancellationToken cancellationToken,
        string redirectAction = nameof(Index))
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await HttpContext.SignOutAsync(TenantCookieDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        try
        {
            await action(userId);
            TempData["Success"] = successMessage;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is (ArgumentException or InvalidOperationException))
        {
            TempData["Error"] = exception.Message;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "{Message} Organization portal user {UserId}.", genericError, userId);
            TempData["Error"] = genericError;
        }

        return RedirectToAction(redirectAction);
    }

}
