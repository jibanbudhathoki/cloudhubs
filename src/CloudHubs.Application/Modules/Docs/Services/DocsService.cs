using CloudHubs.Application.Modules.Docs.Interfaces;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Application.Modules.Docs.Services;

public sealed class DocsService(
    IOrganizationDocRepository docRepository,
    OrganizationPermissionService permissionService,
    IOrganizationRoleRepository roleRepository) : IDocsService
{
    public async Task<IReadOnlyCollection<OrganizationDoc>> GetDocsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAccessAsync(organizationId, userId, cancellationToken);
        var isManager = await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken);
        var docs = await docRepository.GetAllAsync(organizationId, cancellationToken);

        if (isManager)
        {
            return docs;
        }

        return docs
            .Where(doc => doc.IsPublicToMembers || doc.AllowedMembers.Any(m => m.UserId == userId))
            .ToList();
    }

    public async Task<OrganizationDoc?> GetDocByIdAsync(
        Guid organizationId,
        Guid docId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAccessAsync(organizationId, userId, cancellationToken);
        var isManager = await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken);
        var doc = await docRepository.GetByIdAsync(organizationId, docId, cancellationToken);
        if (doc is null) return null;

        if (!isManager && !doc.IsPublicToMembers && !doc.AllowedMembers.Any(m => m.UserId == userId))
        {
            return null;
        }

        return doc;
    }

    public async Task<OrganizationDoc> CreateDocAsync(
        Guid organizationId,
        Guid userId,
        string title,
        string? slug,
        string? category,
        string? description,
        string? baseUrl,
        string? apiKey,
        string? testApiKey,
        string? secretKey,
        string? environmentVariables,
        string? postmanCollection,
        string? content,
        Guid? projectId = null,
        bool isPublicToMembers = true,
        IEnumerable<Guid>? allowedUserIds = null,
        CancellationToken cancellationToken = default)
    {
        await RequireManageAccessAsync(organizationId, userId, cancellationToken);

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
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim(),
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim(),
            TestApiKey = string.IsNullOrWhiteSpace(testApiKey) ? null : testApiKey.Trim(),
            SecretKey = string.IsNullOrWhiteSpace(secretKey) ? null : secretKey.Trim(),
            EnvironmentVariables = environmentVariables,
            PostmanCollection = postmanCollection,
            Content = content,
            IsPublicToMembers = isPublicToMembers,
            CreatedAt = DateTime.UtcNow
        };

        var created = await docRepository.AddAsync(doc, cancellationToken);

        if (!isPublicToMembers && allowedUserIds is not null)
        {
            await docRepository.SetAllowedMembersAsync(created.Id, allowedUserIds, cancellationToken);
        }

        return created;
    }

    public async Task<OrganizationDoc> UpdateDocAsync(
        Guid organizationId,
        Guid docId,
        Guid userId,
        string title,
        string? slug,
        string? category,
        string? description,
        string? baseUrl,
        string? apiKey,
        string? testApiKey,
        string? secretKey,
        string? environmentVariables,
        string? postmanCollection,
        string? content,
        Guid? projectId = null,
        bool isPublicToMembers = true,
        IEnumerable<Guid>? allowedUserIds = null,
        CancellationToken cancellationToken = default)
    {
        await RequireManageAccessAsync(organizationId, userId, cancellationToken);

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
        doc.BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim();
        doc.ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        doc.TestApiKey = string.IsNullOrWhiteSpace(testApiKey) ? null : testApiKey.Trim();
        doc.SecretKey = string.IsNullOrWhiteSpace(secretKey) ? null : secretKey.Trim();
        doc.EnvironmentVariables = environmentVariables;
        doc.PostmanCollection = postmanCollection;
        doc.Content = content;
        doc.ProjectId = projectId;
        doc.IsPublicToMembers = isPublicToMembers;
        doc.UpdatedAt = DateTime.UtcNow;

        var updated = await docRepository.UpdateAsync(doc, cancellationToken);

        await docRepository.SetAllowedMembersAsync(
            doc.Id,
            isPublicToMembers ? [] : (allowedUserIds ?? []),
            cancellationToken);

        return updated;
    }

    public async Task<bool> DeleteDocAsync(
        Guid organizationId,
        Guid docId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await RequireManageAccessAsync(organizationId, userId, cancellationToken);

        var doc = await docRepository.GetByIdAsync(organizationId, docId, cancellationToken);
        if (doc is null) return false;

        return await docRepository.DeleteAsync(doc, cancellationToken);
    }

    private async Task RequireViewAccessAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var hasAccess = await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ViewProjects, cancellationToken);
        if (!hasAccess)
        {
            var memberships = await roleRepository.GetMembershipsForUserAsync(userId, cancellationToken);
            var isMember = memberships.Any(m => m.OrganizationId == organizationId && m.Organization.IsAvailableForUse());
            if (!isMember)
            {
                throw new UnauthorizedAccessException("You do not have permission to view documentation in this workspace.");
            }
        }
    }

    private async Task RequireManageAccessAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var hasAccess = await permissionService.HasPermissionAsync(organizationId, userId, OrganizationPermission.ManageProjects, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have permission to manage documentation in this workspace.");
        }
    }

    private static string GenerateSlug(string text)
    {
        var clean = new string(text.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-').ToArray()).Trim();
        var slug = clean.Replace(' ', '-').ToLowerInvariant();
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return string.IsNullOrWhiteSpace(slug) ? "doc" : slug;
    }
}

