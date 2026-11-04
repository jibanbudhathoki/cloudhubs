using CloudHubs.Application.Modules.Projects.Interfaces;

namespace CloudHubs.UnitTests.Fakes;

public class ProjectScaffolderStub : IProjectScaffolder
{
    public bool ScaffoldCalled { get; private set; }
    public string? LastRootPath { get; private set; }
    public string? LastTemplateId { get; private set; }
    public string? LastProjectName { get; private set; }
    public string? LastOrgSlug { get; private set; }
    public string? LastProjectSlug { get; private set; }

    public Task ScaffoldAsync(
        string rootPath,
        string templateId,
        string projectName,
        string organizationSlug,
        string projectSlug,
        CancellationToken cancellationToken = default)
    {
        ScaffoldCalled = true;
        LastRootPath = rootPath;
        LastTemplateId = templateId;
        LastProjectName = projectName;
        LastOrgSlug = organizationSlug;
        LastProjectSlug = projectSlug;
        return Task.CompletedTask;
    }
}

