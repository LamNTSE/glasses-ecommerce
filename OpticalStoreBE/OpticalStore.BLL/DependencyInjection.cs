using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpticalStore.BLL.Services;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL;

namespace OpticalStore.BLL
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddBllServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
            services.AddDalServices(configuration);
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }
    }
}
