using Core.Models.Account;
using FluentValidation;
using Domain.Entities.Idenity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Domain; 

namespace Core.Validators;

public class RegisterModelValidator : AbstractValidator<RegisterModel>
{
    public RegisterModelValidator(AppDbTransferContext dbContext)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email є обов'язковим.")
            .EmailAddress().WithMessage("Введіть коректний Email.")
            .MaximumLength(256).WithMessage("Email не може перевищувати 256 символів.");

        RuleFor(x => x.Email)
            .MustAsync(async (email, cancellation) =>
            {
                var exists = await dbContext.Users
                    .AnyAsync(u => u.Email == email, cancellation);
                return !exists;
            }).WithMessage("Користувач з таким Email вже існує.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль є обов'язковим.")
            .MinimumLength(6).WithMessage("Пароль повинен містити не менше 6 символів.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Паролі не співпадають.");

        RuleFor(x => x.FirstName)
            .MaximumLength(100).WithMessage("Ім'я не може перевищувати 100 символів.");

        RuleFor(x => x.LastName)
            .MaximumLength(100).WithMessage("Прізвище не може перевищувати 100 символів.");
    }
}