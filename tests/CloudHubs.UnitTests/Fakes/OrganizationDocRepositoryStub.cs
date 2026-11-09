using CloudHubs.Application.Modules.Docs.Interfaces;
using CloudHubs.Domain.Entities;

namespace CloudHubs.UnitTests.Fakes;

internal sealed class OrganizationDocRepositoryStub : IOrganizationDocRepository
{
    public List<OrganizationDoc> Docs { get; } = [];

    public Task<IReadOnlyCollection<OrganizationDoc>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<OrganizationDoc>>(Docs.Where(d => d.OrganizationId == organizationId).ToList());

    public Task<OrganizationDoc?> GetByIdAsync(Guid organizationId, Guid docId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Docs.FirstOrDefault(d => d.OrganizationId == organizationId && d.Id == docId));

    public Task<OrganizationDoc?> GetBySlugAsync(Guid organizationId, string slug, CancellationToken cancellationToken = default) =>
        Task.FromResult(Docs.FirstOrDefault(d => d.OrganizationId == organizationId && string.Equals(d.Slug, slug, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> SlugExistsAsync(Guid organizationId, string slug, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Docs.Any(d => d.OrganizationId == organizationId && string.Equals(d.Slug, slug, StringComparison.OrdinalIgnoreCase) && (!excludeId.HasValue || d.Id != excludeId.Value)));

    public Task<OrganizationDoc> AddAsync(OrganizationDoc doc, CancellationToken cancellationToken = default)
    {
        Docs.Add(doc);
        return Task.FromResult(doc);
    }

    public Task<OrganizationDoc> UpdateAsync(OrganizationDoc doc, CancellationToken cancellationToken = default)
    {
        var idx = Docs.FindIndex(d => d.Id == doc.Id);
        if (idx >= 0) Docs[idx] = doc;
        else Docs.Add(doc);
        return Task.FromResult(doc);
    }

    public Task SetAllowedMembersAsync(Guid docId, IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var doc = Docs.FirstOrDefault(d => d.Id == docId);
        if (doc is not null)
        {
            doc.AllowedMembers.Clear();
            foreach (var uid in userIds)
            {
                doc.AllowedMembers.Add(new OrganizationDocAccess { DocId = docId, UserId = uid });
            }
        }
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(OrganizationDoc doc, CancellationToken cancellationToken = default)
    {
        Docs.RemoveAll(d => d.Id == doc.Id);
        return Task.FromResult(true);
    }
}

