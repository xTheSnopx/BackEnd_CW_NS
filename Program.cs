using CWNS.BackEnd.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Setup MySQL EF Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("CRITICAL: DefaultConnection is missing from environment variables.");
    // Fallback or throw
}

// Fix Render Linux Exit 139 by avoiding native AutoDetect network probing during startup
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 32))));

builder.Services.AddHostedService<CWNS.BackEnd.Services.ScraperBackgroundService>();

// Setup JWT Authentication
var jwtSecret = builder.Configuration["JwtSettings:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
{
    throw new Exception("JWT Secret not configured");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

var app = builder.Build();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Initialize DB and Default Data on Startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        context.Database.Migrate();

        if (!context.Seasons.Any())
        {
            context.Seasons.Add(new CWNS.BackEnd.Models.Season { Name = "Season 3", IsActive = true });
            context.SaveChanges();
        }

        if (!context.Users.Any())
        {
            string adminHash = BCrypt.Net.BCrypt.HashPassword("admin"); // Replace manual hash with dynamic
            context.Users.Add(new CWNS.BackEnd.Models.User 
            { 
                Username = "admin", 
                Password = adminHash,
                PlanExpiresAt = DateTime.UtcNow.AddYears(10)
            });
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during migration: {ex.Message}");
    }
}

app.Run();
