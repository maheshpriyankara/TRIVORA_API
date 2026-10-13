using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TrivoraHrisBackend.Models;

namespace TrivoraHrisBackend.Service
{
    public class EmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            byte[]? pdfAttachment = null,
            string? pdfFileName = null)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlBody };

            if (pdfAttachment != null && pdfAttachment.Length > 0)
            {
                builder.Attachments.Add(
                    pdfFileName ?? "payslip.pdf",
                    pdfAttachment,
                    new ContentType("application", "pdf"));
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _settings.SmtpServer,
                _settings.SmtpPort,
                _settings.EnableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable);

            if (!string.IsNullOrEmpty(_settings.Username))
                await client.AuthenticateAsync(_settings.Username, _settings.Password);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}