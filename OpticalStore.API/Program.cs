using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpticalStore.API.Middleware;
using OpticalStore.API.Swagger;
using OpticalStore.BLL;
using OpticalStore.BLL.Configuration;

// Nạp .env trước khi tạo Configuration: biến môi trường OpenAI__ApiKey, …
var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
{
	Env.TraversePath().Load();
}

// Cwd khi F5/CLI thường là thư mục project API — tải rõ ràng (TraversePath đôi khi không thấy .env)
var dotEnvCwd = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(dotEnvCwd))
{
	Env.Load(dotEnvCwd);
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
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
var vnpaySection = builder.Configuration.GetSection(VnpayOptions.SectionName);
var vnpayOptions = vnpaySection.Get<VnpayOptions>() ?? new VnpayOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.Key) || Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
{
	throw new InvalidOperationException("CRITICAL ERROR: Jwt:Key is missing or shorter than 32 bytes.");
}

if (string.IsNullOrWhiteSpace(jwtOptions.Issuer) || string.IsNullOrWhiteSpace(jwtOptions.Audience))
{
	throw new InvalidOperationException("CRITICAL ERROR: Jwt:Issuer and Jwt:Audience are required.");
}

if (string.IsNullOrWhiteSpace(vnpayOptions.TmnCode) || string.IsNullOrWhiteSpace(vnpayOptions.HashSecret) ||
	string.IsNullOrWhiteSpace(vnpayOptions.Url) || string.IsNullOrWhiteSpace(vnpayOptions.ReturnUrl) ||
	string.IsNullOrWhiteSpace(vnpayOptions.IpnUrl) || string.IsNullOrWhiteSpace(vnpayOptions.FrontendBaseUrl))
{
	throw new InvalidOperationException("CRITICAL ERROR: Vnpay configuration is missing. Check Vnpay:TmnCode, HashSecret, Url, ReturnUrl, IpnUrl and FrontendBaseUrl.");
}

if (!Uri.TryCreate(vnpayOptions.Url, UriKind.Absolute, out _) ||
	!Uri.TryCreate(vnpayOptions.ReturnUrl, UriKind.Absolute, out _) ||
	!Uri.TryCreate(vnpayOptions.IpnUrl, UriKind.Absolute, out _) ||
	!Uri.TryCreate(vnpayOptions.FrontendBaseUrl, UriKind.Absolute, out _))
{
	throw new InvalidOperationException("CRITICAL ERROR: Vnpay URLs must be absolute URLs.");
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

	// File upload: schema cho IFormFile trong multipart (không dùng [FromForm] trên tham số IFormFile — Swashbuckle 6.6+)
	options.MapType<IFormFile>(() => new OpenApiSchema
	{
		Type = "string",
		Format = "binary"
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

// Chuẩn hóa OpenAI: trim, bỏ ngoặc, fallback OPENAI__ApiKey / OPENAI_API_KEY (Railway, Docker thường dùng)
builder.Services.PostConfigure<OpenAiOptions>(o =>
{
	var k = o.ApiKey?.Trim();
	if (string.IsNullOrEmpty(k)) k = Environment.GetEnvironmentVariable("OPENAI__ApiKey")?.Trim();
	if (string.IsNullOrEmpty(k)) k = Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim();
	if (k is { Length: >= 2 } && k[0] == '"' && k[^1] == '"') k = k[1..^1].Trim();
	o.ApiKey = k;
});

builder.Services.PostConfigure<VnpayOptions>(o =>
{
	o.TmnCode = o.TmnCode?.Trim() ?? string.Empty;
	o.HashSecret = o.HashSecret?.Trim() ?? string.Empty;
});

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
			ValidIssuer = jwtOptions.Issuer,
			ValidAudience = jwtOptions.Audience,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
		};
	});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseGlobalExceptionHandling();

app.UseSwagger();
app.UseSwaggerUI();

// Tắt HttpsRedirection: profile https gây 307 5048→7273, cert dev làm fetch/axios thất bại từ FE (http) gọi API.

app.UseCors("AllowAll");

// File tĩnh (uploads/...) dùng không cần JWT — đặt trước auth
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
