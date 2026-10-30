using System.Security.Cryptography;
using System.Text;
using CloudHubs.Domain.Subscriptions;
using CloudHubs.Domain.Users;

namespace CloudHubs.Application.Organizations;

public class AccountSubscriptionService(IAccountSubscriptionRepository repository)
{
    public Task<IReadOnlyCollection<SubscriptionPlan>> GetActivePlansAsync(CancellationToken cancellationToken = default) =>
        repository.GetActivePlansAsync(cancellationToken);

    public async Task<SubscriptionPurchase> CreateManualPurchaseAsync(
        string name,
        string email,
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name?.Trim();
        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 200)
        {
            throw new ArgumentException("Enter your full name.", nameof(name));
        }
        if (string.IsNullOrWhiteSpace(normalizedEmail)
            || normalizedEmail.Length > 200
            || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(normalizedEmail))
        {
            throw new ArgumentException("Enter a valid email address.", nameof(email));
        }

        return await repository.CreatePurchaseAsync(normalizedName, normalizedEmail, planId, cancellationToken);
    }

    public Task<IReadOnlyCollection<SubscriptionPurchase>> GetPurchasesAsync(CancellationToken cancellationToken = default) =>
        repository.GetPurchasesAsync(cancellationToken);

    public Task<SubscriptionPurchase?> GetPurchaseAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetPurchaseAsync(id, cancellationToken);

    public Task<bool> IsCompletionTokenValidAsync(Guid id, string token, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(token)
            ? Task.FromResult(false)
            : repository.IsCompletionTokenValidAsync(id, HashToken(token), cancellationToken);

    public async Task<string?> ApprovePurchaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await IssueCompletionTokenAsync(id, isApproval: true, cancellationToken);
    }

    public async Task<string?> ReissueCompletionTokenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await IssueCompletionTokenAsync(id, isApproval: false, cancellationToken);
    }

    private async Task<string?> IssueCompletionTokenAsync(
        Guid id,
        bool isApproval,
        CancellationToken cancellationToken)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var tokenHash = HashToken(token);
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var saved = isApproval
            ? await repository.ApprovePurchaseAsync(id, tokenHash, expiresAt, cancellationToken)
            : await repository.ReissueCompletionTokenAsync(id, tokenHash, expiresAt, cancellationToken);
        return saved
            ? token
            : null;
    }

    public Task<bool> RejectPurchaseAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.RejectPurchaseAsync(id, cancellationToken);

    public Task<AccountSubscription?> CompletePurchaseAsync(
        Guid id,
        string token,
        string name,
        string? passwordHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult<AccountSubscription?>(null);
        }

        return repository.CompletePurchaseAsync(id, HashToken(token), name.Trim(), passwordHash, cancellationToken);
    }

    public Task<AccountSubscription?> GetAccountSubscriptionAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        repository.GetAccountSubscriptionAsync(ownerUserId, cancellationToken);

    public Task<AccountSubscription?> GetOrCreateAccountSubscriptionFromOwnedOrganizationAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        repository.GetOrCreateAccountSubscriptionFromOwnedOrganizationAsync(ownerUserId, cancellationToken);

    public Task<int> GetOrganizationCountAsync(
        Guid accountSubscriptionId,
        CancellationToken cancellationToken = default) =>
        repository.GetOrganizationCountAsync(accountSubscriptionId, cancellationToken);

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
