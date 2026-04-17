using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpticalStore.BLL;
using OpticalStore.API.Middleware;
using OpticalStore.DAL.DBContext;

LoadEnvIfPresent();

var builder = WebApplication.CreateBuilder(args);

ConfigureCloudPortBinding(builder);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OpticalStore API",
        Version = "v1"
    });

    const string jwtSchemeName = "Bearer";
    options.AddSecurityDefinition(jwtSchemeName, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Nhap token theo dinh dang: Bearer {your JWT token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = jwtSchemeName
                }
            },
            Array.Empty<string>()
        }
    });

    // Group endpoints by first route segment so Swagger UI shows collapsible tag sections.
    options.TagActionsBy(apiDesc =>
    {
        var explicitTags = apiDesc.ActionDescriptor.EndpointMetadata
            .OfType<ITagsMetadata>()
            .SelectMany(x => x.Tags)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (explicitTags.Length > 0)
        {
            return explicitTags;
        }

        var relativePath = apiDesc.RelativePath ?? string.Empty;
        var firstSegment = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();

        return firstSegment switch
        {
            "auth" => new[] { "1. Authentication" },
            "products" => new[] { "2. Products" },
            "product-variants" => new[] { "3. Product Variants" },
            "users" => new[] { "4. Users" },
            _ => new[] { apiDesc.ActionDescriptor.RouteValues["controller"] ?? "Other" }
        };
    });

    options.DocInclusionPredicate((docName, apiDesc) => docName == "v1");
});

// CORS: allow all origins including different ports
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Register BLL + DAL services through layered DI extensions
builder.Services.AddBllServices(builder.Configuration);

// JWT Authentication (API responsibility)
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key") ?? string.Empty;
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");

ValidateRequiredConfiguration(defaultConnection, jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection.GetValue<string>("Issuer"),
            ValidAudience = jwtSection.GetValue<string>("Audience"),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

ApplyMigrationsOnStartup(app, builder.Configuration);

app.UseGlobalExceptionHandling();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "OpticalStore API v1");
        options.DocumentTitle = "OpticalStore API Docs";
    });
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowAll");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

static void ConfigureCloudPortBinding(WebApplicationBuilder builder)
{
    var port = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrWhiteSpace(port) && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    {
        builder.WebHost.UseUrls($"http://*:{port}");
    }
}

static void LoadEnvIfPresent()
{
    var candidates = new[] { ".env", "../.env" };
    foreach (var candidate in candidates)
    {
        if (File.Exists(candidate))
        {
            Env.Load(candidate);
            return;
        }
    }
}

static void ValidateRequiredConfiguration(string? defaultConnection, string jwtKey)
{
    if (string.IsNullOrWhiteSpace(defaultConnection) || defaultConnection.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Invalid 'ConnectionStrings:DefaultConnection'. Set a real value via environment variables.");
    }

    if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Invalid 'Jwt:Key'. Set a real value via environment variables.");
    }

    if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    {
        throw new InvalidOperationException("'Jwt:Key' must be at least 32 bytes for HS256.");
    }
}

static void ApplyMigrationsOnStartup(WebApplication app, IConfiguration configuration)
{
    var autoMigrate = configuration.GetValue("Database:AutoMigrate", true);
    if (!autoMigrate)
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StartupMigration");
    var dbContext = scope.ServiceProvider.GetRequiredService<OpticalStoreDbContext>();

    logger.LogInformation("Applying EF Core migrations on startup...");
    dbContext.Database.Migrate();
    logger.LogInformation("EF Core migrations applied successfully.");
}
