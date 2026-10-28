using MyDevPlatform.Application.Organizations;
using MyDevPlatform.Application.Projects;
using MyDevPlatform.Application.Users;
using MyDevPlatform.Contracts.Organizations;
using MyDevPlatform.Domain.Organizations;
using MyDevPlatform.Domain.Projects;
using MyDevPlatform.Domain.Users;

namespace MyDevPlatform.UnitTests;

public class SuperuserCrudServiceTests
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

    [Fact]
    public async Task OrganizationWithTenantProjectsCannotBeDeleted()
    {
        var repository = new OrganizationRepositoryStub { ProjectsExist = true };
        var service = new OrganizationService(repository, new PlatformSettingsRepositoryStub());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteOrganizationAsync(Guid.NewGuid()));

        Assert.False(repository.DeleteCalled);
    }

    [Fact]
    public async Task OrganizationCreationUsesPlatformDefaults()
    {
        var organizationRepository = new OrganizationRepositoryStub();
        var settingsRepository = new PlatformSettingsRepositoryStub
        {
            Settings = new PlatformSettings
            {
                DefaultOrganizationStatus = OrganizationStatus.Draft,
                DefaultTrialDays = 14,
                DefaultPlanName = "Launch",
                BillingCurrency = "EUR",
                MonthlyPrice = 19.99m
            }
        };
        var service = new OrganizationService(organizationRepository, settingsRepository);

        await service.CreateOrganizationAsync(
            new CreateOrganizationRequest("Acme", "owner@example.com", "acme", "", ""));

        Assert.Equal(OrganizationStatus.Draft, organizationRepository.AddedOrganization?.Status);
        Assert.Equal("Launch", organizationRepository.AddedOrganization?.Subscription?.PlanName);
        Assert.Equal("EUR", organizationRepository.AddedOrganization?.Subscription?.Currency);
        Assert.Equal(19.99m, organizationRepository.AddedOrganization?.Subscription?.MonthlyPrice);
        Assert.InRange(
            (organizationRepository.AddedOrganization!.Subscription!.ExpiresAt!.Value - DateTime.UtcNow).TotalDays,
            13.99,
            14.01);
    }

    [Fact]
    public async Task OrganizationCreationCanBeDisabledInPlatformSettings()
    {
        var settingsRepository = new PlatformSettingsRepositoryStub
        {
            Settings = new PlatformSettings { AllowOrganizationCreation = false }
        };
        var service = new OrganizationService(new OrganizationRepositoryStub(), settingsRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrganizationAsync(
                new CreateOrganizationRequest("Acme", "owner@example.com", "acme", "", "")));
    }

    [Fact]
    public async Task PlatformSettingsRejectUnsafeOrIncompleteValues()
    {
        var service = new PlatformSettingsService(new PlatformSettingsRepositoryStub());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(new PlatformSettings { SessionTimeoutMinutes = 5 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(new PlatformSettings { NotifyOnOrganizationCreated = true }));

        var saved = await service.UpdateAsync(new PlatformSettings { BillingCurrency = "eur" });
        Assert.Equal("EUR", saved.BillingCurrency);
    }

    [Fact]
    public async Task PlatformSettingsSectionUpdateDoesNotValidateOrOverwriteOtherSections()
    {
        var existingSettings = new PlatformSettings
        {
            PlatformName = "Old name",
            NotificationEmail = "invalid-address",
            NotifyOnOrganizationCreated = true,
            DefaultPlanName = "Existing plan"
        };
        var repository = new PlatformSettingsRepositoryStub { Settings = existingSettings };
        var service = new PlatformSettingsService(repository);

        existingSettings.PlatformName = "  CloudHubs  ";
        var saved = await service.UpdateSectionAsync(existingSettings, "profile");

        Assert.Equal("CloudHubs", saved.PlatformName);
        Assert.Equal("invalid-address", saved.NotificationEmail);
        Assert.True(saved.NotifyOnOrganizationCreated);
        Assert.Equal("Existing plan", saved.DefaultPlanName);
    }

    [Fact]
    public async Task PlatformSettingsSectionUpdateValidatesOnlyTheSelectedSection()
    {
        var service = new PlatformSettingsService(new PlatformSettingsRepositoryStub
        {
            Settings = new PlatformSettings { NotificationEmail = "not-an-email" }
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateSectionAsync(new PlatformSettings { SupportEmail = "not-an-email" }, "profile"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateSectionAsync(new PlatformSettings
            {
                NotifyOnOrganizationCreated = true,
                NotificationEmail = ""
            }, "notifications"));
    }

    [Fact]
    public async Task UserCreationNormalizesEmailAndValidatesRole()
    {
        var repository = new UserRepositoryStub();
        var service = new UserService(repository);

        await service.CreateUserAsync("Platform User", "USER@EXAMPLE.COM", "Developer", true);

        Assert.Equal("user@example.com", repository.AddedUser?.Email);
        Assert.Equal(UserRole.Developer, repository.AddedUser?.Role);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateUserAsync("Invalid Role", "invalid@example.com", "Superuser", true));
    }

    [Fact]
    public async Task SuperuserAccountsCannotBeManagedThroughRegularUserCrud()
    {
        var superuser = new User
        {
            Name = "Platform Superuser",
            Email = "admin@example.com",
            Role = UserRole.Superuser
        };
        var repository = new UserRepositoryStub
        {
            ExistingUser = superuser
        };
        var service = new UserService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateUserAsync("Another Superuser", "other@example.com", "Superuser", true));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateUserAsync(superuser.Id, "Platform Superuser", "admin@example.com", "Viewer", true));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteUserAsync(superuser.Id));
    }

    private sealed class OrganizationRepositoryStub : IOrganizationRepository
    {
        public bool ProjectsExist { get; init; }
        public bool DeleteCalled { get; private set; }
        public Organization? AddedOrganization { get; private set; }

        public Task<IReadOnlyCollection<OrganizationSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<OrganizationSummaryDto>>([]);
        public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Organization?>(null);
        public Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasProjectsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(ProjectsExist);
        public Task<Organization> AddAsync(Organization organization, CancellationToken cancellationToken = default)
        {
            AddedOrganization = organization;
            return Task.FromResult(organization);
        }
        public Task<Organization> UpdateAsync(Organization organization, CancellationToken cancellationToken = default) => Task.FromResult(organization);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeleteCalled = true;
            return Task.FromResult(true);
        }
    }

    private sealed class PlatformSettingsRepositoryStub : IPlatformSettingsRepository
    {
        public PlatformSettings Settings { get; init; } = new();

        public Task<PlatformSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Settings);

        public Task<PlatformSettings> UpdateAsync(PlatformSettings settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);
    }

    private sealed class UserRepositoryStub : IUserRepository
    {
        public User? AddedUser { get; private set; }
        public User? ExistingUser { get; init; }

        public Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<User>>([]);
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult((User?)new[] { AddedUser, ExistingUser }.FirstOrDefault(user => user?.Id == id));
        public Task<bool> EmailExistsAsync(string email, Guid? exceptId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
        {
            AddedUser = user;
            return Task.FromResult(user);
        }

        public Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.FromResult(user);
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class PlatformProjectRepositoryStub : IPlatformProjectRepository
    {
        public PlatformProject? AddedProject { get; private set; }

        public Task<IReadOnlyCollection<PlatformProject>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<PlatformProject>>([]);
        public Task<PlatformProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PlatformProject?>(null);
        public Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<PlatformProject> AddAsync(PlatformProject project, CancellationToken cancellationToken = default)
        {
            AddedProject = project;
            return Task.FromResult(project);
        }

        public Task<PlatformProject> UpdateAsync(PlatformProject project, CancellationToken cancellationToken = default) => Task.FromResult(project);
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}