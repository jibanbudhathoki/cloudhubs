using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudHubs.Application.Modules.Docs.Interfaces;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Application.Modules.Projects.Interfaces;
using CloudHubs.Application.Modules.Subscriptions.Services;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.Web.Security;
using CloudHubs.Web.ViewModels.Docs;
using CloudHubs.Web.ViewModels.Organizations;

using CloudHubs.Infrastructure.Data;
using CloudHubs.Application.Modules.Subscriptions.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CloudHubs.Web.Areas.Organization.Controllers;

[Area("Organization")]
[Route("organization/docs")]
[Authorize(AuthenticationSchemes = TenantCookieDefaults.AuthenticationScheme, Roles = "OrganizationMember")]
[AutoValidateAntiforgeryToken]
public class DocsController(
    IDocsService docsService,
    IOrganizationRoleRepository roleRepository,
    OrganizationPermissionService permissionService,
    IOrganizationProjectRepository projectRepository,
    AccountSubscriptionService accountSubscriptionService,
    IOrganizationRepository organizationRepository,
    ISubscriptionPlanRepository planRepository,
    OrganizationTenantService tenantService,
    CloudHubsDbContext dbContext) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships
            .FirstOrDefault(candidate => candidate.OrganizationId == organizationId && candidate.Organization.IsAvailableForUse());
        if (membership is null)
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        var organization = membership.Organization;
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        var canManageDocs = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken);
        var canViewMembers = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ViewMembers, cancellationToken);

        var docs = (await docsService.GetDocsAsync(organizationId, userId, cancellationToken)).ToList();
        var projects = (await projectRepository.GetAllAsync(organizationId, cancellationToken)).ToList();

        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);

        return View("Index", new OrganizationDocsViewModel
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            OrganizationSlug = organization.Slug,
            OrganizationRole = membership.Role.ToString(),
            OrganizationApiKey = organization.ApiKey,
            OrganizationBaseApiUrl = organization.BaseApiUrl,
            SubscriptionPlanName = plan?.Name ?? "Basic",
            HasApiAccess = plan?.HasApiAccess ?? false,
            IsProOrCustomPlan = (plan?.HasApiAccess == true) || (plan?.Name is "Pro" or "Custom"),
            Docs = docs,
            SelectedDoc = null,
            Projects = projects,
            CanManageDocs = canManageDocs,
            CanViewMembers = canViewMembers,
            CanViewProjects = true,
            UserName = User.Identity?.Name ?? "Member",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Organizations = memberships
                .Where(candidate => candidate.Organization.IsAvailableForUse())
                .Select(candidate => new OrganizationPortalMembershipViewModel(
                    candidate.Organization.Id,
                    candidate.Organization.Name,
                    candidate.Organization.Email,
                    candidate.Organization.Slug,
                    candidate.Role.ToString()))
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
            CanShowOrganizationAction = accountSubscription is not null
                || memberships.Any(m => m.Role == OrganizationMemberRole.Owner)
        });
    }

    [HttpGet("{docId:guid}")]
    public async Task<IActionResult> Details(Guid docId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships
            .FirstOrDefault(candidate => candidate.OrganizationId == organizationId && candidate.Organization.IsAvailableForUse());
        if (membership is null)
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        var organization = membership.Organization;
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        var canManageDocs = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken);
        var canViewMembers = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ViewMembers, cancellationToken);

        var doc = await docsService.GetDocByIdAsync(organizationId, docId, userId, cancellationToken);
        if (doc is null)
        {
            TempData["Error"] = "Documentation was not found.";
            return RedirectToAction(nameof(Index));
        }

        var projects = (await projectRepository.GetAllAsync(organizationId, cancellationToken)).ToList();
        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);

        return View("Detail", new OrganizationDocsViewModel
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            OrganizationSlug = organization.Slug,
            OrganizationRole = membership.Role.ToString(),
            OrganizationApiKey = organization.ApiKey,
            Docs = [doc],
            SelectedDoc = doc,
            Projects = projects,
            CanManageDocs = canManageDocs,
            CanViewMembers = canViewMembers,
            CanViewProjects = true,
            UserName = User.Identity?.Name ?? "Member",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Organizations = memberships
                .Where(candidate => candidate.Organization.IsAvailableForUse())
                .Select(candidate => new OrganizationPortalMembershipViewModel(
                    candidate.Organization.Id,
                    candidate.Organization.Name,
                    candidate.Organization.Email,
                    candidate.Organization.Slug,
                    candidate.Role.ToString()))
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
            CanShowOrganizationAction = accountSubscription is not null
                || memberships.Any(m => m.Role == OrganizationMemberRole.Owner)
        });
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships
            .FirstOrDefault(candidate => candidate.OrganizationId == organizationId && candidate.Organization.IsAvailableForUse());
        if (membership is null
            || !await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken))
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        var organization = membership.Organization;
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        var projects = (await projectRepository.GetAllAsync(organizationId, cancellationToken)).ToList();
        var members = await roleRepository.GetMembersAsync(organizationId, cancellationToken);
        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);
        var canViewMembers = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ViewMembers, cancellationToken);

        return View("Editor", new OrganizationDocEditorViewModel
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            OrganizationSlug = organization.Slug,
            OrganizationRole = membership.Role.ToString(),
            OrganizationApiKey = organization.ApiKey,
            CanViewMembers = canViewMembers,
            CanViewProjects = true,
            IsEdit = false,
            Projects = projects,
            OrganizationMembers = members,
            Form = new OrganizationDocFormModel
            {
                BaseUrl = null,
                ApiKey = null,
                TestApiKey = null,
                Category = "API",
                IsPublicToMembers = true,
                AllowedMemberUserIds = [],
                Content = "# API Documentation & Endpoints\n\n## Overview\nExplain this API or service overview here.\n\n## Endpoints\n\n### `GET /api/v1/resource`\nReturns a list of items.\n\n```json\n{\n  \"status\": \"success\",\n  \"data\": []\n}\n```\n\n### `POST /api/v1/resource`\nCreate a new item.\n",
                EnvironmentVariables = "PORT=3000\nNODE_ENV=development"
            },
            UserName = User.Identity?.Name ?? "Member",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Organizations = memberships
                .Where(candidate => candidate.Organization.IsAvailableForUse())
                .Select(candidate => new OrganizationPortalMembershipViewModel(
                    candidate.Organization.Id,
                    candidate.Organization.Name,
                    candidate.Organization.Email,
                    candidate.Organization.Slug,
                    candidate.Role.ToString()))
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
            CanShowOrganizationAction = accountSubscription is not null
                || memberships.Any(m => m.Role == OrganizationMemberRole.Owner)
        });
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(
        OrganizationDocFormModel model,
        IFormFile? postmanFile,
        IFormFile? envFile,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            TempData["Error"] = "Document title is required.";
            return RedirectToAction(nameof(Create));
        }

        if (postmanFile is { Length: > 0 })
        {
            using var reader = new StreamReader(postmanFile.OpenReadStream());
            model.PostmanCollection = await reader.ReadToEndAsync(cancellationToken);
        }

        if (envFile is { Length: > 0 })
        {
            using var reader = new StreamReader(envFile.OpenReadStream());
            model.EnvironmentVariables = await reader.ReadToEndAsync(cancellationToken);
        }

        try
        {
            var doc = await docsService.CreateDocAsync(
                organizationId,
                userId,
                model.Title,
                model.Slug,
                model.Category,
                model.Description,
                model.BaseUrl,
                model.ApiKey,
                model.TestApiKey,
                model.SecretKey,
                model.EnvironmentVariables,
                model.PostmanCollection,
                model.Content,
                model.ProjectId,
                model.IsPublicToMembers,
                model.AllowedMemberUserIds,
                cancellationToken);

            TempData["Success"] = $"Documentation for '{doc.Title}' created successfully.";
            return RedirectToAction(nameof(Details), new { docId = doc.Id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Create));
        }
    }

    [HttpGet("{docId:guid}/edit")]
    public async Task<IActionResult> Edit(Guid docId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships
            .FirstOrDefault(candidate => candidate.OrganizationId == organizationId && candidate.Organization.IsAvailableForUse());
        if (membership is null
            || !await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken))
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        var doc = await docsService.GetDocByIdAsync(organizationId, docId, userId, cancellationToken);
        if (doc is null)
        {
            TempData["Error"] = "Documentation profile was not found.";
            return RedirectToAction(nameof(Index));
        }

        var organization = membership.Organization;
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        var projects = (await projectRepository.GetAllAsync(organizationId, cancellationToken)).ToList();
        var members = await roleRepository.GetMembersAsync(organizationId, cancellationToken);
        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);
        var canViewMembers = await permissionService.HasPermissionAsync(
            organizationId, userId, OrganizationPermission.ViewMembers, cancellationToken);

        return View("Editor", new OrganizationDocEditorViewModel
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            OrganizationSlug = organization.Slug,
            OrganizationRole = membership.Role.ToString(),
            OrganizationApiKey = organization.ApiKey,
            CanViewMembers = canViewMembers,
            CanViewProjects = true,
            IsEdit = true,
            DocId = doc.Id,
            Projects = projects,
            OrganizationMembers = members,
            Form = new OrganizationDocFormModel
            {
                Id = doc.Id,
                Title = doc.Title,
                Slug = doc.Slug,
                Category = doc.Category,
                Description = doc.Description,
                BaseUrl = doc.BaseUrl,
                ApiKey = doc.ApiKey,
                TestApiKey = doc.TestApiKey,
                SecretKey = doc.SecretKey,
                EnvironmentVariables = doc.EnvironmentVariables,
                PostmanCollection = doc.PostmanCollection,
                Content = doc.Content,
                ProjectId = doc.ProjectId,
                IsPublicToMembers = doc.IsPublicToMembers,
                AllowedMemberUserIds = doc.AllowedMembers.Select(m => m.UserId).ToList()
            },
            UserName = User.Identity?.Name ?? "Member",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Organizations = memberships
                .Where(candidate => candidate.Organization.IsAvailableForUse())
                .Select(candidate => new OrganizationPortalMembershipViewModel(
                    candidate.Organization.Id,
                    candidate.Organization.Name,
                    candidate.Organization.Email,
                    candidate.Organization.Slug,
                    candidate.Role.ToString()))
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
            CanShowOrganizationAction = accountSubscription is not null
                || memberships.Any(m => m.Role == OrganizationMemberRole.Owner)
        });
    }

    [HttpPost("{docId:guid}/edit")]
    public async Task<IActionResult> Edit(
        Guid docId,
        OrganizationDocFormModel model,
        IFormFile? postmanFile,
        IFormFile? envFile,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            TempData["Error"] = "Document title is required.";
            return RedirectToAction(nameof(Edit), new { docId });
        }

        if (postmanFile is { Length: > 0 })
        {
            using var reader = new StreamReader(postmanFile.OpenReadStream());
            model.PostmanCollection = await reader.ReadToEndAsync(cancellationToken);
        }

        if (envFile is { Length: > 0 })
        {
            using var reader = new StreamReader(envFile.OpenReadStream());
            model.EnvironmentVariables = await reader.ReadToEndAsync(cancellationToken);
        }

        try
        {
            await docsService.UpdateDocAsync(
                organizationId,
                docId,
                userId,
                model.Title,
                model.Slug,
                model.Category,
                model.Description,
                model.BaseUrl,
                model.ApiKey,
                model.TestApiKey,
                model.SecretKey,
                model.EnvironmentVariables,
                model.PostmanCollection,
                model.Content,
                model.ProjectId,
                model.IsPublicToMembers,
                model.AllowedMemberUserIds,
                cancellationToken);

            TempData["Success"] = $"Documentation '{model.Title}' updated successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { docId });
    }

    [HttpGet("{docId:guid}/download-docs")]
    public async Task<IActionResult> DownloadDocs(Guid docId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var doc = await docsService.GetDocByIdAsync(organizationId, docId, userId, cancellationToken);
        if (doc is null) return NotFound();

        var header = $"# {doc.Title}\n\n" +
            $"- **Category**: {doc.Category}\n" +
            (!string.IsNullOrWhiteSpace(doc.BaseUrl) ? $"- **Base URL**: `{doc.BaseUrl}`\n" : "") +
            (!string.IsNullOrWhiteSpace(doc.Description) ? $"- **Description**: {doc.Description}\n" : "") +
            $"- **Exported On**: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n---\n\n";

        var fullDoc = header + (doc.Content ?? "No documentation body written yet.\n");
        var bytes = System.Text.Encoding.UTF8.GetBytes(fullDoc);
        var filename = $"{doc.Slug}-documentation.md";
        return File(bytes, "text/markdown", filename);
    }

    [HttpGet("{docId:guid}/download-bundle")]
    public async Task<IActionResult> DownloadBundle(Guid docId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var doc = await docsService.GetDocByIdAsync(organizationId, docId, userId, cancellationToken);
        if (doc is null) return NotFound();

        using var memoryStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            // 1. Documentation.md
            var docEntry = archive.CreateEntry("DOCUMENTATION.md");
            using (var stream = docEntry.Open())
            using (var writer = new StreamWriter(stream))
            {
                var content = $"# {doc.Title}\n\nCategory: {doc.Category}\nBase URL: {doc.BaseUrl}\n\n" + (doc.Content ?? "");
                await writer.WriteAsync(content);
            }

            // 2. postman_collection.json
            if (!string.IsNullOrWhiteSpace(doc.PostmanCollection))
            {
                var postmanEntry = archive.CreateEntry("postman_collection.json");
                using var stream = postmanEntry.Open();
                using var writer = new StreamWriter(stream);
                await writer.WriteAsync(doc.PostmanCollection);
            }

            // 3. .env.example
            if (!string.IsNullOrWhiteSpace(doc.EnvironmentVariables))
            {
                var envEntry = archive.CreateEntry(".env.example");
                using var stream = envEntry.Open();
                using var writer = new StreamWriter(stream);
                await writer.WriteAsync(doc.EnvironmentVariables);
            }

            // 4. API_INTEGRATION_GUIDE.md
            var guideEntry = archive.CreateEntry("API_INTEGRATION_GUIDE.md");
            using (var stream = guideEntry.Open())
            using (var writer = new StreamWriter(stream))
            {
                var guide = $"# Quick Integration Guide for {doc.Title}\n\n" +
                    $"## Base URL\n`{doc.BaseUrl ?? "https://api.cloudhubs.com"}`\n\n" +
                    $"## Authorization\nInclude API Key in headers:\n```\nAuthorization: Bearer {doc.ApiKey ?? "YOUR_API_KEY"}\n```\n\n" +
                    $"## cURL Example\n```bash\ncurl -X GET \"{doc.BaseUrl ?? "https://api.cloudhubs.com"}\" \\\n  -H \"Authorization: Bearer {doc.ApiKey ?? "YOUR_API_KEY"}\"\n```\n";
                await writer.WriteAsync(guide);
            }
        }

        memoryStream.Position = 0;
        return File(memoryStream.ToArray(), "application/zip", $"{doc.Slug}-developer-kit.zip");
    }

    [HttpPost("{docId:guid}/delete")]
    public async Task<IActionResult> Delete(Guid docId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        try
        {
            await docsService.DeleteDocAsync(organizationId, docId, userId, cancellationToken);
            TempData["Success"] = "Documentation entry deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{docId:guid}/download-postman")]
    public async Task<IActionResult> DownloadPostman(Guid docId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var doc = await docsService.GetDocByIdAsync(organizationId, docId, userId, cancellationToken);
        if (doc is null) return NotFound();

        var content = doc.PostmanCollection ?? "{}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var filename = $"{doc.Slug}-postman_collection.json";
        return File(bytes, "application/json", filename);
    }

    [HttpGet("{docId:guid}/download-env")]
    public async Task<IActionResult> DownloadEnv(Guid docId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var doc = await docsService.GetDocByIdAsync(organizationId, docId, userId, cancellationToken);
        if (doc is null) return NotFound();

        var content = doc.EnvironmentVariables ?? "# Environment variables\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var filename = $"{doc.Slug}.env";
        return File(bytes, "text/plain", filename);
    }

    [HttpGet("api-hub")]
    public async Task<IActionResult> ApiHub(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var membership = memberships
            .FirstOrDefault(candidate => candidate.OrganizationId == organizationId && candidate.Organization.IsAvailableForUse());
        if (membership is null)
        {
            return Forbid(TenantCookieDefaults.AuthenticationScheme);
        }

        var organization = membership.Organization;
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        var hasApiAccess = plan?.HasApiAccess ?? false;
        var isProOrCustom = hasApiAccess || (plan?.Name is "Pro" or "Custom");

        // Check if there is a pending API request
        var hasPendingRequest = await dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(1) AS \"Value\" FROM \"OrganizationApiRequests\" WHERE \"OrganizationId\" = {0} AND \"Status\" = 'Pending'", organizationId)
            .FirstOrDefaultAsync(cancellationToken) > 0;

        var projects = (await projectRepository.GetAllAsync(organizationId, cancellationToken)).ToList();
        var docs = (await docsService.GetDocsAsync(organizationId, userId, cancellationToken)).ToList();
        var accountSubscription = await GetCurrentAccountSubscriptionAsync(cancellationToken);

        return View("ApiHub", new OrganizationDocsViewModel
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            OrganizationSlug = organization.Slug,
            OrganizationRole = membership.Role.ToString(),
            OrganizationApiKey = organization.ApiKey,
            OrganizationBaseApiUrl = organization.BaseApiUrl,
            SubscriptionPlanName = plan?.Name ?? "Basic",
            HasApiAccess = hasApiAccess,
            IsProOrCustomPlan = isProOrCustom,
            HasPendingApiRequest = hasPendingRequest,
            ActiveTab = "api-hub",
            Docs = docs,
            Projects = projects,
            CanManageDocs = await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken),
            CanViewMembers = await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ViewMembers, cancellationToken),
            CanViewProjects = true,
            UserName = User.Identity?.Name ?? "Member",
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Organizations = memberships
                .Where(candidate => candidate.Organization.IsAvailableForUse())
                .Select(candidate => new OrganizationPortalMembershipViewModel(
                    candidate.Organization.Id,
                    candidate.Organization.Name,
                    candidate.Organization.Email,
                    candidate.Organization.Slug,
                    candidate.Role.ToString()))
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
            CanShowOrganizationAction = accountSubscription is not null
                || memberships.Any(m => m.Role == OrganizationMemberRole.Owner)
        });
    }

    [HttpPost("request-api-access")]
    public async Task<IActionResult> RequestApiAccess(
        [FromForm] string planName,
        [FromForm] string? contactEmail,
        [FromForm] string? useCase,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var normalizedPlan = planName is "Custom" ? "Custom" : "Pro";
        var email = string.IsNullOrWhiteSpace(contactEmail) ? User.FindFirstValue(ClaimTypes.Email) ?? "user@cloudhubs.com" : contactEmail.Trim();
        var notes = string.IsNullOrWhiteSpace(useCase) ? "Custom developer dashboard integration" : useCase.Trim();

        var requestId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"OrganizationApiRequests\" (\"Id\", \"OrganizationId\", \"UserId\", \"PlanName\", \"ContactEmail\", \"UseCase\", \"Status\", \"CreatedAt\") VALUES ({0}, {1}, {2}, {3}, {4}, {5}, 'Pending', {6})",
            requestId, organizationId, userId, normalizedPlan, email, notes, now);

        TempData["Success"] = $"Your request for {normalizedPlan} Plan API access has been submitted! Our team will review and unlock API access for your workspace.";
        return RedirectToAction(nameof(ApiHub));
    }

    [HttpPost("activate-pro-plan")]
    public async Task<IActionResult> ActivateProPlan(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var organization = await dbContext.Organizations
            .Include(o => o.Subscription)
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        if (organization is null) return NotFound();

        var proPlan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Name == "Pro", cancellationToken);

        if (proPlan is null)
        {
            proPlan = new SubscriptionPlan
            {
                Name = "Pro",
                Description = "Professional tier with full REST API access for custom dashboards.",
                Price = 49.00m,
                Currency = "USD",
                HasApiAccess = true,
                MaxProjects = 25,
                MaxMembers = 50,
                StorageLimitGb = 100,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.SubscriptionPlans.Add(proPlan);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (organization.Subscription is null)
        {
            organization.Subscription = new Subscription
            {
                OrganizationId = organization.Id,
                PlanId = proPlan.Id,
                PlanName = proPlan.Name,
                Status = SubscriptionStatus.Active,
                StartsAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
        }
        else
        {
            organization.Subscription.PlanId = proPlan.Id;
            organization.Subscription.PlanName = proPlan.Name;
            organization.Subscription.Status = SubscriptionStatus.Active;
            organization.Subscription.UpdatedAt = DateTime.UtcNow;
        }

        if (organization.AccountSubscriptionId is Guid accountSubId)
        {
            var accountSub = await dbContext.AccountSubscriptions.FirstOrDefaultAsync(a => a.Id == accountSubId, cancellationToken);
            if (accountSub is not null)
            {
                accountSub.PlanId = proPlan.Id;
                accountSub.Plan = proPlan;
                accountSub.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (string.IsNullOrWhiteSpace(organization.ApiKey))
        {
            var rawKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            organization.ApiKey = $"ch_live_cloudhubs_{rawKey}";
        }

        organization.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "⚡ Pro Plan activated! Organization REST API access is now fully unlocked for your custom dashboard.";
        return RedirectToAction(nameof(ApiHub));
    }

    [HttpPost("regenerate-org-api-key")]
    public async Task<IActionResult> RegenerateOrgApiKey(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var organization = await dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);
        if (organization is null) return NotFound();

        var rawKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        organization.ApiKey = $"ch_live_cloudhubs_{rawKey}";
        organization.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "Organization Master API Key has been regenerated successfully.";
        return RedirectToAction(nameof(ApiHub));
    }

    [HttpPost("import-platform-api-to-docs")]
    public async Task<IActionResult> ImportPlatformApiToDocs(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var organization = await dbContext.Organizations
            .Include(o => o.Subscription).ThenInclude(s => s!.Plan)
            .Include(o => o.AccountSubscription).ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);
        if (organization is null) return NotFound();

        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        if (plan is null || !plan.HasApiAccess)
        {
            TempData["Error"] = "Organization REST API access requires a Pro or Custom subscription plan.";
            return RedirectToAction(nameof(ApiHub));
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}/api/organizations/{organization.Id}";
        var title = $"{organization.Name} - Platform API (Custom Dashboard)";
        var slug = $"platform-api-{organization.Slug}";

        var existing = await docsService.GetDocsAsync(organizationId, userId, cancellationToken);
        var existingDoc = existing.FirstOrDefault(d => d.Slug == slug || d.Title.Contains("Platform API"));

        if (existingDoc is not null)
        {
            TempData["Success"] = "Platform API documentation profile is already present in Docs. You can configure member access below.";
            return RedirectToAction(nameof(Edit), new { docId = existingDoc.Id });
        }

        var content = @"# CloudHubs Organization Platform REST API

This API is provided directly by the **CloudHubs Platform** to build your own custom organization dashboard, automate projects, and manage hubs.

## Authentication
All requests must include your Master Organization API Key:
`X-Api-Key: " + (organization.ApiKey ?? "YOUR_ORGANIZATION_API_KEY") + @"`
or
`Authorization: Bearer " + (organization.ApiKey ?? "YOUR_ORGANIZATION_API_KEY") + @"`

## Available Endpoints

### 1. Dashboard Overview & Analytics
`GET /api/organizations/" + organization.Id + @"/overview`
Returns real-time dashboard metrics (projects count, members count, docs count, storage limits, plan info).

### 2. Projects & Hubs
- `GET /api/organizations/" + organization.Id + @"/projects` - List all projects
- `POST /api/organizations/" + organization.Id + @"/projects` - Provision a new project workspace
- `PUT /api/organizations/" + organization.Id + @"/projects/{projectId}` - Update project
- `DELETE /api/organizations/" + organization.Id + @"/projects/{projectId}` - Delete project

### 3. Team Members & Roles
- `GET /api/organizations/" + organization.Id + @"/members` - List organization members
- `POST /api/organizations/" + organization.Id + @"/members` - Invite member
- `PUT /api/organizations/" + organization.Id + @"/members/{memberId}` - Update role

### 4. Documentation & Resources
- `GET /api/organizations/" + organization.Id + @"/docs` - List documentation profiles
- `GET /api/organizations/" + organization.Id + @"/resources` - Project resources & servers
";

        var doc = await docsService.CreateDocAsync(
            organizationId,
            userId,
            title,
            slug,
            "Platform API",
            "CloudHubs platform REST API for custom dashboards and automation.",
            baseUrl,
            organization.ApiKey,
            null,
            null,
            $"CLOUDHUBS_ORG_ID={organization.Id}\nCLOUDHUBS_BASE_URL={baseUrl}\nCLOUDHUBS_API_KEY={organization.ApiKey}",
            null,
            content,
            null,
            isPublicToMembers: false,
            allowedUserIds: [userId],
            cancellationToken: cancellationToken);

        TempData["Success"] = "Platform API moved to Docs! You can now choose which members have permission to view it.";
        return RedirectToAction(nameof(Edit), new { docId = doc.Id });
    }

    [HttpGet("download-org-postman")]
    public async Task<IActionResult> DownloadOrgPostman(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("OrganizationId"), out var organizationId))
        {
            return RedirectToAction("Login", "Portal");
        }

        var organization = await dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);
        if (organization is null) return NotFound();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var postmanCollection = new
        {
            info = new
            {
                name = $"{organization.Name} - CloudHubs Organization Portal API",
                description = "Complete REST API collection for CloudHubs Organization Portal to build custom developer dashboards and integrations.",
                schema = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            variable = new object[]
            {
                new { key = "baseUrl", value = baseUrl, type = "string" },
                new { key = "organizationId", value = organization.Id.ToString(), type = "string" },
                new { key = "apiKey", value = organization.ApiKey ?? "YOUR_ORGANIZATION_API_KEY", type = "string" }
            },
            auth = new
            {
                type = "bearer",
                bearer = new object[]
                {
                    new { key = "token", value = "{{apiKey}}", type = "string" }
                }
            },
            item = new object[]
            {
                new
                {
                    name = "1. Organization Overview & Metrics",
                    request = new
                    {
                        method = "GET",
                        header = new object[]
                        {
                            new { key = "Accept", value = "application/json" },
                            new { key = "X-Api-Key", value = "{{apiKey}}" }
                        },
                        url = new
                        {
                            raw = "{{baseUrl}}/api/organizations/{{organizationId}}/overview",
                            host = new[] { "{{baseUrl}}" },
                            path = new[] { "api", "organizations", "{{organizationId}}", "overview" }
                        },
                        description = "Retrieve organization dashboard stats, plan entitlements, projects count, members count, and doc profile counts."
                    }
                },
                new
                {
                    name = "2. List All Projects",
                    request = new
                    {
                        method = "GET",
                        header = new object[]
                        {
                            new { key = "Accept", value = "application/json" },
                            new { key = "X-Api-Key", value = "{{apiKey}}" }
                        },
                        url = new
                        {
                            raw = "{{baseUrl}}/api/organizations/{{organizationId}}/projects",
                            host = new[] { "{{baseUrl}}" },
                            path = new[] { "api", "organizations", "{{organizationId}}", "projects" }
                        },
                        description = "List all active code projects and cloud hubs within this organization."
                    }
                },
                new
                {
                    name = "3. Create Project",
                    request = new
                    {
                        method = "POST",
                        header = new object[]
                        {
                            new { key = "Content-Type", value = "application/json" },
                            new { key = "X-Api-Key", value = "{{apiKey}}" }
                        },
                        body = new
                        {
                            mode = "raw",
                            raw = "{\n  \"name\": \"My New Cloud Hub\",\n  \"slug\": \"my-new-cloud-hub\",\n  \"description\": \"Project provisioned via Organization API\",\n  \"templateId\": \"starter-node\"\n}"
                        },
                        url = new
                        {
                            raw = "{{baseUrl}}/api/organizations/{{organizationId}}/projects",
                            host = new[] { "{{baseUrl}}" },
                            path = new[] { "api", "organizations", "{{organizationId}}", "projects" }
                        },
                        description = "Provision a new code project workspace."
                    }
                },
                new
                {
                    name = "4. List Organization Members",
                    request = new
                    {
                        method = "GET",
                        header = new object[]
                        {
                            new { key = "Accept", value = "application/json" },
                            new { key = "X-Api-Key", value = "{{apiKey}}" }
                        },
                        url = new
                        {
                            raw = "{{baseUrl}}/api/organizations/{{organizationId}}/members",
                            host = new[] { "{{baseUrl}}" },
                            path = new[] { "api", "organizations", "{{organizationId}}", "members" }
                        },
                        description = "Get list of all team members and their roles."
                    }
                },
                new
                {
                    name = "5. List Documentation Profiles",
                    request = new
                    {
                        method = "GET",
                        header = new object[]
                        {
                            new { key = "Accept", value = "application/json" },
                            new { key = "X-Api-Key", value = "{{apiKey}}" }
                        },
                        url = new
                        {
                            raw = "{{baseUrl}}/api/organizations/{{organizationId}}/docs",
                            host = new[] { "{{baseUrl}}" },
                            path = new[] { "api", "organizations", "{{organizationId}}", "docs" }
                        },
                        description = "Get list of all API documentation profiles, base URLs, and integration credentials."
                    }
                },
                new
                {
                    name = "6. List Project Resources",
                    request = new
                    {
                        method = "GET",
                        header = new object[]
                        {
                            new { key = "Accept", value = "application/json" },
                            new { key = "X-Api-Key", value = "{{apiKey}}" }
                        },
                        url = new
                        {
                            raw = "{{baseUrl}}/api/organizations/{{organizationId}}/resources",
                            host = new[] { "{{baseUrl}}" },
                            path = new[] { "api", "organizations", "{{organizationId}}", "resources" }
                        },
                        description = "Get list of all project resources and servers."
                    }
                }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(postmanCollection, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", $"{organization.Slug}-organization-api.json");
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
}
