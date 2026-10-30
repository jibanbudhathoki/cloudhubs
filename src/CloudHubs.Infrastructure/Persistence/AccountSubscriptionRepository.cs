using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using CloudHubs.Application.Organizations;
using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Subscriptions;
using CloudHubs.Domain.Users;

namespace CloudHubs.Infrastructure.Persistence;

public sealed class AccountSubscriptionRepository(CloudHubsDbContext dbContext) : IAccountSubscriptionRepository
{
    public async Task<IReadOnlyCollection<SubscriptionPlan>> GetActivePlansAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.SubscriptionPlans
            .AsNoTracking()
            .Where(plan => plan.IsActive && plan.Price > 0)
            .OrderBy(plan => plan.Price)
            .ThenBy(plan => plan.Name)
            .ToListAsync(cancellationToken);

    public async Task<SubscriptionPurchase> CreatePurchaseAsync(
        string name,
        string email,
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.SubscriptionPlans.FirstOrDefaultAsync(
            candidate => candidate.Id == planId && candidate.IsActive && candidate.Price > 0,
            cancellationToken)
            ?? throw new ArgumentException("Choose an available paid subscription plan.", nameof(planId));

        var existingUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email != null && user.Email.ToLower() == email, cancellationToken);
        if (existingUser is { Role: UserRole.Superuser })
        {
            throw new InvalidOperationException("Superuser accounts cannot purchase organization subscriptions.");
        }
        if (existingUser is { IsActive: false })
        {
            throw new InvalidOperationException("This account is inactive. Contact support before purchasing a subscription.");
        }
        if (existingUser is not null
            && await dbContext.AccountSubscriptions.AnyAsync(
                subscription => subscription.OwnerUserId == existingUser.Id,
                cancellationToken))
        {
            throw new InvalidOperationException("This account already has an account subscription.");
        }
        if (await dbContext.SubscriptionPurchases.AnyAsync(
                purchase => purchase.Email.ToLower() == email
                    && (purchase.Status == SubscriptionPurchaseStatus.PendingPayment
                        || purchase.Status == SubscriptionPurchaseStatus.PaymentApproved),
                cancellationToken))
        {
            throw new InvalidOperationException("A pending subscription purchase already exists for this email.");
        }

