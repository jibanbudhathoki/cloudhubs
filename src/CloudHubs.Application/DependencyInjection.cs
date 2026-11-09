using Microsoft.Extensions.DependencyInjection;
using CloudHubs.Application.Modules.Docs.Interfaces;
using CloudHubs.Application.Modules.Docs.Services;
using CloudHubs.Application.Modules.MeroSpace.Interfaces;
using CloudHubs.Application.Modules.MeroSpace.Services;
using CloudHubs.Application.Modules.Organizations.Services;
using CloudHubs.Application.Modules.Platform.Services;
using CloudHubs.Application.Modules.Projects.Interfaces;
using CloudHubs.Application.Modules.Projects.Services;
using CloudHubs.Application.Modules.Subscriptions.Services;
using CloudHubs.Application.Modules.Users.Interfaces;
using CloudHubs.Application.Modules.Users.Services;

namespace CloudHubs.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IProjectTemplateService, ProjectTemplateService>();
        services.AddScoped<IMeroSpaceService, MeroSpaceService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<OrganizationRoleService>();
        services.AddScoped<OrganizationPermissionService>();
        services.AddScoped<OrganizationTenantService>();
        services.AddScoped<IDocsService, DocsService>();
        services.AddScoped<PlatformProjectService>();
        services.AddScoped<AccountSubscriptionService>();
        services.AddScoped<SubscriptionPlanService>();
        services.AddScoped<UserService>();
        services.AddScoped<PlatformSettingsService>();
        services.AddScoped<AuditLogService>();

        return services;
    }
}
