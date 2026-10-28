using System.Text;
using System.Text.Json.Serialization;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using sales_portal.api.Middleware;
using sales_portal.core.Configuration;
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
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default")
        ?? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING"),
        npgsqlOptions => npgsqlOptions.MigrationsAssembly("sales_portal.infrastructure"))
    .UseSnakeCaseNamingConvention());

// JWT settings are bound through the Options pattern. The secret must come from
// configuration (Jwt:Secret) or the legacy JWT_SECRET environment variable; there is
// deliberately no fallback value so a missing secret fails startup instead of silently
// signing tokens with a well-known key.
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .PostConfigure(options =>
    {
        options.Secret = FirstNonEmpty(Environment.GetEnvironmentVariable("JWT_SECRET"), options.Secret);
        options.Issuer = FirstNonEmpty(Environment.GetEnvironmentVariable("JWT_ISSUER"), options.Issuer);
        options.Audience = FirstNonEmpty(Environment.GetEnvironmentVariable("JWT_AUDIENCE"), options.Audience);
    })
    .Validate(options => !string.IsNullOrWhiteSpace(options.Secret), "Jwt:Secret (or JWT_SECRET) must be configured.")
    .Validate(options => options.Secret.Length >= 32, "Jwt:Secret (or JWT_SECRET) must be at least 32 characters long.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer must be configured.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience must be configured.")
    .ValidateOnStart();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
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

    if (builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
    {
        await dbContext.Database.MigrateAsync();
    }

    // The demo admin account is a development convenience only.
    await DbInitializer.SeedAsync(dbContext, seedDemoUser: app.Environment.IsDevelopment());
}

// Configure the HTTP request pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    try
    {
        var databaseUp = await dbContext.Database.CanConnectAsync(cancellationToken);

        return databaseUp
            ? Results.Ok(new { status = "Healthy", database = "Up" })
            : Results.Json(
                new { status = "Unhealthy", database = "Down" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception)
    {
        return Results.Json(
            new { status = "Unhealthy", database = "Down" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.Run();

static string FirstNonEmpty(string? value, string? fallback) =>
    string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
