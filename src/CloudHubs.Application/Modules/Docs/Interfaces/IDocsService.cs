using CloudHubs.Domain.Entities;

namespace CloudHubs.Application.Modules.Docs.Interfaces;

public interface IDocsService
{
    Task<IReadOnlyCollection<OrganizationDoc>> GetDocsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<OrganizationDoc?> GetDocByIdAsync(
        Guid organizationId,
        Guid docId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<OrganizationDoc> CreateDocAsync(
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
        CancellationToken cancellationToken = default);

    Task<OrganizationDoc> UpdateDocAsync(
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
        CancellationToken cancellationToken = default);

    Task<bool> DeleteDocAsync(
        Guid organizationId,
        Guid docId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

