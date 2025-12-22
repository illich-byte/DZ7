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

// Виправлення для коректної роботи типів дати в PostgreSQL
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
.AddDefaultTokenProviders(); // КРИТИЧНО: для генерації токенів скидання пароля

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "SecretKey1234567890"))
    };
});

// 4. РЕЄСТРАЦІЯ СЕРВІСІВ (DI)
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<ICountryService, CountryService>();

builder.Services.AddControllers(); // Додає підтримку контролерів
builder.Services.AddEndpointsApiExplorer();

// SWAGGER З JWT
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
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
app.UseDefaultFiles(); 
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// ЦЕЙ РЯДОК ВИПРАВЛЯЄ ПОМИЛКУ 404 (з'єднує маршрути з контролерами)
app.MapControllers(); 

// 5. ІНІЦІАЛІЗАЦІЯ БД ТА SEED DATA
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbTransferContext>();
        var userManager = services.GetRequiredService<UserManager<UserEntity>>();
        var roleManager = services.GetRequiredService<RoleManager<RoleEntity>>();

        await context.Database.MigrateAsync();

        // Створення ролей
        if (!await roleManager.RoleExistsAsync("Admin")) await roleManager.CreateAsync(new RoleEntity { Name = "Admin" });
        if (!await roleManager.RoleExistsAsync("User")) await roleManager.CreateAsync(new RoleEntity { Name = "User" });

        // Створення адміна
        var adminEmail = "admin@gmail.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new UserEntity { UserName = adminEmail, Email = adminEmail, FirstName = "System", EmailConfirmed = true };
            await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        await SeedData.SeedAsync(context, userManager);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DB Error: {ex.Message}");
    }
}

app.Run();

// --- КЛАС SEED DATA (можна винести в окремий файл) ---
public static class SeedData
{
    public static async Task SeedAsync(AppDbTransferContext context, UserManager<UserEntity> userManager)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var root = Path.Combine(Directory.GetCurrentDirectory(), "SeedData");
        if (!Directory.Exists(root)) return;

        // Приклад для Країн
        var p = Path.Combine(root, "Countries.json");
        if (File.Exists(p) && !context.Set<CountryEntity>().Any())
        {
            var json = await File.ReadAllTextAsync(p);
            var items = JsonSerializer.Deserialize<List<CountryEntity>>(json, options);
            if (items != null) { context.AddRange(items); await context.SaveChangesAsync(); }
        }
    }
}
