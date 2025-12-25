using Core.Interfaces;
using Core.Models.Account;
using Domain.Entities.Idenity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore; // Важливо для ToListAsync

namespace Core.Services;

public class UserService(IAuthService authService,
    UserManager<UserEntity> userManager) : IUserService
{
    public async Task<UserProfileModel> GetUserProfileAsync()
    {
        var userId = await authService.GetUserIdAsync();
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user == null) throw new Exception("User not found.");

        return MapToProfile(user);
    }

    // РЕАЛІЗАЦІЯ ПОШУКУ
    public async Task<List<UserProfileModel>> SearchUsersAsync(string search)
    {
        var searchLower = search.ToLower();

        // Шукаємо користувачів, де email, ім'я або прізвище містять пошуковий запит
        var users = await userManager.Users
            .Where(u => u.Email!.ToLower().Contains(searchLower) || 
                        u.FirstName!.ToLower().Contains(searchLower) || 
                        u.LastName!.ToLower().Contains(searchLower))
            .Select(u => new UserProfileModel // Мапимо відразу в модель профілю
            {
                Id = u.Id,
                Email = u.Email!,
                UserName = u.UserName!,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Image = u.Image
            })
            .ToListAsync();

        return users;
    }

    // Допоміжний метод для мапінгу (щоб не дублювати код)
    private UserProfileModel MapToProfile(UserEntity user) => new UserProfileModel
    {
        Id = user.Id,
        Email = user.Email!,
        UserName = user.UserName!,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Image = user.Image
    };
}
