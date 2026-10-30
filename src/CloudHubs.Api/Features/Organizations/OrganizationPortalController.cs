using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using CloudHubs.Application.Organizations;
using CloudHubs.Domain.Projects;
using CloudHubs.Application.Users;
using CloudHubs.Contracts.Organizations;
using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Subscriptions;
using CloudHubs.Domain.Users;

namespace CloudHubs.Api.Features.Organizations;

public static class TenantCookieDefaults
{
    public const string AuthenticationScheme = "TenantCookie";
}

[Route("organization")]
[AutoValidateAntiforgeryToken]
public class OrganizationPortalController(
    IUserRepository userRepository,
    IPasswordHasher<User> passwordHasher,
    IOrganizationRoleRepository roleRepository,
    OrganizationTenantService tenantService,
    OrganizationPermissionService permissionService,
    OrganizationService organizationService,
    AccountSubscriptionService accountSubscriptionService,
    ILogger<OrganizationPortalController> logger) : Controller
{
    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login(Guid? oId)
    {
        return View("~/Views/organization/Login.cshtml", new OrganizationLoginViewModel
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
            return View("~/Views/organization/Login.cshtml", model);
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
            return View("~/Views/organization/Login.cshtml", model);
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(user.Id, cancellationToken);
        var activeMemberships = memberships
            .Where(membership => membership.Organization.IsAvailableForUse())
            .ToArray();
        if (activeMemberships.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Email or member ID and password are incorrect, or this account has no active organization.");
            model.Password = string.Empty;
            return View("~/Views/organization/Login.cshtml", model);
        }

        var selectedMembership = organizationId is Guid selectedOrganizationId
            ? activeMemberships.FirstOrDefault(membership => membership.OrganizationId == selectedOrganizationId)
            : activeMemberships.Length == 1 ? activeMemberships[0] : null;
        if (organizationId.HasValue && selectedMembership is null)
        {
            ModelState.AddModelError(string.Empty, "This account does not have access to the organization in that sign-in link.");
            model.Password = string.Empty;
            return View("~/Views/organization/Login.cshtml", model);
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

        return View("~/Views/organization/Index.cshtml", new OrganizationPortalIndexViewModel
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
    public async Task<IActionResult> Members(CancellationToken cancellationToken)
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

        return View("~/Views/organization/Members.cshtml", new OrganizationPortalIndexViewModel
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
            MaxOrganizations = accountSubscription?.Plan.MaxOrganizations ?? 0
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
            TempData["Error"] = "Enter a valid name and email, choose a role, and use an 8-character password.";
            return RedirectToAction(nameof(Members));
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
                var memberId = await GenerateMemberIdAsync(cancellationToken);
                var member = new User
                {
                    Name = model.Name.Trim(),
                    Email = model.Email.Trim().ToLowerInvariant(),
                    LoginId = memberId,
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
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Members));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to add member to organization {OrganizationId}.", model.OrganizationId);
            TempData["Error"] = "The member could not be added. Please try again later.";
            return RedirectToAction(nameof(Members));
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
            return View("~/Views/organization/SubscriptionRequired.cshtml");
        }

        var organizationCount = await accountSubscriptionService.GetOrganizationCountAsync(
            accountSubscription.Id, cancellationToken);
        var maxOrganizations = accountSubscription.Plan.MaxOrganizations;
        return View("~/Views/organization/Create.cshtml", new OrganizationPortalCreateViewModel
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
                return View("~/Views/organization/SubscriptionRequired.cshtml");
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
                return View("~/Views/organization/Create.cshtml", model);
            }

            if (!model.CanCreateOrganization)
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"Your {model.PlanName} plan allows up to {model.MaxOrganizations} organizations. Upgrade your plan to create more.");
                return View("~/Views/organization/Create.cshtml", model);
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
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
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
            return View("~/Views/organization/Create.cshtml", model);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to create organization for account {UserId}.", User.FindFirstValue(ClaimTypes.NameIdentifier));
            ModelState.AddModelError(string.Empty, "The organization could not be created. Please try again later.");
            return View("~/Views/organization/Create.cshtml", model);
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

    private async Task<string> GenerateMemberIdAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var memberId = $"ID{Convert.ToHexString(RandomNumberGenerator.GetBytes(3))}";
            if (await userRepository.GetByLoginIdAsync(memberId, cancellationToken) is null)
            {
                return memberId;
            }
        }

        throw new InvalidOperationException("A unique member ID could not be generated. Please try again.");
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
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
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

public sealed class OrganizationLoginViewModel
{
    public string? OrganizationContext { get; set; }

    [Required, StringLength(200)]
    public string Identifier { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}

public sealed class OrganizationPortalIndexViewModel
{
    public string UserName { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public IReadOnlyCollection<OrganizationPortalMembershipViewModel> Organizations { get; init; } = [];
    public OrganizationPortalCurrentOrganizationViewModel? CurrentOrganization { get; init; }
    public IReadOnlyCollection<OrganizationPortalProjectViewModel> Projects { get; init; } = [];
    public IReadOnlyCollection<OrganizationPortalMemberViewModel> Members { get; init; } = [];
    public bool CanViewProjects { get; init; }
    public bool CanManageProjects { get; init; }
    public bool CanViewMembers { get; init; }
    public bool CanManageMembers { get; init; }
    public bool RequiresOrganizationSelection { get; init; }
    public bool CanCreateOrganization { get; init; }
    public bool CanShowOrganizationAction { get; init; }
    public bool HasAccountSubscription { get; init; }
    public int AccountOrganizationCount { get; init; }
    public int MaxOrganizations { get; init; }
}

public sealed class OrganizationPortalCreateViewModel
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string ContactEmail { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public int OrganizationCount { get; set; }
    public int MaxOrganizations { get; set; }
    public bool CanCreateOrganization { get; set; }
}

public sealed record OrganizationPortalMembershipViewModel(
    Guid Id,
    string Name,
    string Email,
    string Slug,
    string Role);

public sealed record OrganizationPortalCurrentOrganizationViewModel(
    Guid Id,
    string Name,
    string Email,
    string Slug,
    string Role,
    string PlanName,
    DateTime? ExpiresAt,
    int MaxProjects,
    int MaxMembers);

public sealed record OrganizationPortalProjectViewModel(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    DateTime CreatedAt);

public sealed record OrganizationPortalMemberViewModel(
    Guid UserId,
    string MemberId,
    string Name,
    string Email,
    string Role,
    bool IsOwner,
    bool IsCustomRole);

public sealed class OrganizationPortalProjectFormModel
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(120)]
    [RegularExpression("^[a-zA-Z0-9_-]+$")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(500)]
    public string RootPath { get; set; } = string.Empty;

    [StringLength(150)]
    public string TemplateId { get; set; } = "default";

    public Guid OrganizationId { get; set; }
}

public sealed class OrganizationPortalMemberFormModel
{
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Admin|Developer|Viewer)$")]
    public string Role { get; set; } = "Developer";

    [Required, StringLength(8, MinimumLength = 8, ErrorMessage = "Password must be exactly 8 characters.")]
    public string Password { get; set; } = string.Empty;

    public Guid OrganizationId { get; set; }
}
