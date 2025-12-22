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

// Виправлення для роботи з часом у Postgres
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 1. НАЛАШТУВАННЯ БАЗИ ДАНИХ
builder.Services.AddDbContext<AppDbTransferContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// 2. IDENTITY (Користувачі та Ролі)
builder.Services.AddIdentity<UserEntity, RoleEntity>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbTransferContext>()
.AddDefaultTokenProviders(); // НЕОБХІДНО для генерації токенів скидання пароля

// 3. JWT АУТЕНТИФІКАЦІЯ
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "DefaultSecretKey1234567890"))
    };
});

// 4. РЕЄСТРАЦІЯ СЕРВІСІВ (Dependency Injection)
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<ICountryService, CountryService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// SWAGGER З ПІДТРИМКОЮ JWT
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Введіть JWT токен у форматі: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, new string[] { } }
    });
});

builder.Services.AddCors(opt => opt.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// --- MIDDLEWARE ---
app.UseCors("AllowAll");
app.UseDefaultFiles(); // Дозволяє запуск index.html
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 5. ІНІЦІАЛІЗАЦІЯ БАЗИ (MIGRATIONS & SEED DATA)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbTransferContext>();
        var userManager = services.GetRequiredService<UserManager<UserEntity>>();
        var roleManager = services.GetRequiredService<RoleManager<RoleEntity>>();

        // Застосування міграцій (Database.EnsureDeleted() ПРИБРАНО для збереження даних)
        await context.Database.MigrateAsync();

        // Початкове створення ролей
        if (!await roleManager.RoleExistsAsync("Admin")) await roleManager.CreateAsync(new RoleEntity { Name = "Admin" });
        if (!await roleManager.RoleExistsAsync("User")) await roleManager.CreateAsync(new RoleEntity { Name = "User" });

        // Створення адміна
        var adminEmail = "admin@gmail.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new UserEntity
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Admin",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // Завантаження даних з JSON
        await SeedData.SeedAsync(context, userManager);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Помилка при ініціалізації БД: {ex.Message}");
    }
}

app.Run();

// --- КЛАС SEED DATA ---
public static class SeedData
{
    public static async Task SeedAsync(AppDbTransferContext context, UserManager<UserEntity> userManager)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var rootPath = Path.Combine(Directory.GetCurrentDirectory(), "SeedData");

        if (!Directory.Exists(rootPath)) return;

        // Приклад наповнення Країн
        var countriesPath = Path.Combine(rootPath, "Countries.json");
        if (File.Exists(countriesPath) && !context.Set<CountryEntity>().Any())
        {
            var json = await File.ReadAllTextAsync(countriesPath);
            var countries = JsonSerializer.Deserialize<List<CountryEntity>>(json, options);
            if (countries != null) { context.AddRange(countries); await context.SaveChangesAsync(); }
        }

        // Приклад наповнення Користувачів
        var usersPath = Path.Combine(rootPath, "Users.json");
        if (File.Exists(usersPath) && context.Users.Count() <= 1)
        {
            var json = await File.ReadAllTextAsync(usersPath);
            var users = JsonSerializer.Deserialize<List<UserEntity>>(json, options);
            if (users != null)
            {
                foreach (var user in users)
                {
                    if (await userManager.FindByEmailAsync(user.Email!) == null)
                    {
                        user.UserName = user.Email;
                        user.EmailConfirmed = true;
                        await userManager.CreateAsync(user, "User123!");
                        await userManager.AddToRoleAsync(user, "User");
                    }
                }
            }
        }
    }
}
