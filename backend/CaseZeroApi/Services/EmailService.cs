using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace CaseZeroApi.Services
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string FromEmail { get; set; } = "";
        public string FromName { get; set; } = "";
        public string SmtpUsername { get; set; } = "";
        public string SmtpPassword { get; set; } = "";
        public bool EnableSsl { get; set; } = true;
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;

        public EmailService(IOptionsMonitor<EmailSettings> emailSettings, ILogger<EmailService> logger, IConfiguration configuration)
        {
            _emailSettings = emailSettings.CurrentValue;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SendEmailVerificationAsync(string email, string userName, string verificationToken)
        {
            var subject = "🔒 Verificação de Email - Sistema Policial CaseZero";
            var verificationUrl = $"{_configuration.GetSection("Frontend:BaseUrl").Value}/verify-email?token={verificationToken}";
            
            var htmlBody = GetEmailVerificationTemplate(userName, verificationUrl);
            
            await SendEmailAsync(email, subject, htmlBody);
        }

        public async Task SendWelcomeEmailAsync(string email, string userName, string policeEmail)
        {
            var subject = "🎯 Bem-vindo ao Sistema CaseZero!";
            var htmlBody = GetWelcomeEmailTemplate(userName, policeEmail);
            
            await SendEmailAsync(email, subject, htmlBody);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                // If email settings are not configured, log and skip sending
                if (string.IsNullOrEmpty(_emailSettings.FromEmail) || string.IsNullOrEmpty(_emailSettings.SmtpServer))
                {
                    _logger.LogWarning("Email settings not configured. Email would be sent to {Email} with subject: {Subject}", toEmail, subject);
                    return;
                }

                using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort);
                client.EnableSsl = _emailSettings.EnableSsl;
                client.Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
                throw;
            }
        }

        private string GetEmailVerificationTemplate(string userName, string verificationUrl)
        {
            return $@"
<!DOCTYPE html>
<html lang='pt-BR'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Verificação de Email - CaseZero</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 0; background-color: #0a0f23; color: #fff; }}
        .container {{ max-width: 600px; margin: 0 auto; background: linear-gradient(135deg, #1a2140 0%, #2a3458 50%, #1a2140 100%); }}
        .header {{ background: rgba(0, 0, 0, 0.4); padding: 2rem; text-align: center; border-bottom: 2px solid #3498db; }}
        .logo {{ font-size: 3rem; margin-bottom: 1rem; }}
        .title {{ color: #3498db; font-size: 1.8rem; margin: 0; text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.5); }}
        .subtitle {{ color: rgba(52, 152, 219, 0.8); margin: 0.5rem 0 0 0; }}
        .content {{ padding: 2rem; }}
        .welcome {{ font-size: 1.1rem; margin-bottom: 1.5rem; line-height: 1.6; }}
        .verify-button {{ display: inline-block; background: linear-gradient(135deg, #3498db, #2980b9); color: white; padding: 1rem 2rem; text-decoration: none; border-radius: 0.5rem; font-weight: 600; margin: 1rem 0; box-shadow: 0 4px 15px rgba(52, 152, 219, 0.3); }}
        .verify-button:hover {{ background: linear-gradient(135deg, #2980b9, #3498db); }}
        .warning {{ background: rgba(231, 76, 60, 0.1); border: 1px solid rgba(231, 76, 60, 0.3); border-radius: 0.5rem; padding: 1rem; margin: 1.5rem 0; color: rgba(231, 76, 60, 0.9); }}
        .footer {{ background: rgba(0, 0, 0, 0.3); padding: 1.5rem; text-align: center; font-size: 0.9rem; color: rgba(255, 255, 255, 0.7); }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo'>🏛️</div>
            <h1 class='title'>Sistema CaseZero</h1>
            <p class='subtitle'>Departamento de Polícia Metropolitana</p>
        </div>
        
        <div class='content'>
            <div class='welcome'>
                <strong>Olá, {userName}!</strong><br><br>
                Sua solicitação de registro no Sistema CaseZero foi recebida com sucesso. Para ativar sua conta e começar suas investigações, você precisa verificar seu endereço de email.
            </div>
            
            <div style='text-align: center;'>
                <a href='{verificationUrl}' class='verify-button'>
                    🔐 Verificar Email
                </a>
            </div>
            
            <div class='warning'>
                <strong>⚠️ Importante:</strong> Este link é válido por 24 horas. Se você não clicou no link, ignore este email.
            </div>
            
            <p>Se o botão não funcionar, copie e cole este link no seu navegador:</p>
            <p style='word-break: break-all; color: #3498db;'>{verificationUrl}</p>
        </div>
        
        <div class='footer'>
            <p>Sistema CaseZero - Investigações Detetivescas</p>
            <p>Este é um email automático, não responda.</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GetWelcomeEmailTemplate(string userName, string policeEmail)
        {
            return $@"
<!DOCTYPE html>
<html lang='pt-BR'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Bem-vindo ao CaseZero</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 0; background-color: #0a0f23; color: #fff; }}
        .container {{ max-width: 600px; margin: 0 auto; background: linear-gradient(135deg, #1a2140 0%, #2a3458 50%, #1a2140 100%); }}
        .header {{ background: rgba(0, 0, 0, 0.4); padding: 2rem; text-align: center; border-bottom: 2px solid #3498db; }}
        .logo {{ font-size: 3rem; margin-bottom: 1rem; }}
        .title {{ color: #3498db; font-size: 1.8rem; margin: 0; text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.5); }}
        .subtitle {{ color: rgba(52, 152, 219, 0.8); margin: 0.5rem 0 0 0; }}
        .content {{ padding: 2rem; }}
        .welcome {{ font-size: 1.1rem; margin-bottom: 1.5rem; line-height: 1.6; }}
        .badge {{ background: rgba(52, 152, 219, 0.1); border: 1px solid rgba(52, 152, 219, 0.3); border-radius: 0.5rem; padding: 1.5rem; margin: 1.5rem 0; }}
        .badge-title {{ color: #3498db; font-weight: 600; margin-bottom: 1rem; }}
        .info-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }}
        .info-item {{ background: rgba(255, 255, 255, 0.05); padding: 0.8rem; border-radius: 0.3rem; }}
        .info-label {{ color: rgba(255, 255, 255, 0.7); font-size: 0.9rem; }}
        .info-value {{ color: #3498db; font-weight: 600; }}
        .footer {{ background: rgba(0, 0, 0, 0.3); padding: 1.5rem; text-align: center; font-size: 0.9rem; color: rgba(255, 255, 255, 0.7); }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo'>🎯</div>
            <h1 class='title'>Bem-vindo ao CaseZero!</h1>
            <p class='subtitle'>Divisão de Casos Frios - Departamento de Polícia</p>
        </div>
        
        <div class='content'>
            <div class='welcome'>
                <strong>Parabéns, {userName}!</strong><br><br>
                Sua conta foi ativada com sucesso! Você agora faz parte da elite de investigadores do Sistema CaseZero. Como um novo <strong>Rook</strong>, você está pronto para começar sua jornada resolvendo os casos mais desafiadores.
            </div>
            
            <div class='badge'>
                <div class='badge-title'>🏅 Suas Credenciais de Acesso</div>
                <div class='info-grid'>
                    <div class='info-item'>
                        <div class='info-label'>Email Institucional:</div>
                        <div class='info-value'>{policeEmail}</div>
                    </div>
                    <div class='info-item'>
                        <div class='info-label'>Posição:</div>
                        <div class='info-value'>Rook</div>
                    </div>
                    <div class='info-item'>
                        <div class='info-label'>Departamento:</div>
                        <div class='info-value'>ColdCase</div>
                    </div>
                    <div class='info-item'>
                        <div class='info-label'>Status:</div>
                        <div class='info-value'>Ativo</div>
                    </div>
                </div>
            </div>
            
            <div style='margin: 2rem 0;'>
                <h3 style='color: #3498db;'>🔍 Próximos Passos:</h3>
                <ul style='line-height: 1.8;'>
                    <li>Acesse o sistema com seu email institucional</li>
                    <li>Explore o dashboard para ver casos disponíveis</li>
                    <li>Comece com casos de nível Rook</li>
                    <li>Colete evidências e resolva seus primeiros casos</li>
                    <li>Suba de ranking conforme sua experiência</li>
                </ul>
            </div>
        </div>
        
        <div class='footer'>
            <p>Sistema CaseZero - Divisão de Casos Frios</p>
            <p>""A justiça nunca descansa""</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}