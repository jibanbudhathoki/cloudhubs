using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CloudHubs.Application.Organizations;
using CloudHubs.Infrastructure.Persistence;

namespace CloudHubs.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CloudHubsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        return services;
    }
}
