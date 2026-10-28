using System.Text;
using System.Text.Json.Serialization;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using sales_portal.api.Middleware;
using sales_portal.core.IService;
using sales_portal.core.Services;
using sales_portal.domain.Interfaces;
using sales_portal.infrastructure;
using sales_portal.infrastructure.Contexts;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IOfferCalculator, OfferCalculator>();
builder.Services.AddScoped<IOfferDocumentBuilder, OfferDocumentBuilder>();
builder.Services.AddScoped<IOfferService, OfferService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING"), npgsqlOptions =>
        npgsqlOptions.MigrationsAssembly("sales_portal.infrastructure"))
    .UseSnakeCaseNamingConvention());

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? "SuperSecretTechnocomSalesPortalKey_MustBeAtLeast32BytesLong!";
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "SalesPortalAPI";
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "SalesPortalClient";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// 1. Register CORS before builder.Build()
const string CorsPolicyName = "SalesPortalCors";

var configuredOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                // In development allow any localhost / 127.0.0.1 port (e.g. 8443, 5173, 3000).
                // Other environments only allow the explicitly configured origins.
                if (builder.Environment.IsDevelopment() &&
                    Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                    (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                     uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                return configuredOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors(CorsPolicyName);

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.SeedAsync(dbContext);
}

// Configure the HTTP request pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
