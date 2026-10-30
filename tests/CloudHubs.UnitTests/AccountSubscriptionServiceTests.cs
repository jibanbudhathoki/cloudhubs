using System.Security.Cryptography;
using System.Text;
using CloudHubs.Application.Organizations;
using CloudHubs.Domain.Subscriptions;
using CloudHubs.Domain.Users;

namespace CloudHubs.UnitTests;

public class AccountSubscriptionServiceTests
{
    [Fact]
    public async Task ManualPurchaseNormalizesCustomerDetailsBeforeSaving()
    {
        var repository = new AccountSubscriptionRepositoryStub();
        var service = new AccountSubscriptionService(repository);

        await service.CreateManualPurchaseAsync("  Cloud Owner  ", " OWNER@EXAMPLE.COM ", Guid.NewGuid());

        Assert.Equal("Cloud Owner", repository.CreatedName);
        Assert.Equal("owner@example.com", repository.CreatedEmail);
    }

    [Fact]
    public async Task ApprovingPurchaseReturnsOneTimeTokenAndStoresOnlyItsHash()
    {
        var repository = new AccountSubscriptionRepositoryStub();
        var service = new AccountSubscriptionService(repository);
        var purchaseId = Guid.NewGuid();

        var token = await service.ApprovePurchaseAsync(purchaseId);

        Assert.NotNull(token);
        Assert.NotEqual(token, repository.ApprovedTokenHash);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            repository.ApprovedTokenHash);
        Assert.True(await service.IsCompletionTokenValidAsync(purchaseId, token));
    }

    [Fact]
    public async Task CompletingPurchaseRequiresTokenAndForwardsNewAccountPasswordHash()
    {
        var repository = new AccountSubscriptionRepositoryStub();
        var service = new AccountSubscriptionService(repository);

        var noToken = await service.CompletePurchaseAsync(Guid.NewGuid(), "", "Owner", "password-hash");
        var completed = await service.CompletePurchaseAsync(Guid.NewGuid(), "token", "Owner", "password-hash");

        Assert.Null(noToken);
        Assert.NotNull(completed);
        Assert.Equal("password-hash", repository.CompletedUser?.PasswordHash);
    }

    [Fact]
    public async Task CompletingPurchaseForAnExistingAccountDoesNotRequireOrReplaceItsPassword()
    {
        var repository = new AccountSubscriptionRepositoryStub();
        var service = new AccountSubscriptionService(repository);

        var completed = await service.CompletePurchaseAsync(
            Guid.NewGuid(), "token", "Existing name", passwordHash: null);

        Assert.NotNull(completed);
        Assert.Equal("Existing name", repository.CompletedName);
        Assert.Null(repository.CompletedPasswordHash);
    }

    [Fact]
    public async Task AccountSubscriptionServiceReturnsOrganizationUsage()
    {
        var repository = new AccountSubscriptionRepositoryStub { OrganizationCount = 2 };
        var service = new AccountSubscriptionService(repository);

        var count = await service.GetOrganizationCountAsync(Guid.NewGuid());

        Assert.Equal(2, count);
    }

    private sealed class AccountSubscriptionRepositoryStub : IAccountSubscriptionRepository
    {
        public string? CreatedName { get; private set; }
        public string? CreatedEmail { get; private set; }
        public string? ApprovedTokenHash { get; private set; }
        public Guid? ApprovedPurchaseId { get; private set; }
        public User? CompletedUser { get; private set; }
        public string? CompletedName { get; private set; }
        public string? CompletedPasswordHash { get; private set; }
        public bool IsTokenValid { get; private set; }
        public int OrganizationCount { get; init; }

        public Task<IReadOnlyCollection<SubscriptionPlan>> GetActivePlansAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<SubscriptionPlan>>([]);

        public Task<SubscriptionPurchase> CreatePurchaseAsync(
            string name,
            string email,
            Guid planId,
            CancellationToken cancellationToken = default)
        {
            CreatedName = name;
            CreatedEmail = email;
            return Task.FromResult(new SubscriptionPurchase { Name = name, Email = email, PlanId = planId });
        }

        public Task<IReadOnlyCollection<SubscriptionPurchase>> GetPurchasesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<SubscriptionPurchase>>([]);

        public Task<SubscriptionPurchase?> GetPurchaseAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<SubscriptionPurchase?>(null);

        public Task<bool> IsCompletionTokenValidAsync(
            Guid id,
            string completionTokenHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(IsTokenValid || ApprovedPurchaseId == id && ApprovedTokenHash == completionTokenHash);

        public Task<bool> ApprovePurchaseAsync(
            Guid id,
            string completionTokenHash,
            DateTime completionTokenExpiresAt,
            CancellationToken cancellationToken = default)
        {
            ApprovedTokenHash = completionTokenHash;
            ApprovedPurchaseId = id;
            return Task.FromResult(true);
        }

        public Task<bool> ReissueCompletionTokenAsync(
            Guid id,
            string completionTokenHash,
            DateTime completionTokenExpiresAt,
            CancellationToken cancellationToken = default)
        {
            ApprovedTokenHash = completionTokenHash;
            ApprovedPurchaseId = id;
            return Task.FromResult(true);
        }

        public Task<bool> RejectPurchaseAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<AccountSubscription?> CompletePurchaseAsync(
            Guid id,
            string completionTokenHash,
            string name,
            string? passwordHash,
            CancellationToken cancellationToken = default)
        {
            CompletedName = name;
            CompletedPasswordHash = passwordHash;
            var user = new User
            {
                Name = name,
                PasswordHash = passwordHash,
                Email = "owner@example.com",
                Role = UserRole.OrganizationAdmin
            };
            CompletedUser = user;
            return Task.FromResult<AccountSubscription?>(new AccountSubscription
            {
                OwnerUser = user,
                OwnerUserId = user.Id
            });
        }

        public Task<AccountSubscription?> GetAccountSubscriptionAsync(
            Guid ownerUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AccountSubscription?>(null);

        public Task<AccountSubscription?> GetOrCreateAccountSubscriptionFromOwnedOrganizationAsync(
            Guid ownerUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AccountSubscription?>(null);

        public Task<int> GetOrganizationCountAsync(
            Guid accountSubscriptionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OrganizationCount);
    }
}
