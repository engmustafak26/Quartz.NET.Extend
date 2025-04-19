
namespace Demo.Services
{
    public interface ISendEmail
    {
        Task SendEmailAsync(string email);
    }
}
