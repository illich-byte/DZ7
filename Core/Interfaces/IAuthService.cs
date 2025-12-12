using Core.Models.Account;

namespace Core.Interfaces;

public interface IAuthService
{
    Task<int> GetUserIdAsync();

    Task<string> RegisterAsync(RegisterModel model);
}
