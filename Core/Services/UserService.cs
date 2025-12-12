// Core/Services/UserService.cs
using Core.Interfaces;
using Core.Models.Account;
using Domain.Entities.Idenity;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims; // Потрібен для ClaimTypes

namespace Core.Services;

public class UserService(IAuthService authService,
    UserManager<UserEntity> userManager) : IUserService
{
    // Тут ми будемо отримувати дані користувача з БД та мапити їх на модель профілю
    public async Task<UserProfileModel> GetUserProfileAsync()
    {
        // Отримання ID через IAuthService, який ви вже створили
        var userId = await authService.GetUserIdAsync();

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            throw new Exception("User not found.");
        }

        // Тут можна використати AutoMapper (якщо він налаштований)
        // Або створити модель вручну:
        var model = new UserProfileModel
        {
            Id = user.Id,
            Email = user.Email!,
            UserName = user.UserName!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Image = user.Image
        };

        return model;
    }
}