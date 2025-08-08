namespace CaseZeroApi.Services
{
    public interface IEmailService
    {
        Task SendEmailVerificationAsync(string email, string userName, string verificationToken);
        Task SendWelcomeEmailAsync(string email, string userName, string policeEmail);
    }
}