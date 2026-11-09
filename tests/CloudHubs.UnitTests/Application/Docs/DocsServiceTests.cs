using CloudHubs.Application.Modules.Docs.Services;
using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Domain.Entities;
using CloudHubs.Domain.Enums;
using CloudHubs.UnitTests.Fakes;

namespace CloudHubs.UnitTests.Application.Docs;

public sealed class DocsServiceTests
{
    [Fact]
    public async Task CreateDocAsync_AdminMember_CreatesDocSuccessfully()
    {
        var organizationId = Guid.NewGuid();
        var admin = new User { Id = Guid.NewGuid(), Name = "Admin User", Email = "admin@example.com" };
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = admin.Id,
            User = admin,
            Role = OrganizationMemberRole.Admin
        };
        var roleRepo = new OrganizationRoleRepositoryStub { Membership = membership };
        var docRepo = new OrganizationDocRepositoryStub();
        var permissionService = new OrganizationPermissionService(roleRepo);
        var docsService = new DocsService(docRepo, permissionService, roleRepo);

        var doc = await docsService.CreateDocAsync(
            organizationId,
            admin.Id,
            "Orders API",
            "orders-api",
            "Microservice",
            "Orders processing endpoints",
            "https://api.orders.example.com",
            "ch_live_key_order",
            "ch_test_key_order",
            "sec_123",
            "PORT=4000\nDB=orders",
            "{\"info\":{\"name\":\"Orders\"}}",
            "# Orders API Docs");

        Assert.NotNull(doc);
        Assert.Equal("Orders API", doc.Title);
        Assert.Equal("orders-api", doc.Slug);
        Assert.Equal("https://api.orders.example.com", doc.BaseUrl);
        Assert.Single(docRepo.Docs);
    }

    [Fact]
    public async Task GetDocsAsync_OrganizationMember_ReturnsAllDocs()
    {
        var organizationId = Guid.NewGuid();
        var member = new User { Id = Guid.NewGuid(), Name = "Dev User", Email = "dev@example.com" };
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = member.Id,
            User = member,
            Role = OrganizationMemberRole.Developer
        };
        var roleRepo = new OrganizationRoleRepositoryStub { Membership = membership };
        var docRepo = new OrganizationDocRepositoryStub();
        docRepo.Docs.Add(new OrganizationDoc
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Title = "Auth Service",
            Slug = "auth-service"
        });
        var permissionService = new OrganizationPermissionService(roleRepo);
        var docsService = new DocsService(docRepo, permissionService, roleRepo);

        var result = await docsService.GetDocsAsync(organizationId, member.Id);

        Assert.Single(result);
        Assert.Equal("Auth Service", result.First().Title);
    }

    [Fact]
    public async Task UpdateDocAsync_AdminMember_UpdatesAllFields()
    {
        var organizationId = Guid.NewGuid();
        var admin = new User { Id = Guid.NewGuid(), Name = "Admin", Email = "admin@example.com" };
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = admin.Id,
            User = admin,
            Role = OrganizationMemberRole.Admin
        };
        var roleRepo = new OrganizationRoleRepositoryStub { Membership = membership };
        var docRepo = new OrganizationDocRepositoryStub();
        var existingDoc = new OrganizationDoc
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Title = "Old Title",
            Slug = "old-title"
        };
        docRepo.Docs.Add(existingDoc);
        var permissionService = new OrganizationPermissionService(roleRepo);
        var docsService = new DocsService(docRepo, permissionService, roleRepo);

        var updated = await docsService.UpdateDocAsync(
            organizationId,
            existingDoc.Id,
            admin.Id,
            "Updated Title",
            "updated-title",
            "Public API",
            "New description",
            "https://newapi.example.com",
            "new_key",
            "new_test_key",
            "new_secret",
            "KEY=VAL",
            "{}",
            "# New Docs");

        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("https://newapi.example.com", updated.BaseUrl);
        Assert.Equal("new_key", updated.ApiKey);
    }

    [Fact]
    public async Task DeleteDocAsync_AdminMember_RemovesDoc()
    {
        var organizationId = Guid.NewGuid();
        var admin = new User { Id = Guid.NewGuid(), Name = "Admin", Email = "admin@example.com" };
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = admin.Id,
            User = admin,
            Role = OrganizationMemberRole.Owner
        };
        var roleRepo = new OrganizationRoleRepositoryStub { Membership = membership };
        var docRepo = new OrganizationDocRepositoryStub();
        var doc = new OrganizationDoc
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Title = "To Delete",
            Slug = "to-delete"
        };
        docRepo.Docs.Add(doc);
        var permissionService = new OrganizationPermissionService(roleRepo);
        var docsService = new DocsService(docRepo, permissionService, roleRepo);

        var deleted = await docsService.DeleteDocAsync(organizationId, doc.Id, admin.Id);

        Assert.True(deleted);
        Assert.Empty(docRepo.Docs);
    }
}