        var purchase = new SubscriptionPurchase
        {
            Name = name,
            Email = email,
            PlanId = plan.Id,
            PlanName = plan.Name,
            Currency = plan.Currency,
            Price = plan.Price,
            BillingInterval = plan.BillingInterval
        };
        dbContext.SubscriptionPurchases.Add(purchase);
        await dbContext.SaveChangesAsync(cancellationToken);
        return purchase;
    }

    public async Task<IReadOnlyCollection<SubscriptionPurchase>> GetPurchasesAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.SubscriptionPurchases
            .AsNoTracking()
            .Include(purchase => purchase.Plan)
            .OrderByDescending(purchase => purchase.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<SubscriptionPurchase?> GetPurchaseAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.SubscriptionPurchases
            .AsNoTracking()
            .Include(purchase => purchase.Plan)
            .FirstOrDefaultAsync(purchase => purchase.Id == id, cancellationToken);

    public async Task<bool> IsCompletionTokenValidAsync(
        Guid id,
        string completionTokenHash,
        CancellationToken cancellationToken = default)
    {
        var purchase = await dbContext.SubscriptionPurchases
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id
                    && candidate.Status == SubscriptionPurchaseStatus.PaymentApproved
                    && candidate.CompletionTokenExpiresAt > DateTime.UtcNow,
                cancellationToken);
        return purchase?.CompletionTokenHash is { } storedHash
            && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(storedHash),
                Encoding.ASCII.GetBytes(completionTokenHash));
    }

    public async Task<bool> ApprovePurchaseAsync(
        Guid id,
        string completionTokenHash,
        DateTime completionTokenExpiresAt,
        CancellationToken cancellationToken = default)
    {
        var purchase = await dbContext.SubscriptionPurchases.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.Status == SubscriptionPurchaseStatus.PendingPayment,
            cancellationToken);
        if (purchase is null) return false;

        purchase.Status = SubscriptionPurchaseStatus.PaymentApproved;
        purchase.CompletionTokenHash = completionTokenHash;
        purchase.CompletionTokenExpiresAt = completionTokenExpiresAt;
        purchase.ApprovedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReissueCompletionTokenAsync(
        Guid id,
        string completionTokenHash,
        DateTime completionTokenExpiresAt,
        CancellationToken cancellationToken = default)
    {
        var purchase = await dbContext.SubscriptionPurchases.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.Status == SubscriptionPurchaseStatus.PaymentApproved,
            cancellationToken);
        if (purchase is null) return false;

        purchase.CompletionTokenHash = completionTokenHash;
        purchase.CompletionTokenExpiresAt = completionTokenExpiresAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RejectPurchaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var purchase = await dbContext.SubscriptionPurchases.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.Status == SubscriptionPurchaseStatus.PendingPayment,
            cancellationToken);
        if (purchase is null) return false;

        purchase.Status = SubscriptionPurchaseStatus.Rejected;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AccountSubscription?> CompletePurchaseAsync(
        Guid id,
        string completionTokenHash,
        string name,
        string? passwordHash,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var purchase = await dbContext.SubscriptionPurchases
            .Include(candidate => candidate.Plan)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (purchase?.Status != SubscriptionPurchaseStatus.PaymentApproved
            || purchase.CompletionTokenExpiresAt is not DateTime tokenExpiresAt
            || tokenExpiresAt <= DateTime.UtcNow
            || purchase.CompletionTokenHash is null
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(purchase.CompletionTokenHash),
                Encoding.ASCII.GetBytes(completionTokenHash)))
        {
            return null;
        }

        var existingUsers = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE lower(\"Email\") = {purchase.Email.ToLowerInvariant()} FOR UPDATE")
            .ToListAsync(cancellationToken);
        User user;
        if (existingUsers.Count > 0)
        {
            user = existingUsers[0];
            if (!user.IsActive || user.Role == UserRole.Superuser)
            {
                throw new InvalidOperationException("This account cannot own an organization subscription. Contact support.");
            }
            if (await dbContext.AccountSubscriptions.AnyAsync(
                    subscription => subscription.OwnerUserId == user.Id,
                    cancellationToken))
            {
                throw new InvalidOperationException("This account already has an account subscription.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new InvalidOperationException("A password is required to create a new account.");
            }

            user = new User
            {
                Name = name,
                Email = purchase.Email,
                LoginId = Guid.NewGuid().ToString(),
                PasswordHash = passwordHash,
                Role = UserRole.OrganizationAdmin,
                IsActive = true
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var startsAt = DateTime.UtcNow;
        var expiresAt = purchase.BillingInterval switch
        {
            SubscriptionBillingInterval.Monthly => startsAt.AddMonths(1),
            SubscriptionBillingInterval.Yearly => startsAt.AddYears(1),
            _ => throw new InvalidOperationException("The selected plan has an unsupported billing interval.")
        };
        var accountSubscription = new AccountSubscription
        {
            OwnerUserId = user.Id,
            OwnerUser = user,
            PlanId = purchase.PlanId,
            PlanName = purchase.PlanName,
            Currency = purchase.Currency,
            Price = purchase.Price,
            BillingInterval = purchase.BillingInterval,
            Status = SubscriptionStatus.Active,
            StartsAt = startsAt,
            ExpiresAt = expiresAt
        };
        dbContext.AccountSubscriptions.Add(accountSubscription);
        purchase.Status = SubscriptionPurchaseStatus.Completed;
        purchase.CompletionTokenHash = null;
        purchase.CompletionTokenExpiresAt = null;
        purchase.CompletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return accountSubscription;
    }

    public Task<AccountSubscription?> GetAccountSubscriptionAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        dbContext.AccountSubscriptions
            .AsNoTracking()
            .Include(subscription => subscription.Plan)
            .FirstOrDefaultAsync(subscription => subscription.OwnerUserId == ownerUserId, cancellationToken);

    public async Task<AccountSubscription?> GetOrCreateAccountSubscriptionFromOwnedOrganizationAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var users = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {ownerUserId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            return null;
        }

        var existingAccountSubscription = await dbContext.AccountSubscriptions
            .Include(subscription => subscription.Plan)
            .FirstOrDefaultAsync(subscription => subscription.OwnerUserId == ownerUserId, cancellationToken);
        if (existingAccountSubscription is not null)
        {
            if (existingAccountSubscription.IsAvailable())
            {
                var existingOrganizations = await dbContext.OrganizationMemberships
                    .Where(membership => membership.UserId == ownerUserId
                        && membership.Role == OrganizationMemberRole.Owner
                        && membership.Organization.AccountSubscriptionId == null)
                    .Include(membership => membership.Organization)
                    .ToListAsync(cancellationToken);
                foreach (var membership in existingOrganizations)
                {
                    membership.Organization.AccountSubscriptionId = existingAccountSubscription.Id;
                    membership.Organization.AccountSubscription = existingAccountSubscription;
                }

                if (existingOrganizations.Count > 0)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            return existingAccountSubscription;
        }

        var ownedOrganizations = await dbContext.OrganizationMemberships
            .Where(membership => membership.UserId == ownerUserId
                && membership.Role == OrganizationMemberRole.Owner
                && membership.Organization.AccountSubscriptionId == null)
            .Include(membership => membership.Organization)
                .ThenInclude(organization => organization.Subscription)
                    .ThenInclude(subscription => subscription!.Plan)
            .ToListAsync(cancellationToken);
        var eligibleOrganizations = ownedOrganizations
            .Where(membership => membership.Organization.IsAvailableForUse()
                && membership.Organization.Subscription?.PlanId is not null
                && membership.Organization.Subscription.Plan is not null)
            .ToArray();

        if (eligibleOrganizations.Length != 1)
        {
            return null;
        }

        var organization = eligibleOrganizations[0].Organization;
        var subscription = organization.Subscription!;
        var plan = subscription.Plan!;
        var accountSubscription = new AccountSubscription
        {
            OwnerUserId = ownerUserId,
            OwnerUser = users[0],
            PlanId = plan.Id,
            Plan = plan,
            PlanName = subscription.PlanName,
            Currency = subscription.Currency,
            Price = subscription.MonthlyPrice,
            BillingInterval = subscription.BillingInterval,
            Status = subscription.Status,
            StartsAt = subscription.StartsAt ?? subscription.CreatedAt,
            ExpiresAt = subscription.ExpiresAt
        };
        dbContext.AccountSubscriptions.Add(accountSubscription);
        organization.AccountSubscriptionId = accountSubscription.Id;
        organization.AccountSubscription = accountSubscription;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return accountSubscription;
    }

    public Task<int> GetOrganizationCountAsync(
        Guid accountSubscriptionId,
        CancellationToken cancellationToken = default) =>
        dbContext.Organizations.CountAsync(
            organization => organization.AccountSubscriptionId == accountSubscriptionId,
            cancellationToken);
}
