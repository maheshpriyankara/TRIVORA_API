using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TrivoraHrisBackend.Models;

namespace TRIVORA_API.Service
{
    public class EmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        // ────────────────────────────────────────────────────────────────
        // BACKWARD-COMPATIBLE overload — unchanged signature.
        // Keeps existing callers (pay-slip emails, etc.) working as-is.
        // ────────────────────────────────────────────────────────────────
        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            byte[]? pdfAttachment = null,
            string? pdfFileName = null)
        {
            return SendAsync(
                toEmail,
                subject,
                htmlBody,
                pdfAttachment,
                pdfFileName,
                cc: null,
                bcc: null);
        }

        // ────────────────────────────────────────────────────────────────
        // NEW overload — supports CC and BCC (attendance report emails).
        // Accepts comma/semicolon-separated lists for To / Cc / Bcc.
        // ────────────────────────────────────────────────────────────────
        public async Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            byte[]? pdfAttachment,
            string? pdfFileName,
            IEnumerable<string>? cc,
            IEnumerable<string>? bcc)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));

            // ── To ──
            var toList = SplitAddresses(toEmail);
            if (toList.Count == 0)
                throw new ArgumentException("At least one 'To' address is required.", nameof(toEmail));

            foreach (var addr in toList)
                message.To.Add(MailboxAddress.Parse(addr));

            // ── Cc (optional) ──
            if (cc != null)
            {
                foreach (var addr in SplitAddresses(cc))
                    message.Cc.Add(MailboxAddress.Parse(addr));
            }

            // ── Bcc (optional) ──
            if (bcc != null)
            {
                foreach (var addr in SplitAddresses(bcc))
                    message.Bcc.Add(MailboxAddress.Parse(addr));
            }

            message.Subject = subject;

            // ── Body + attachment ──
            var builder = new BodyBuilder { HtmlBody = htmlBody };

            if (pdfAttachment != null && pdfAttachment.Length > 0)
            {
                builder.Attachments.Add(
                    pdfFileName ?? "attachment.pdf",
                    pdfAttachment,
                    new ContentType("application", "pdf"));
            }

            message.Body = builder.ToMessageBody();

            // ── Send ──
            using var client = new SmtpClient();

            await client.ConnectAsync(
                _settings.SmtpServer,
                _settings.SmtpPort,
                _settings.EnableSsl
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTlsWhenAvailable);

            if (!string.IsNullOrEmpty(_settings.Username))
                await client.AuthenticateAsync(_settings.Username, _settings.Password);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        // ────────────────────────────────────────────────────────────────
        // Helper — split an address list ("a@x.com, b@y.com; c@z.com")
        // into a clean List<string>. Handles a single plain string as well.
        // ────────────────────────────────────────────────────────────────
        private static List<string> SplitAddresses(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            return value
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        private static List<string> SplitAddresses(IEnumerable<string>? values)
        {
            if (values == null) return new List<string>();

            return values
                .SelectMany(v => (v ?? "").Split(new[] { ',', ';' },
                                                 StringSplitOptions.RemoveEmptyEntries))
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }
    }
}