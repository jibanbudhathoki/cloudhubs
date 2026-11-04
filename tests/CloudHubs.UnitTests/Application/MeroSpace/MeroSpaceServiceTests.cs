using CloudHubs.Application.Modules.MeroSpace.DTOs;
using CloudHubs.Application.Modules.MeroSpace.Services;
using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Application.Modules.Projects.Services;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.UnitTests.Fakes;

namespace CloudHubs.UnitTests.Application.MeroSpace;

public class MeroSpaceServiceTests
{
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly User _user = new() { Name = "Developer", Email = "dev@example.com", Role = UserRole.Developer };
    private readonly Organization _organization;
    private readonly OrganizationMembership _membership;
    private readonly OrganizationRoleRepositoryStub _roleRepo;
    private readonly OrganizationProjectRepositoryStub _projectRepo;
    private readonly ProjectFileServiceStub _fileService = new();
    private readonly ProjectTemplateService _templateService = new();

    public MeroSpaceServiceTests()
    {
        _organization = new Organization
        {
            Id = _organizationId,
            Slug = "acme",
            Subscription = new Subscription
            {
                Status = SubscriptionStatus.Active,
                Plan = new SubscriptionPlan { HasApiAccess = true, MaxProjects = 5 }
            }
        };

        _membership = new OrganizationMembership
        {
            OrganizationId = _organizationId,
            UserId = _user.Id,
            User = _user,
            Role = OrganizationMemberRole.Developer
        };

        _roleRepo = new OrganizationRoleRepositoryStub { Membership = _membership };
        _projectRepo = new OrganizationProjectRepositoryStub();
    }

    private MeroSpaceService CreateService()
    {
        return new MeroSpaceService(
            new OrganizationRepositoryStub { ExistingOrganization = _organization },
            _projectRepo,
            new UserRepositoryStub { Users = [_user] },
            new OrganizationPermissionService(_roleRepo),
            _templateService,
            _fileService);
    }

    [Fact]
    public async Task GetWorkspacesAsync_ReturnsWorkspaces_WithResolvedTemplateNames()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            Name = "Web Store",
            Slug = "web-store",
            TemplateId = "nodejs-mvc",
            RootPath = "projects/acme/web-store"
        };
        _projectRepo.Projects.Add(project);

        var service = CreateService();
        var workspaces = await service.GetWorkspacesAsync(_organizationId, _user.Id);

        Assert.Single(workspaces);
        var ws = workspaces.First();
        Assert.Equal("Web Store", ws.ProjectName);
        Assert.Equal("Node.js MVC", ws.TemplateName);
        Assert.Equal("nodejs-mvc", ws.TemplateId);
    }

    [Fact]
    public async Task GetWorkspaceFileTreeAsync_FetchesTreeViaFileService()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            Name = "Go Service",
            Slug = "go-service",
            TemplateId = "go",
            RootPath = "projects/acme/go-service"
        };
        _projectRepo.Projects.Add(project);

        var service = CreateService();
        var tree = await service.GetWorkspaceFileTreeAsync(_organizationId, project.Id, _user.Id);

        Assert.NotNull(tree);
        Assert.True(_fileService.TreeCalled);
        Assert.Equal("projects/acme/go-service", _fileService.LastTreeRootPath);
    }

    [Theory]
    [InlineData("nodejs-mvc", "3000")]
    [InlineData("nodejs-modules", "3000")]
    [InlineData("go", "8080")]
    [InlineData("java", "8080")]
    public async Task RunCommandAsync_TailorsOutputToTemplate(string templateId, string expectedPort)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            Name = "Sample App",
            Slug = "sample-app",
            TemplateId = templateId,
            RootPath = "projects/acme/sample-app"
        };
        _projectRepo.Projects.Add(project);

        var service = CreateService();
        var output = await service.RunCommandAsync(_organizationId, project.Id, _user.Id, "start");

        Assert.Contains(expectedPort, output);
        Assert.Contains("[MeroSpace Runner]", output);
    }

    [Fact]
    public async Task Comments_Add_Reply_ToggleResolve_And_Delete_WorkEndToEnd()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            Name = "Comment Test App",
            Slug = "comment-test-app",
            TemplateId = "nodejs-mvc",
            RootPath = "projects/acme/comment-test-app"
        };
        _projectRepo.Projects.Add(project);

        var service = CreateService();

        // 1. Initial comments should be empty
        var initialComments = await service.GetCommentsAsync(_organizationId, project.Id, _user.Id);
        Assert.Empty(initialComments);

        // 2. Add comment
        var created = await service.AddCommentAsync(
            _organizationId,
            project.Id,
            _user.Id,
            "Jiban",
            "jiban@example.com",
            new CreateMeroSpaceCommentRequest("src/server.js", 15, "app.listen(3000)", "Check port configuration"));

        Assert.NotNull(created);
        Assert.Equal("src/server.js", created.FilePath);
        Assert.Equal(15, created.LineNumber);
        Assert.Equal("Check port configuration", created.Content);
        Assert.False(created.IsResolved);

        // 3. Get comments
        var afterAdd = await service.GetCommentsAsync(_organizationId, project.Id, _user.Id);
        Assert.Single(afterAdd);

        // 4. Reply
        var replied = await service.ReplyCommentAsync(
            _organizationId,
            project.Id,
            _user.Id,
            created.Id,
            "Teammate",
            "team@example.com",
            "Looks good to me");

        Assert.NotNull(replied);
        Assert.Single(replied.Replies);
        Assert.Equal("Looks good to me", replied.Replies[0].Content);

        // 5. Toggle Resolve
        var toggled = await service.ToggleResolveCommentAsync(_organizationId, project.Id, _user.Id, created.Id);
        Assert.True(toggled);

        var resolvedList = await service.GetCommentsAsync(_organizationId, project.Id, _user.Id);
        Assert.True(resolvedList[0].IsResolved);

        // 6. Delete Comment
        var deleted = await service.DeleteCommentAsync(_organizationId, project.Id, _user.Id, created.Id);
        Assert.True(deleted);

        var emptyAgain = await service.GetCommentsAsync(_organizationId, project.Id, _user.Id);
        Assert.Empty(emptyAgain);
    }

    [Fact]
    public async Task ExportProjectZipAsync_ReturnsZipBytes_WhenUserHasAccess()
    {
        var service = CreateService();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            Name = "Download Project",
            Slug = "download-project",
            RootPath = "projects/acme/download-project",
            TemplateId = "nodejs-mvc"
        };
        _projectRepo.Projects.Add(project);

        var zipBytes = await service.ExportProjectZipAsync(_organizationId, project.Id, _user.Id);

        Assert.NotNull(zipBytes);
    }
}

