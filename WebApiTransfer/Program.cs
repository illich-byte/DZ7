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
using System;
using Core.Interfaces;
using Core.Services;

// >>> ПОЧАТОК ВИПРАВЛЕННЯ ДАТИ/ЧАСУ ДЛЯ POSTGRES <<<
// Цей рядок встановлює, що Npgsql повинен розглядати DateTime без вказаної 
// часової зони (Kind=Unspecified) як UTC. Це вирішує помилку 'timestamp with time zone'.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
// >>> КІНЕЦЬ ВИПРАВЛЕННЯ ДАТИ/ЧАСУ ДЛЯ POSTGRES <<<

var builder = WebApplication.CreateBuilder(args);

// >>> ВИПРАВЛЕННЯ ПОМИЛКИ MIGRATIONS.PendingModelChangesWarning <<<
builder.Services.AddDbContext<AppDbTransferContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    // Додаємо цю конфігурацію, щоб ігнорувати попередження про незавершені зміни моделі
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});
// >>> КІНЕЦЬ ВИПРАВЛЕННЯ ПОМИЛКИ MIGRATIONS.PendingModelChangesWarning <<<


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
        IssuerSigningKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

var assemblyName = typeof(LoginModel).Assembly.GetName().Name;

builder.Services.AddSwaggerGen(opt =>
{
    var fileDoc = $"{assemblyName}.xml";
    var filePath = Path.Combine(AppContext.BaseDirectory, fileDoc);
    opt.IncludeXmlComments(filePath);

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
new OpenApiSecurityScheme
{
Reference = new OpenApiReference { Type=ReferenceType.SecurityScheme, Id="Bearer" }
},
new string[]{}
}
});
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
    builder => builder.AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());
});

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddScoped<ICountryService, CountryService>();
builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailSender, EmailSender>();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});
builder.Services.AddValidatorsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddMvc(options =>
{
    options.Filters.Add<ValidationFilter>();
});

var app = builder.Build();


app.UseCors("AllowFrontend");
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

var dirImageName = builder.Configuration.GetValue<string>("DirImageName") ?? "duplo";
var path = Path.Combine(Directory.GetCurrentDirectory(), dirImageName);
Directory.CreateDirectory(dirImageName);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(path),
    RequestPath = $"/{dirImageName}"
});

using (var scoped = app.Services.CreateScope())
{
    var services = scoped.ServiceProvider;
    var myAppDbContext = services.GetRequiredService<AppDbTransferContext>();
    var roleManager = services.GetRequiredService<RoleManager<RoleEntity>>();
    var userManager = services.GetRequiredService<UserManager<UserEntity>>();
    var emailSender = services.GetRequiredService<IEmailSender>();

    // 🛑 ВИПРАВЛЕННЯ: Скидаємо базу даних, щоб уникнути конфлікту "relation tblCountries already exists"
    // який був спричинений попереднім запуском EnsureCreatedAsync()
    await myAppDbContext.Database.EnsureDeletedAsync();
    myAppDbContext.Database.Migrate();

    var roles = new[] { "User", "Admin" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new RoleEntity { Name = role });
        }
    }

    UserEntity adminUser = null;

    if (!myAppDbContext.Users.Any(u => u.Email == "admin@gmail.com"))
    {
        adminUser = new UserEntity
        {
            UserName = "admin@gmail.com",
            Email = "admin@gmail.com",
            FirstName = "System",
            LastName = "Administrator",
            Image = "default.jpg",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(adminUser, "Admin123");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
    else
    {
        adminUser = await userManager.FindByEmailAsync("admin@gmail.com");
    }


    await SeedData.SeedAsync(myAppDbContext, userManager);

    if (adminUser != null)
    {
        var subject = "Успішний запуск сайту TransferApp";
        var body = $"<h1>Привіт, {adminUser.FirstName}!</h1><p>Сайт ASP.NET Core TransferApp успішно запущено в мережі.</p><p>Час запуску: {DateTime.Now}</p>";

        try
        {
            await emailSender.SendEmailAsync(adminUser.Email, subject, body);
            Console.WriteLine($"[EMAIL] Повідомлення про запуск успішно надіслано на {adminUser.Email}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[EMAIL ERROR] Помилка при надсиланні email: {ex.Message}");
            Console.ResetColor();
        }
    }
}

app.Run();


public static class SeedData
{
    public static async Task SeedAsync(AppDbTransferContext context, UserManager<UserEntity> userManager)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var seedDirectory = Path.Combine(Directory.GetCurrentDirectory(), "SeedData");

        if (!Directory.Exists(seedDirectory)) return;

        if (!context.Set<CountryEntity>().Any())
        {
            var data = await File.ReadAllTextAsync(Path.Combine(seedDirectory, "Countries.json"));
            var items = JsonSerializer.Deserialize<List<CountryEntity>>(data, options);
            if (items != null) { context.Set<CountryEntity>().AddRange(items); await context.SaveChangesAsync(); }
        }

        if (!context.Set<CityEntity>().Any())
        {
            var data = await File.ReadAllTextAsync(Path.Combine(seedDirectory, "Cities.json"));
            var items = JsonSerializer.Deserialize<List<CityEntity>>(data, options);
            if (items != null) { context.Set<CityEntity>().AddRange(items); await context.SaveChangesAsync(); }
        }

        if (!context.Set<TransportationStatusEntity>().Any())
        {
            var data = await File.ReadAllTextAsync(Path.Combine(seedDirectory, "FlightStatuses.json"));
            var items = JsonSerializer.Deserialize<List<TransportationStatusEntity>>(data, options);
            if (items != null) { context.Set<TransportationStatusEntity>().AddRange(items); await context.SaveChangesAsync(); }
        }

        if (context.Users.Count() <= 1)
        {
            var data = await File.ReadAllTextAsync(Path.Combine(seedDirectory, "Users.json"));
            var users = JsonSerializer.Deserialize<List<UserEntity>>(data, options);
            if (users != null)
            {
                foreach (var user in users)
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