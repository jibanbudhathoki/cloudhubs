using CloudHubs.Application.Modules.Docs.Interfaces;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Application.Modules.Projects.Interfaces;
using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Application.Modules.Organizations.Services;

public class OrganizationTenantService(
    IOrganizationRepository organizationRepository,
    IOrganizationRoleRepository roleRepository,
    IOrganizationProjectRepository projectRepository,
    IUserRepository userRepository,
    OrganizationPermissionService permissionService,
    IProjectScaffolder? projectScaffolder = null,
    IOrganizationDocRepository? docRepository = null)
{
    public async Task<Organization> RequireAccessAsync(
        Guid organizationId,
        Guid userId,
        OrganizationPermission permission,
        bool requireApiAccess,
        CancellationToken cancellationToken = default)
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
        var activePlan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan;
        if (activePlan is not { } plan)
        {
            throw new UnauthorizedAccessException("The organization has no subscription plan with configured entitlements.");
        }
        if (requireApiAccess && !plan.HasApiAccess)
        {
            throw new UnauthorizedAccessException("API access is not included in the organization’s subscription plan.");
        }
        if (!await permissionService.HasPermissionAsync(organizationId, userId, permission, cancellationToken))
        {
            throw new UnauthorizedAccessException("Your organization role does not allow this action.");
        }

        return organization;
    }

    public async Task<IReadOnlyCollection<Project>> GetProjectsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ViewProjects, requireApiAccess, cancellationToken);
        return await projectRepository.GetAllAsync(organizationId, cancellationToken);
    }

    public async Task<Project> CreateProjectAsync(
        Guid organizationId,
        Guid userId,
        string name,
        string slug,
        string? description,
        string? rootPath,
        string? templateId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        var organization = await RequireAccessAsync(
            organizationId, userId, OrganizationPermission.ManageProjects, requireApiAccess, cancellationToken);
        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan
            ?? throw new UnauthorizedAccessException("The organization has no subscription plan with configured entitlements.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Project name is required and must be 200 characters or fewer.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug) || slug.Trim().Length > 120 || !IsSlug(slug))
            throw new ArgumentException("Project slug must contain only letters, numbers, hyphens, or underscores and be 120 characters or fewer.", nameof(slug));
        if (description?.Length > 2000)
            throw new ArgumentException("Project description must be 2,000 characters or fewer.", nameof(description));
        if (rootPath?.Length > 500)
            throw new ArgumentException("Project root path must be 500 characters or fewer.", nameof(rootPath));
        if (templateId?.Length > 150)
            throw new ArgumentException("Template ID must be 150 characters or fewer.", nameof(templateId));

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (await projectRepository.SlugExistsAsync(organizationId, normalizedSlug, cancellationToken))
        {
            throw new InvalidOperationException("A project with this slug already exists in the organization.");
        }

        var normalizedTemplateId = string.IsNullOrWhiteSpace(templateId) ? "nodejs-mvc" : templateId.Trim().ToLowerInvariant();
        var normalizedRootPath = string.IsNullOrWhiteSpace(rootPath)
            ? $"projects/{organization.Slug}/{normalizedSlug}"
            : rootPath.Trim();

        var createdProject = await projectRepository.AddIfBelowLimitAsync(new Project
        {
            OrganizationId = organizationId,
            Name = name.Trim(),
            Slug = normalizedSlug,
            Description = description?.Trim() ?? string.Empty,
            RootPath = normalizedRootPath,
            TemplateId = normalizedTemplateId
        }, plan.MaxProjects, cancellationToken);

        if (projectScaffolder is not null)
        {
            await projectScaffolder.ScaffoldAsync(
                createdProject.RootPath,
                createdProject.TemplateId,
                createdProject.Name,
                organization.Slug,
                createdProject.Slug,
                cancellationToken);
        }

        return createdProject;
    }

    public async Task<bool> DeleteProjectAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ManageProjects, requireApiAccess, cancellationToken);
        var project = await projectRepository.GetByIdAsync(organizationId, projectId, cancellationToken);
        return project is not null && await projectRepository.DeleteAsync(project, cancellationToken);
    }

    public async Task<Project?> UpdateProjectAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        string name,
        string slug,
        string? description,
        string? rootPath,
        string? templateId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ManageProjects, requireApiAccess, cancellationToken);
        var project = await projectRepository.GetByIdAsync(organizationId, projectId, cancellationToken);
        if (project is null) return null;

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Project name is required and must be 200 characters or fewer.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug) || slug.Trim().Length > 120 || !IsSlug(slug))
            throw new ArgumentException("Project slug must contain only letters, numbers, hyphens, or underscores and be 120 characters or fewer.", nameof(slug));
        if (description?.Length > 2000)
            throw new ArgumentException("Project description must be 2,000 characters or fewer.", nameof(description));
        if (rootPath?.Length > 500)
            throw new ArgumentException("Project root path must be 500 characters or fewer.", nameof(rootPath));
        if (templateId?.Length > 150)
            throw new ArgumentException("Template ID must be 150 characters or fewer.", nameof(templateId));

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (!string.Equals(project.Slug, normalizedSlug, StringComparison.Ordinal)
            && await projectRepository.SlugExistsAsync(organizationId, normalizedSlug, cancellationToken))
        {
            throw new InvalidOperationException("A project with this slug already exists in the organization.");
        }

        project.Name = name.Trim();
        project.Slug = normalizedSlug;
        project.Description = description?.Trim() ?? string.Empty;
        project.RootPath = rootPath?.Trim() ?? string.Empty;
        project.TemplateId = string.IsNullOrWhiteSpace(templateId) ? "default" : templateId.Trim();
        return await projectRepository.UpdateAsync(project, cancellationToken);
    }

    public async Task<Project?> GetProjectAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = false)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ViewProjects, requireApiAccess, cancellationToken);
        var project = await projectRepository.GetByIdAsync(organizationId, projectId, cancellationToken);
        if (project is null) return null;

        // Sync or fallback from disk if fields are currently empty
        if (string.IsNullOrWhiteSpace(project.Documentation) && !string.IsNullOrWhiteSpace(project.RootPath))
        {
            var readme = Path.Combine(project.RootPath, "README.md");
            var docs = Path.Combine(project.RootPath, "DOCS.md");
            if (File.Exists(docs))
            {
                try { project.Documentation = await File.ReadAllTextAsync(docs, cancellationToken); } catch { }
            }
            else if (File.Exists(readme))
            {
                try { project.Documentation = await File.ReadAllTextAsync(readme, cancellationToken); } catch { }
            }
        }

        if (string.IsNullOrWhiteSpace(project.PostmanCollection) && !string.IsNullOrWhiteSpace(project.RootPath))
        {
            var postman = Path.Combine(project.RootPath, "postman_collection.json");
            if (File.Exists(postman))
            {
                try { project.PostmanCollection = await File.ReadAllTextAsync(postman, cancellationToken); } catch { }
            }
        }

        if (string.IsNullOrWhiteSpace(project.EnvironmentVariables) && !string.IsNullOrWhiteSpace(project.RootPath))
        {
            var envExample = Path.Combine(project.RootPath, ".env.example");
            var env = Path.Combine(project.RootPath, ".env");
            if (File.Exists(envExample))
            {
                try { project.EnvironmentVariables = await File.ReadAllTextAsync(envExample, cancellationToken); } catch { }
            }
            else if (File.Exists(env))
            {
                try { project.EnvironmentVariables = await File.ReadAllTextAsync(env, cancellationToken); } catch { }
            }
        }

        return project;
    }

    public async Task<Project> UpdateProjectResourcesAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        string? documentation,
        string? postmanCollection,
        string? environmentVariables,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = false)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ManageProjects, requireApiAccess, cancellationToken);
        var project = await projectRepository.GetByIdAsync(organizationId, projectId, cancellationToken)
            ?? throw new KeyNotFoundException("Project not found.");

        project.Documentation = documentation;
        project.PostmanCollection = postmanCollection;
        project.EnvironmentVariables = environmentVariables;
        project.UpdatedAt = DateTime.UtcNow;

        var updated = await projectRepository.UpdateAsync(project, cancellationToken);

        // Also sync to disk files if root path exists
        if (!string.IsNullOrWhiteSpace(project.RootPath) && Directory.Exists(project.RootPath))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(documentation))
                {
                    await File.WriteAllTextAsync(Path.Combine(project.RootPath, "DOCS.md"), documentation, cancellationToken);
                }
                if (!string.IsNullOrWhiteSpace(postmanCollection))
                {
                    await File.WriteAllTextAsync(Path.Combine(project.RootPath, "postman_collection.json"), postmanCollection, cancellationToken);
                }
                if (!string.IsNullOrWhiteSpace(environmentVariables))
                {
                    await File.WriteAllTextAsync(Path.Combine(project.RootPath, ".env.example"), environmentVariables, cancellationToken);
                }
            }
            catch
            {
                // File writing is best effort
            }
        }

        return updated;
    }

    public async Task<IReadOnlyCollection<OrganizationDoc>> GetOrganizationDocsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ViewProjects, false, cancellationToken);
        if (docRepository is null) return [];
        return await docRepository.GetAllAsync(organizationId, cancellationToken);
    }

    public async Task<OrganizationDoc?> GetOrganizationDocAsync(
        Guid organizationId,
        Guid docId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ViewProjects, false, cancellationToken);
        if (docRepository is null) return null;
        return await docRepository.GetByIdAsync(organizationId, docId, cancellationToken);
    }

    public async Task<OrganizationDoc> CreateOrganizationDocAsync(
        Guid organizationId,
        Guid userId,
        string title,
        string? slug,
        string? category,
        string? description,
        string? baseUrl,
        string? apiKey,
        string? secretKey,
        string? environmentVariables,
        string? postmanCollection,
        string? content,
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ManageProjects, false, cancellationToken);
        if (docRepository is null) throw new InvalidOperationException("Docs repository not configured.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var cleanSlug = string.IsNullOrWhiteSpace(slug)
            ? GenerateSlug(title)
            : slug.Trim().ToLowerInvariant();

        if (await docRepository.SlugExistsAsync(organizationId, cleanSlug, null, cancellationToken))
        {
            cleanSlug = $"{cleanSlug}-{Guid.NewGuid().ToString()[..4]}";
        }

        var doc = new OrganizationDoc
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ProjectId = projectId,
            Title = title.Trim(),
            Slug = cleanSlug,
            Category = string.IsNullOrWhiteSpace(category) ? "API" : category.Trim(),
            Description = description?.Trim() ?? string.Empty,
            BaseUrl = baseUrl?.Trim(),
            ApiKey = apiKey?.Trim(),
            SecretKey = secretKey?.Trim(),
            EnvironmentVariables = environmentVariables,
            PostmanCollection = postmanCollection,
            Content = content,
            IsPublicToMembers = true,
            CreatedAt = DateTime.UtcNow
        };

        return await docRepository.AddAsync(doc, cancellationToken);
    }

    public async Task<OrganizationDoc> UpdateOrganizationDocAsync(
        Guid organizationId,
        Guid docId,
        Guid userId,
        string title,
        string? slug,
        string? category,
        string? description,
        string? baseUrl,
        string? apiKey,
        string? secretKey,
        string? environmentVariables,
        string? postmanCollection,
        string? content,
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ManageProjects, false, cancellationToken);
        if (docRepository is null) throw new InvalidOperationException("Docs repository not configured.");

        var doc = await docRepository.GetByIdAsync(organizationId, docId, cancellationToken)
            ?? throw new KeyNotFoundException("Documentation profile was not found.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var cleanSlug = string.IsNullOrWhiteSpace(slug)
            ? doc.Slug
            : slug.Trim().ToLowerInvariant();

        if (cleanSlug != doc.Slug && await docRepository.SlugExistsAsync(organizationId, cleanSlug, docId, cancellationToken))
        {
            throw new InvalidOperationException("A documentation profile with this slug already exists.");
        }

        doc.Title = title.Trim();
        doc.Slug = cleanSlug;
        doc.Category = string.IsNullOrWhiteSpace(category) ? "API" : category.Trim();
        doc.Description = description?.Trim() ?? string.Empty;
        doc.BaseUrl = baseUrl?.Trim();
        doc.ApiKey = apiKey?.Trim();
        doc.SecretKey = secretKey?.Trim();
        doc.EnvironmentVariables = environmentVariables;
        doc.PostmanCollection = postmanCollection;
        doc.Content = content;
        doc.ProjectId = projectId;
        doc.UpdatedAt = DateTime.UtcNow;

        return await docRepository.UpdateAsync(doc, cancellationToken);
    }

    public async Task<bool> DeleteOrganizationDocAsync(
        Guid organizationId,
        Guid docId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ManageProjects, false, cancellationToken);
        if (docRepository is null) return false;

        var doc = await docRepository.GetByIdAsync(organizationId, docId, cancellationToken);
        if (doc is null) return false;

        return await docRepository.DeleteAsync(doc, cancellationToken);
    }

    private static string GenerateSlug(string text)
    {
        var clean = new string(text.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-').ToArray()).Trim();
        var slug = clean.Replace(' ', '-').ToLowerInvariant();
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return string.IsNullOrWhiteSpace(slug) ? "doc" : slug;
    }

    public async Task<IReadOnlyCollection<OrganizationMembership>> GetMembersAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        await RequireAccessAsync(organizationId, userId, OrganizationPermission.ViewMembers, requireApiAccess, cancellationToken);
        return await roleRepository.GetMembersAsync(organizationId, cancellationToken);
    }

    public async Task<OrganizationMembership> AddMemberAsync(
        Guid organizationId,
        Guid actorId,
        string email,
        OrganizationMemberRole role,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        var organization = await RequireAccessAsync(
            organizationId, actorId, OrganizationPermission.ManageMembers, requireApiAccess, cancellationToken);
        if (role is OrganizationMemberRole.Owner || !Enum.IsDefined(role))
            throw new ArgumentException("Choose a valid non-owner organization role.", nameof(role));

        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan
            ?? throw new UnauthorizedAccessException("The organization has no subscription plan with configured entitlements.");
        var user = await userRepository.GetByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.PasswordHash))
            throw new ArgumentException("No active user with sign-in credentials was found for this email.", nameof(email));
        if (user.Role == UserRole.Superuser)
            throw new ArgumentException("Superusers cannot be added as organization members.", nameof(email));
        return await roleRepository.AddMembershipIfBelowLimitAsync(new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            User = user,
            Role = role
        }, plan.MaxMembers, cancellationToken);
    }

    public async Task<OrganizationMembership> CreateMemberAsync(
        Guid organizationId,
        Guid actorId,
        User member,
        OrganizationMemberRole role,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        var organization = await RequireAccessAsync(
            organizationId, actorId, OrganizationPermission.ManageMembers, requireApiAccess, cancellationToken);
        if (role is OrganizationMemberRole.Owner || !Enum.IsDefined(role))
            throw new ArgumentException("Choose a valid non-owner organization role.", nameof(role));
        if (member.Role == UserRole.Superuser)
            throw new ArgumentException("Superusers cannot be added as organization members.", nameof(member));

        var plan = organization.AccountSubscription?.Plan ?? organization.Subscription?.Plan
            ?? throw new UnauthorizedAccessException("The organization has no subscription plan with configured entitlements.");
        return await roleRepository.AddNewMemberIfBelowLimitAsync(
            member, organizationId, role, plan.MaxMembers, cancellationToken);
    }

    public async Task<bool> RemoveMemberAsync(
        Guid organizationId,
        Guid actorId,
        Guid memberId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        await RequireAccessAsync(organizationId, actorId, OrganizationPermission.ManageMembers, requireApiAccess, cancellationToken);
        if (actorId == memberId)
            throw new InvalidOperationException("You cannot remove your own organization membership.");
        return await roleRepository.RemoveMembershipAsync(organizationId, memberId, cancellationToken);
    }

    public async Task<OrganizationMembership?> UpdateMemberRoleAsync(
        Guid organizationId,
        Guid actorId,
        Guid memberId,
        OrganizationMemberRole role,
        Guid? customRoleId,
        CancellationToken cancellationToken = default,
        bool requireApiAccess = true)
    {
        await RequireAccessAsync(organizationId, actorId, OrganizationPermission.ManageMembers, requireApiAccess, cancellationToken);
        if (role == OrganizationMemberRole.Owner || !Enum.IsDefined(role))
            throw new ArgumentException("Choose a valid non-owner organization role.", nameof(role));

        var membership = await roleRepository.GetMembershipAsync(organizationId, memberId, cancellationToken);
        if (membership is null) return null;
        if (membership.Role == OrganizationMemberRole.Owner)
            throw new InvalidOperationException("The organization owner’s role cannot be changed.");
        if (customRoleId.HasValue
            && await roleRepository.GetByIdAsync(organizationId, customRoleId.Value, cancellationToken) is null)
            throw new ArgumentException("Choose a custom role belonging to this organization.", nameof(customRoleId));

        membership.Role = role;
        membership.CustomRoleId = customRoleId;
        return await roleRepository.SaveMembershipAsync(membership, cancellationToken) ? membership : null;
    }

    public async Task<User> UpdateUserProfileAsync(
        Guid userId,
        string name,
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Please enter your name.", nameof(name));
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Please enter a valid email address.", nameof(email));

        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await userRepository.EmailExistsAsync(normalizedEmail, userId, cancellationToken))
        {
            throw new InvalidOperationException("That email address is already in use by another account.");
        }

        user.Name = name.Trim();
        user.Email = normalizedEmail;
        user.UpdatedAt = DateTime.UtcNow;

        return await userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task ChangeUserPasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        string confirmPassword,
        IPasswordHashingService hashingService,
        int minimumPasswordLength,
        bool requirePasswordDigit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(currentPassword))
            throw new ArgumentException("Please enter your current password.", nameof(currentPassword));
        if (string.IsNullOrEmpty(newPassword))
            throw new ArgumentException("Please enter a new password.", nameof(newPassword));
        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            throw new ArgumentException("The new passwords do not match.");

        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        if (!string.IsNullOrEmpty(user.PasswordHash) &&
            !hashingService.VerifyPassword(user, user.PasswordHash, currentPassword))
        {
            throw new ArgumentException("Your current password is incorrect.");
        }

        if (newPassword.Length < minimumPasswordLength)
        {
            throw new ArgumentException($"New password must be at least {minimumPasswordLength} characters long.");
        }

        if (requirePasswordDigit && !newPassword.Any(char.IsAsciiDigit))
        {
            throw new ArgumentException("New password must contain at least one digit.");
        }

        if (newPassword.Length > 128)
        {
            throw new ArgumentException("New password must be 128 characters or fewer.");
        }

        user.PasswordHash = hashingService.HashPassword(user, newPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        user.MustChangePasswordOnNextLogin = false;
        user.UpdatedAt = DateTime.UtcNow;

        await userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task<Organization> UpdateOrganizationProfileAsync(
        Guid organizationId,
        Guid userId,
        string name,
        string email,
        string? baseApiUrl,
        CancellationToken cancellationToken = default)
    {
        var organization = await RequireAccessAsync(
            organizationId,
            userId,
            OrganizationPermission.ManageMembers,
            false,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("A valid organization contact email is required.", nameof(email));

        organization.Name = name.Trim();
        organization.Email = email.Trim().ToLowerInvariant();
        organization.BaseApiUrl = string.IsNullOrWhiteSpace(baseApiUrl) ? null : baseApiUrl.Trim();
        organization.UpdatedAt = DateTime.UtcNow;

        return await organizationRepository.UpdateAsync(organization, cancellationToken);
    }

    public async Task<string> RegenerateApiKeyAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var organization = await RequireAccessAsync(
            organizationId,
            userId,
            OrganizationPermission.ManageMembers,
            false,
            cancellationToken);

        var rawKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var newApiKey = $"ch_live_cloudhubs_{rawKey}";

        organization.ApiKey = newApiKey;
        organization.UpdatedAt = DateTime.UtcNow;

        await organizationRepository.UpdateAsync(organization, cancellationToken);
        return newApiKey;
    }

    private static bool IsSlug(string slug) =>
        slug.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static string GetDefaultProjectsRoot()
    {
        var custom = Environment.GetEnvironmentVariable("CLOUDHUBS_PROJECTS_ROOT");
        if (!string.IsNullOrWhiteSpace(custom)) return custom;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cloudhubs", "projects");
    }
}
