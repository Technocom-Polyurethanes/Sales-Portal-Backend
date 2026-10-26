using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using sales_portal.infrastructure.Contexts;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING"), npgsqlOptions =>
        npgsqlOptions.MigrationsAssembly("sales_portal.infrastructure"))
    .UseSnakeCaseNamingConvention());

var app = builder.Build();

var dbContext = builder.Services.BuildServiceProvider().GetService<AppDbContext>();
await DbInitializer.SeedAsync(dbContext);

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
