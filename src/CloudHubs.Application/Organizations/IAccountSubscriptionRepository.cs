using CloudHubs.Domain.Subscriptions;
using CloudHubs.Domain.Users;

namespace CloudHubs.Application.Organizations;

public interface IAccountSubscriptionRepository
{
    Task<IReadOnlyCollection<SubscriptionPlan>> GetActivePlansAsync(CancellationToken cancellationToken = default);
    Task<SubscriptionPurchase> CreatePurchaseAsync(
        string name,
        string email,
        Guid planId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<SubscriptionPurchase>> GetPurchasesAsync(CancellationToken cancellationToken = default);
    Task<SubscriptionPurchase?> GetPurchaseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> IsCompletionTokenValidAsync(
        Guid id,
        string completionTokenHash,
        CancellationToken cancellationToken = default);
    Task<bool> ApprovePurchaseAsync(
        Guid id,
        string completionTokenHash,
        DateTime completionTokenExpiresAt,
        CancellationToken cancellationToken = default);
    Task<bool> ReissueCompletionTokenAsync(
        Guid id,
        string completionTokenHash,
        DateTime completionTokenExpiresAt,
        CancellationToken cancellationToken = default);
    Task<bool> RejectPurchaseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AccountSubscription?> CompletePurchaseAsync(
        Guid id,
        string completionTokenHash,
        string name,
        string? passwordHash,
        CancellationToken cancellationToken = default);
    Task<AccountSubscription?> GetAccountSubscriptionAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);
    Task<AccountSubscription?> GetOrCreateAccountSubscriptionFromOwnedOrganizationAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);
    Task<int> GetOrganizationCountAsync(Guid accountSubscriptionId, CancellationToken cancellationToken = default);
}
