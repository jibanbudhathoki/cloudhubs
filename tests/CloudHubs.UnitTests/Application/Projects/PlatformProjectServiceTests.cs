using CloudHubs.Application.Modules.Projects.Services;
using CloudHubs.Domain.Entities;
using CloudHubs.UnitTests.Fakes;

namespace CloudHubs.UnitTests.Application.Projects;

public class PlatformProjectServiceTests
{
    [Fact]
    public async Task PlatformProjectIsIndependentFromOrganizations()
    {
        var repository = new PlatformProjectRepositoryStub();
        var service = new PlatformProjectService(repository);

        await service.CreateProjectAsync("Platform Owner Project", "", "Internal tooling", "platform/owner-project", "dotnet", true);

        Assert.Equal("platform-owner-project", repository.AddedProject?.Slug);
        Assert.Null(typeof(PlatformProject).GetProperty(nameof(Project.OrganizationId)));
    }
}
