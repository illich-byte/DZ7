using Core.Interfaces;
using Core.Models.Account;
using Core.Services;
using Domain;
using Domain.Entities;
using Domain.Entities.Idenity;
using Domain.Entities.Location;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;
using WebApiTransfer.Filters;

// Виправлення для Postgres (UTC)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Налаштування БД
builder.Services.AddDbContext<AppDbTransferContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// Налаштування Identity
builder.Services.AddIdentity<UserEntity, RoleEntity>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<AppDbTransferContext>()
.AddDefaultTokenProviders();

// Налаштування JWT
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
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "DefaultSecretKey1234567890"))
    };
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

// Swagger
var assemblyName = typeof(LoginModel).Assembly.GetName().Name;
builder.Services.AddSwaggerGen(opt =>
{
    var fileDoc = $"{assemblyName}.xml";
    var filePath = Path.Combine(AppContext.BaseDirectory, fileDoc);
    if (File.Exists(filePath)) opt.IncludeXmlComments(filePath);

    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type=ReferenceType.SecurityScheme, Id="Bearer" } },
            new string[]{}
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Services
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddScoped<ICountryService, CountryService>();
builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailSender, EmailSender>();

builder.Services.AddValidatorsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddMvc(options => options.Filters.Add<ValidationFilter>());

var app = builder.Build();

// --- СЕРЕДОВИЩЕ ВИКОНАННЯ (MIDDLEWARE) ---
app.UseCors("AllowFrontend");

// ВАЖЛИВО: Ці два рядки запускають ваш index.html з wwwroot
app.UseDefaultFiles(); 
app.UseStaticFiles(); 

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Налаштування папки зображень
var dirImageName = builder.Configuration.GetValue<string>("DirImageName") ?? "duplo";
var imagePath = Path.Combine(Directory.GetCurrentDirectory(), dirImageName);
if (!Directory.Exists(imagePath)) Directory.CreateDirectory(imagePath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagePath),
    RequestPath = $"/{dirImageName}"
});

// Ініціалізація бази та SeedData
using (var scoped = app.Services.CreateScope())
{
    var services = scoped.ServiceProvider;
    try 
    {
        var context = services.GetRequiredService<AppDbTransferContext>();
        var roleManager = services.GetRequiredService<RoleManager<RoleEntity>>();
        var userManager = services.GetRequiredService<UserManager<UserEntity>>();
        var emailSender = services.GetRequiredService<IEmailSender>();

        // Очищення та міграція (Для розробки)
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        // Створення ролей
        var roles = new[] { "User", "Admin" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new RoleEntity { Name = role });
        }

        // Створення Адміна
        var adminEmail = "admin@gmail.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new UserEntity
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Administrator",
                Image = "default.jpg",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(adminUser, "Admin123!");
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }

        // SeedData
        await SeedData.SeedAsync(context, userManager);

        // Email повідомлення
        try 
        {
            await emailSender.SendEmailAsync(adminEmail, "Сайт запущено", $"Запуск успішний: {DateTime.Now}");
        }
        catch (Exception ex) { Console.WriteLine($"Email Error: {ex.Message}"); }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Critical Database Error: {ex.Message}");
    }
}

app.Run();

// Клас SeedData
public static class SeedData
{
    public static async Task SeedAsync(AppDbTransferContext context, UserManager<UserEntity> userManager)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var seedDirectory = Path.Combine(Directory.GetCurrentDirectory(), "SeedData");

        if (!Directory.Exists(seedDirectory)) return;

        // Countries
        var countriesPath = Path.Combine(seedDirectory, "Countries.json");
        if (File.Exists(countriesPath) && !context.Set<CountryEntity>().Any())
        {
            var data = await File.ReadAllTextAsync(countriesPath);
            var items = JsonSerializer.Deserialize<List<CountryEntity>>(data, options);
            if (items != null) { context.Set<CountryEntity>().AddRange(items); await context.SaveChangesAsync(); }
        }

        // Cities
        var citiesPath = Path.Combine(seedDirectory, "Cities.json");
        if (File.Exists(citiesPath) && !context.Set<CityEntity>().Any())
        {
            var data = await File.ReadAllTextAsync(citiesPath);
            var items = JsonSerializer.Deserialize<List<CityEntity>>(data, options);
            if (items != null) { context.Set<CityEntity>().AddRange(items); await context.SaveChangesAsync(); }
        }

        // Statuses
        var statusPath = Path.Combine(seedDirectory, "FlightStatuses.json");
        if (File.Exists(statusPath) && !context.Set<TransportationStatusEntity>().Any())
        {
            var data = await File.ReadAllTextAsync(statusPath);
            var items = JsonSerializer.Deserialize<List<TransportationStatusEntity>>(data, options);
            if (items != null) { context.Set<TransportationStatusEntity>().AddRange(items); await context.SaveChangesAsync(); }
        }

        // Users
        var usersPath = Path.Combine(seedDirectory, "Users.json");
        if (File.Exists(usersPath) && context.Users.Count() <= 1)
        {
            var data = await File.ReadAllTextAsync(usersPath);
            var users = JsonSerializer.Deserialize<List<UserEntity>>(data, options);
            if (users != null)
            {
                foreach (var user in users)
                {
                    if (await userManager.FindByEmailAsync(user.Email) == null)
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
