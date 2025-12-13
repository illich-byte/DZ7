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
    IAuthService authService) : ControllerBase 
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

        if (user == null || !await userManager.CheckPasswordAsync(user, model.Password))
        {
            return Unauthorized("Invalid email or password.");
        }

        var token = await jwtTokenService.CreateAsync(user);

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
            var resultMessage = await authService.RegisterAsync(model);

            return Ok(new { Message = resultMessage });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpGet]
    [Authorize] 
    public async Task<IActionResult> GetProfile()
    {
        var model = await userService.GetUserProfileAsync();

        return Ok(model);
    }
}