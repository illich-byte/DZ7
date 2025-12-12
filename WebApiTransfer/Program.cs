using Core.Interfaces;
using Core.Models.Account;
using Core.Services;
using Domain;
using Domain.Entities.Idenity;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using WebApiTransfer.Filters;

var builder = WebApplication.CreateBuilder(args);

// ====================================================================
// КОНФІГУРАЦІЯ БАЗИ ДАНИХ ТА IDENTITY
// ====================================================================

// 1. Додавання DB Context та підключення Npgsql
builder.Services.AddDbContext<AppDbTransferContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Налаштування Identity (UserManager, SignInManager, RoleManager)
builder.Services.AddIdentity<UserEntity, RoleEntity>(options =>
{
    // Налаштування вимог до пароля (відповідно до ваших конфігурацій)
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
})
    .AddEntityFrameworkStores<AppDbTransferContext>()
    .AddDefaultTokenProviders();

// 3. Налаштування JWT Bearer аутентифікації
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
        ValidateIssuer = false, // (Використовуєте False)
        ValidateAudience = false, // (Використовуєте False)
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});
//====================================================================

// Щоб отримати доступ до HttpContext в сервісах (потрібно для AuthService)
builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers();

var assemblyName = typeof(LoginModel).Assembly.GetName().Name;

// Налаштування Swagger (для тестування та документації API)
builder.Services.AddSwaggerGen(opt =>
{
    var fileDoc = $"{assemblyName}.xml";
    var filePath = Path.Combine(AppContext.BaseDirectory, fileDoc);
    opt.IncludeXmlComments(filePath);

    // Налаштування Bearer схеми для передачі JWT-токена
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
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[]{}
        }
    });

});

// ====================================================================
// ?? ЗМІНИ ТУТ: Додано явну політику CORS для використання з UseCors
// Це не є необхідним, оскільки у вас є app.UseCors нижче, але робить код чистішим.
// builder.Services.AddCors(); // Ваш оригінальний рядок
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});
// ====================================================================

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// ====================================================================
// РЕЄСТРАЦІЯ СЕРВІСІВ (КЛЮЧОВІ СЕРВІСИ ДЛЯ АУТЕНТИФІКАЦІЇ)
// ====================================================================
builder.Services.AddScoped<ICountryService, CountryService>();
builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services.AddScoped<IAuthService, AuthService>(); // Сервіс для логіки реєстрації/входу
builder.Services.AddScoped<IUserService, UserService>(); // Сервіс для отримання профілю
// ====================================================================


// ====================================================================
// ВАЛІДАЦІЯ ТА ФІЛЬТРИ (FluentValidation)
// ====================================================================
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});
// Автоматичний пошук та реєстрація валідаторів (включаючи RegisterModelValidator)
builder.Services.AddValidatorsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddMvc(options =>
{
    // Застосування ValidationFilter для автоматичної обробки валідації
    options.Filters.Add<ValidationFilter>();
});
// ====================================================================


var app = builder.Build();

// Configure the HTTP request pipeline.

// ====================================================================
// ВИКОРИСТАННЯ CORS - ВИКОРИСТОВУЄМО ЯВНЕ ІМ'Я ПОЛІТИКИ "AllowFrontend"
// Зверніть увагу, що ваша попередня конфігурація також працювала б, 
// але явне використання політики є кращою практикою.
app.UseCors("AllowFrontend");
// ====================================================================


//if(app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

// ====================================================================
// ВИКОРИСТАННЯ АУТЕНТИФІКАЦІЇ ТА АВТОРИЗАЦІЇ
// ====================================================================
app.UseAuthentication();
app.UseAuthorization();
// ====================================================================


app.MapControllers();

// Налаштування статичних файлів
var dirImageName = builder.Configuration
    .GetValue<string>("DirImageName") ?? "duplo";

var path = Path.Combine(Directory.GetCurrentDirectory(), dirImageName);
Directory.CreateDirectory(dirImageName);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(path),
    RequestPath = $"/{dirImageName}"
});

// ====================================================================
// ІНІЦІАЛІЗАЦІЯ БАЗИ ДАНИХ (СТВОРЕННЯ РОЛЕЙ ТА АДМІНА)
// ====================================================================
using (var scoped = app.Services.CreateScope())
{
    var myAppDbContext = scoped.ServiceProvider.GetRequiredService<AppDbTransferContext>();
    var roleManager = scoped.ServiceProvider.GetRequiredService<RoleManager<RoleEntity>>();
    myAppDbContext.Database.Migrate(); // Застосування міграцій

    // Створення ролей: "User" (потрібна для реєстрації) та "Admin"
    var roles = new[] { "User", "Admin" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new RoleEntity { Name = role });
        }
    }

    // Створення користувача-адміністратора
    if (!myAppDbContext.Users.Any())
    {
        var userManager = scoped.ServiceProvider
            .GetRequiredService<UserManager<UserEntity>>();
        var adminUser = new UserEntity
        {
            UserName = "admin@gmail.com",
            Email = "admin@gmail.com",
            FirstName = "System",
            LastName = "Administrator",
            Image = "default.jpg"
        };
        var result = await userManager.CreateAsync(adminUser, "Admin123");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}
// ====================================================================

app.Run();