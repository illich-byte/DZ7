// Core/Services/UserService.cs
using Core.Interfaces;
using Core.Models.Account;
using Domain.Entities.Idenity;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims; 

namespace Core.Services;

public class UserService(IAuthService authService,
    UserManager<UserEntity> userManager) : IUserService
{
    public async Task<UserProfileModel> GetUserProfileAsync()
    {
        var userId = await authService.GetUserIdAsync();

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            throw new Exception("User not found.");
        }

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