
namespace Demo.Services
{
    public class SendEmail : ISendEmail
    {
        public Task SendEmailAsync(string email)
        {
            Console.WriteLine("Send Email: " + email);
            return Task.CompletedTask;
        }
    }
}
