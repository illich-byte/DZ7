using Core.Interfaces;
using Core.Models.Account;
using Domain.Entities.Idenity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace WebApiTransfer.Controllers;

// Налаштування маршруту: api/Account/[назва_методу]
[Route("api/[controller]/[action]")]
[ApiController]
public class AccountController(
    UserManager<UserEntity> userManager,
    IUserService userService,
    IJwtTokenService jwtTokenService,
    IAuthService authService,
    IEmailSender emailSender) : ControllerBase
{
    /// <summary>
    /// Вхід у систему
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        var user = await userManager.FindByEmailAsync(model.Email);

        if (user == null || !await userManager.CheckPasswordAsync(user, model.Password))
        {
            return Unauthorized(new { Message = "Невірний email або пароль." });
        }

        var token = await jwtTokenService.CreateAsync(user);

        return Ok(new { token });
    }

    /// <summary>
    /// Реєстрація нового користувача
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        try
        {
            var resultMessage = await authService.RegisterAsync(model);
            return Ok(new { Message = resultMessage });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Отримання профілю поточного користувача
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var model = await userService.GetUserProfileAsync();
        return Ok(model);
    }

    /// <summary>
    /// ПОШУК КОРИСТУВАЧІВ (Завдання: Пошук)
    /// </summary>
    /// <param name="query">Рядок для пошуку (Email, Ім'я або Прізвище)</param>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { Message = "Пошуковий запит не може бути порожнім." });
        }

        var results = await userService.SearchUsersAsync(query);
        return Ok(results);
    }

    /// <summary>
    /// Запит на відновлення пароля
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ForgotPassword([FromBody] string email)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user == null)
        {
            return Ok(new { Message = "Якщо такий Email існує, лист для відновлення надіслано." });
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var callbackUrl = $"{Request.Scheme}://{Request.Host}?email={user.Email}&token={Uri.EscapeDataString(token)}";

        var messageBody = $@"
            <div style='font-family: Arial; padding: 20px;'>
                <h2>Відновлення доступу</h2>
                <p>Натисніть на кнопку нижче для зміни пароля:</p>
                <a href='{callbackUrl}' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>Змінити пароль</a>
            </div>";

        await emailSender.SendEmailAsync(user.Email!, "Відновлення пароля", messageBody);

        return Ok(new { Message = "Лист надіслано успішно." });
    }

    /// <summary>
    /// Скидання пароля за токеном
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
    {
        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null) return BadRequest(new { Message = "Користувача не знайдено." });

        var result = await userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

        if (result.Succeeded)
        {
            return Ok(new { Message = "Пароль успішно оновлено!" });
        }

        return BadRequest(new { Message = "Помилка", Errors = result.Errors.Select(e => e.Description) });
    }
}