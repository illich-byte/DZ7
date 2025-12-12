// Core/Services/AuthService.cs

using Core.Interfaces;
using Core.Models.Account; // Додали
using Domain.Entities.Idenity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Core.Services;

public class AuthService(IHttpContextAccessor httpContextAccessor,
    UserManager<UserEntity> userManager)
    : IAuthService
{
    // ... (існуючий GetUserIdAsync)
    public async Task<int> GetUserIdAsync()
    {
        // ... (існуючий код)
        var email = httpContextAccessor.HttpContext?.User.Claims.First().Value;
        if (string.IsNullOrEmpty(email))
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }
        var user = await userManager.FindByEmailAsync(email);
        return user!.Id;
    }

    // Реалізація нового методу RegisterAsync
    public async Task<string> RegisterAsync(RegisterModel model)
    {
        var user = new UserEntity
        {
            Email = model.Email,
            UserName = model.Email, // Використовуємо Email як UserName
            FirstName = model.FirstName,
            LastName = model.LastName,
            Image = "default.jpg" // Призначити зображення за замовчуванням
        };

        var result = await userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            // Збираємо помилки Identity в один рядок
            var errors = result.Errors.Select(e => e.Description);
            throw new Exception($"Registration failed: {string.Join(", ", errors)}");
        }

        // Присвоюємо роль "User" за замовчуванням
        await userManager.AddToRoleAsync(user, "User");

        return "Registration successful";
    }
}