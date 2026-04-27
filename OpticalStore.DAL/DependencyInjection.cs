using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Repositories;
using OpticalStore.DAL.Repositories.Interfaces;

namespace OpticalStore.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddDalServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OpticalStoreDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IInvalidatedTokenRepository, InvalidatedTokenRepository>();

        return services;
    }
}
