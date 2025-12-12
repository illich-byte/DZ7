// WebApiTransfer/Controllers/AccountController.cs

using Core.Interfaces;
using Core.Models.Account;
using Domain.Entities.Idenity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace WebApiTransfer.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class AccountController(
    UserManager<UserEntity> userManager,
    IUserService userService,
    IJwtTokenService jwtTokenService,
    IAuthService authService) : ControllerBase // Додано IAuthService
{
    /// <summary>
    /// Аутентифікація користувача та видача JWT-токена.
    /// </summary>
    /// <param name="model">Модель для входу, що містить Email та Password.</param>
    /// <returns>JWT-токен у разі успіху.</returns>
    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        var user = await userManager.FindByEmailAsync(model.Email);

        // Перевірка існування користувача та коректності пароля
        if (user == null || !await userManager.CheckPasswordAsync(user, model.Password))
        {
            return Unauthorized("Invalid email or password.");
        }

        // Створення JWT-токена для аутентифікованого користувача
        var token = await jwtTokenService.CreateAsync(user);

        // Повернення токена
        return Ok(new { token });
    }

    /// <summary>
    /// Реєстрація нового користувача в системі.
    /// </summary>
    /// <param name="model">Модель для реєстрації, що містить Email, Password та інше.</param>
    /// <returns>Статус успішної реєстрації.</returns>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        try
        {
            // Виклик логіки реєстрації, реалізованої в AuthService
            var resultMessage = await authService.RegisterAsync(model);

            return Ok(new { Message = resultMessage });
        }
        catch (Exception ex)
        {
            // Обробка помилок (наприклад, Email вже використовується, або помилки Identity)
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Отримання інформації про профіль поточного аутентифікованого користувача.
    /// </summary>
    /// <returns>Дані профілю користувача.</returns>
    [HttpGet]
    [Authorize] // Доступно лише для користувачів із дійсним JWT-токеном
    public async Task<IActionResult> GetProfile()
    {
        // Виклик сервісу для отримання даних профілю
        var model = await userService.GetUserProfileAsync();

        // Повернення даних профілю
        return Ok(model);
    }
}