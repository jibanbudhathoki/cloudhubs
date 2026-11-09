using CloudHubs.Application.Modules.Organizations.DTOs;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Domain.Entities;

namespace CloudHubs.UnitTests.Fakes;

internal sealed class OrganizationRepositoryStub : IOrganizationRepository
{
    public bool ProjectsExist { get; init; }
    public bool DeleteCalled { get; private set; }
    public Organization? AddedOrganization { get; private set; }
    public Organization? ExistingOrganization { get; set; }

    public Task<IReadOnlyCollection<OrganizationSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<OrganizationSummaryDto>>([]);
    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(ExistingOrganization?.Id == id ? ExistingOrganization : null);
    public Task<Organization?> GetByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(ExistingOrganization?.ApiKey == apiKey ? ExistingOrganization : null);
    public Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> HasProjectsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(ProjectsExist);
    public Task<Organization> AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        AddedOrganization = organization;
        return Task.FromResult(organization);
    }
    public Task<Organization> UpdateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        ExistingOrganization ??= organization;
        return Task.FromResult(organization);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCalled = true;
        return Task.FromResult(true);
    }
}
