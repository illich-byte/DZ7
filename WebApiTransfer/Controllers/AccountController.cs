using Core.Interfaces;
using Core.Models.Account;
using Domain.Entities.Idenity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace WebApiTransfer.Controllers;

// Маршрут налаштований так, щоб звертатися до методів за схемою: api/Account/НазваМетоду
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
    /// Отримання профілю (вимагає авторизації)
    /// </summary>
    [HttpGet]
    [Authorize] 
    public async Task<IActionResult> GetProfile()
    {
        var model = await userService.GetUserProfileAsync();
        return Ok(model);
    }

    /// <summary>
    /// Запит на відновлення пароля (надсилає лист з токеном)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ForgotPassword([FromBody] string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        
        // З міркувань безпеки завжди повертаємо OK
        if (user == null) 
        {
            return Ok(new { Message = "Якщо такий Email існує, лист для відновлення надіслано." });
        }

        // Генеруємо токен
        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        // Формуємо посилання на ваш фронтенд (Koyeb)
        var callbackUrl = $"{Request.Scheme}://{Request.Host}?email={user.Email}&token={Uri.EscapeDataString(token)}";

        var messageBody = $@"
            <div style='font-family: Arial; padding: 20px; border: 1px solid #eee;'>
                <h2>Відновлення доступу</h2>
                <p>Ви отримали цей лист, бо зробили запит на зміну пароля.</p>
                <p>Натисніть на кнопку нижче, щоб встановити новий пароль:</p>
                <a href='{callbackUrl}' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>Змінити пароль</a>
                <p style='color: #666; font-size: 0.8em; margin-top: 20px;'>Якщо ви цього не робили, просто видаліть цей лист.</p>
            </div>";

        await emailSender.SendEmailAsync(user.Email!, "Відновлення пароля", messageBody);

        return Ok(new { Message = "Лист для відновлення надіслано успішно." });
    }

    /// <summary>
    /// Встановлення нового пароля за допомогою токена
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
    {
        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return BadRequest(new { Message = "Користувача не знайдено." });
        }

        // Перевірка токена та зміна пароля
        var result = await userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

        if (result.Succeeded)
        {
            return Ok(new { Message = "Пароль успішно оновлено!" });
        }

        var errors = result.Errors.Select(e => e.Description);
        return BadRequest(new { Message = "Помилка при зміні пароля", Errors = errors });
    }
}
