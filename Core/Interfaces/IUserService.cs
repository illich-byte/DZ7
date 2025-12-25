using Core.Models.Account;

namespace Core.Interfaces;

public interface IUserService
{
    Task<UserProfileModel> GetUserProfileAsync();
    // Додаємо метод пошуку
    Task<List<UserProfileModel>> SearchUsersAsync(string search);
}
