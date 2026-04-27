using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpticalStore.BLL.Configuration;
using OpticalStore.BLL.Services;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL;

namespace OpticalStore.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBllServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<VnpayOptions>(configuration.GetSection(VnpayOptions.SectionName));
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.AddHttpClient<IChatbotService, ChatbotService>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(o.BaseUrl) ? "https://api.openai.com/v1" : o.BaseUrl.Trim().TrimEnd('/');
            client.BaseAddress = new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/");
            client.Timeout = TimeSpan.FromMinutes(1);
        });
        services.AddDalServices(configuration);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ILensService, LensService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductVariantService, ProductVariantService>();
        services.AddScoped<IPaymentWorkflowService, PaymentWorkflowService>();
        services.AddScoped<IFeedbackWorkflowService, FeedbackWorkflowService>();
        services.AddScoped<IOrdersWorkflowService, OrdersWorkflowService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddSingleton<INotificationStreamService, NotificationStreamService>();

        return services;
    }
}
