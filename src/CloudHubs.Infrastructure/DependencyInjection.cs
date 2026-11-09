using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CloudHubs.Application.Modules.Docs.Interfaces;
using CloudHubs.Application.Modules.Organizations.Interfaces;
using CloudHubs.Application.Modules.Platform.Interfaces;
using CloudHubs.Application.Modules.Projects.Interfaces;
using CloudHubs.Application.Modules.Subscriptions.Interfaces;
using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Infrastructure.Data;
using CloudHubs.Infrastructure.Modules.Docs;
using CloudHubs.Infrastructure.Modules.Organizations;
using CloudHubs.Infrastructure.Modules.Platform;
using CloudHubs.Infrastructure.Modules.Projects;
using CloudHubs.Infrastructure.Modules.Subscriptions;
using CloudHubs.Infrastructure.Modules.Users;
using CloudHubs.Infrastructure.Services;

namespace CloudHubs.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CloudHubsDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IOrganizationDocRepository, OrganizationDocRepository>();
        services.AddScoped<IAccountSubscriptionRepository, AccountSubscriptionRepository>();
        services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
        services.AddScoped<IOrganizationRoleRepository, OrganizationRoleRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IPlatformSettingsRepository, PlatformSettingsRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPlatformProjectRepository, PlatformProjectRepository>();
        services.AddScoped<IOrganizationProjectRepository, OrganizationProjectRepository>();
        services.AddScoped<IProjectScaffolder, ProjectScaffolder>();
        services.AddScoped<IProjectFileService, ProjectFileService>();
        services.AddSingleton<IProjectProcessRunner, ProjectProcessRunner>();

        services.AddScoped<OrganizationCreatedNotificationSender>();
        services.AddScoped<IPasswordResetEmailSender, PasswordResetEmailSender>();

        return services;
    }
}
