// Core/Validators/RegisterModelValidator.cs

using Core.Models.Account;
using FluentValidation;
using Domain.Entities.Idenity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Domain; // Ваш DbContext

namespace Core.Validators;

public class RegisterModelValidator : AbstractValidator<RegisterModel>
{
    // Щоб перевірити унікальність email, нам потрібен AppDbTransferContext
    public RegisterModelValidator(AppDbTransferContext dbContext)
    {
        // 1. Перевірка Email
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email є обов'язковим.")
            .EmailAddress().WithMessage("Введіть коректний Email.")
            .MaximumLength(256).WithMessage("Email не може перевищувати 256 символів.");

        // 2. Перевірка унікальності Email (асинхронна валідація)
        RuleFor(x => x.Email)
            .MustAsync(async (email, cancellation) =>
            {
                // Перевірка, чи не існує користувача з таким Email у базі даних
                var exists = await dbContext.Users
                    .AnyAsync(u => u.Email == email, cancellation);
                return !exists;
            }).WithMessage("Користувач з таким Email вже існує.");

        // 3. Перевірка Пароля
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль є обов'язковим.")
            .MinimumLength(6).WithMessage("Пароль повинен містити не менше 6 символів.");
        // Можете додати додаткові перевірки, які відповідають конфігурації в Program.cs:
        // .Matches("[A-Z]").WithMessage("Пароль повинен містити хоча б одну велику літеру.")
        // тощо. (Але ви їх відключили в Program.cs, тому краще не додавати тут зайві).

        // 4. Перевірка ConfirmPassword
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Паролі не співпадають.");

        // 5. Перевірка First/LastName (за бажанням)
        RuleFor(x => x.FirstName)
            .MaximumLength(100).WithMessage("Ім'я не може перевищувати 100 символів.");

        RuleFor(x => x.LastName)
            .MaximumLength(100).WithMessage("Прізвище не може перевищувати 100 символів.");
    }
}