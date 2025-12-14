using System.Threading.Tasks;

namespace Core.Interfaces
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string toEmail, string subject, string message);
    }
}