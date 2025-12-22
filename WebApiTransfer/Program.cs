using Core.Interfaces;
using Core.Models.Account;
using Core.Services;
using Domain;
using Domain.Entities.Idenity;
using Domain.Entities.Location;
using Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Виправлення для Postgres
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// 1. БД
builder.Services.AddDbContext<AppDbTransferContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Identity
builder.Services.AddIdentity<UserEntity, RoleEntity>(options => {
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<AppDbTransferContext>()
.AddDefaultTokenProviders();

// 3. Auth & JWT
builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "SecretKey1234567890"))
    };
});

// 4. Реєстрація всіх сервісів
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<ICountryService, CountryService>();

builder.Services.AddControllers(); // Обов'язково
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(opt => opt.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// --- MIDDLEWARE ---
app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// ЦЕЙ РЯДОК ВИПРАВИТЬ ПОМИЛКУ 404
app.MapControllers(); 

// 5. Ініціалізація бази
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try {
        var context = services.GetRequiredService<AppDbTransferContext>();
        var userManager = services.GetRequiredService<UserManager<UserEntity>>();
        var roleManager = services.GetRequiredService<RoleManager<RoleEntity>>();

        await context.Database.MigrateAsync();

        if (!await roleManager.RoleExistsAsync("Admin")) await roleManager.CreateAsync(new RoleEntity { Name = "Admin" });
        if (!await roleManager.RoleExistsAsync("User")) await roleManager.CreateAsync(new RoleEntity { Name = "User" });

        var adminEmail = "admin@gmail.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null) {
            var admin = new UserEntity { UserName = adminEmail, Email = adminEmail, FirstName = "System", EmailConfirmed = true };
            await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        await SeedData.SeedAsync(context, userManager);
    } catch (Exception ex) { Console.WriteLine(ex.Message); }
}

app.Run();

// --- SEED DATA ---
public static class SeedData {
    public static async Task SeedAsync(AppDbTransferContext context, UserManager<UserEntity> userManager) {
        // Тут ваша логіка JSON...
    }
}
