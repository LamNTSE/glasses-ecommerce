using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpticalStore.API.Middleware;
using OpticalStore.API.Swagger;
using OpticalStore.BLL;

// Chỉ nạp .env cho local development để tránh phụ thuộc file này trên cloud.
var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
{
	Env.TraversePath().Load();
}

var builder = WebApplication.CreateBuilder(args);

// --- Railway Configuration: Dynamic Port Binding ---
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port) && !builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

// --- Fail-Fast Security Checks ---
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
{
    throw new InvalidOperationException("CRITICAL ERROR: ConnectionStrings:DefaultConnection is missing from environment.");
}

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key");
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("CRITICAL ERROR: Jwt:Key is missing from environment. Must provide a base64 encoded string or a string of appropriate length.");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "OpticalStore API",
		Version = "v1"
	});

	options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
	{
		Name = "Authorization",
		Type = SecuritySchemeType.Http,
		Scheme = "bearer",
		BearerFormat = "JWT",
		In = ParameterLocation.Header
	});

	options.AddSecurityRequirement(new OpenApiSecurityRequirement
	{
		{
			new OpenApiSecurityScheme
			{
				Reference = new OpenApiReference
				{
					Type = ReferenceType.SecurityScheme,
					Id = "Bearer"
				}
			},
			Array.Empty<string>()
		}
	});

	options.DocumentFilter<SortTagsDocumentFilter>();
	options.OperationFilter<MultipartJsonRequestOperationFilter>();
});

// Configure CORS
builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowAll", builder =>
	{
		builder.AllowAnyOrigin()
			   .AllowAnyMethod()
			   .AllowAnyHeader();
	});
});

builder.Services.AddBllServices(builder.Configuration);

// jwtSection has already been extracted above
// var jwtSection = builder.Configuration.GetSection("Jwt");
// var jwtKey = jwtSection.GetValue<string>("Key") ?? string.Empty;

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

app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
