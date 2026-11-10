using Microsoft.EntityFrameworkCore;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Infrastructure.Data;

public class CloudHubsDbContext : DbContext
{
    public CloudHubsDbContext(DbContextOptions<CloudHubsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<PlatformProject> PlatformProjects => Set<PlatformProject>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<OrganizationRole> OrganizationRoles => Set<OrganizationRole>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<AccountSubscription> AccountSubscriptions => Set<AccountSubscription>();
    public DbSet<SubscriptionPurchase> SubscriptionPurchases => Set<SubscriptionPurchase>();
    public DbSet<PlatformSettings> PlatformSettings => Set<PlatformSettings>();
    public DbSet<OrganizationDoc> OrganizationDocs => Set<OrganizationDoc>();
    public DbSet<OrganizationDocAccess> OrganizationDocAccesses => Set<OrganizationDocAccess>();
    public DbSet<ProjectIssue> ProjectIssues => Set<ProjectIssue>();
    public DbSet<ProjectIssueComment> ProjectIssueComments => Set<ProjectIssueComment>();
    public DbSet<ProjectIssueActivity> ProjectIssueActivities => Set<ProjectIssueActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CloudHubsDbContext).Assembly);
    }
}
