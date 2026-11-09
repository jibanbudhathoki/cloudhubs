using Microsoft.EntityFrameworkCore;
using CloudHubs.Application.Modules.Docs.Interfaces;
using CloudHubs.Domain.Entities;
using CloudHubs.Infrastructure.Data;

namespace CloudHubs.Infrastructure.Modules.Docs;

public sealed class OrganizationDocRepository(CloudHubsDbContext dbContext) : IOrganizationDocRepository
{
    public async Task<IReadOnlyCollection<OrganizationDoc>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await dbContext.OrganizationDocs
            .AsNoTracking()
            .Include(x => x.AllowedMembers)
                .ThenInclude(m => m.User)
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<OrganizationDoc?> GetByIdAsync(Guid organizationId, Guid docId, CancellationToken cancellationToken = default) =>
        await dbContext.OrganizationDocs
            .Include(x => x.AllowedMembers)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == docId, cancellationToken);

    public async Task<OrganizationDoc?> GetBySlugAsync(Guid organizationId, string slug, CancellationToken cancellationToken = default) =>
        await dbContext.OrganizationDocs
            .Include(x => x.AllowedMembers)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Slug == slug.ToLowerInvariant(), cancellationToken);

    public async Task<bool> SlugExistsAsync(Guid organizationId, string slug, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        await dbContext.OrganizationDocs
            .AnyAsync(x => x.OrganizationId == organizationId && x.Slug == slug.ToLowerInvariant() && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken);

    public async Task<OrganizationDoc> AddAsync(OrganizationDoc doc, CancellationToken cancellationToken = default)
    {
        doc.Slug = doc.Slug.Trim().ToLowerInvariant();
        doc.CreatedAt = DateTime.UtcNow;
        dbContext.OrganizationDocs.Add(doc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return doc;
    }

    public async Task<OrganizationDoc> UpdateAsync(OrganizationDoc doc, CancellationToken cancellationToken = default)
    {
        doc.Slug = doc.Slug.Trim().ToLowerInvariant();
        doc.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return doc;
    }

    public async Task SetAllowedMembersAsync(Guid docId, IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.OrganizationDocAccesses.Where(x => x.DocId == docId).ToListAsync(cancellationToken);
        dbContext.OrganizationDocAccesses.RemoveRange(existing);

        var list = userIds.Distinct().ToList();
        if (list.Count > 0)
        {
            var newAccesses = list.Select(uid => new OrganizationDocAccess
            {
                DocId = docId,
                UserId = uid,
                GrantedAt = DateTime.UtcNow
            });
            await dbContext.OrganizationDocAccesses.AddRangeAsync(newAccesses, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(OrganizationDoc doc, CancellationToken cancellationToken = default)
    {
        dbContext.OrganizationDocs.Remove(doc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

