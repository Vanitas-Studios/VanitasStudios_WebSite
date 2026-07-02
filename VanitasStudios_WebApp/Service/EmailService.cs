using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using VanitasStudios_WebApp.Models;

namespace VanitasStudios_WebApp.Service
{
    public class EmailService : IEmailSender<ApplicationUser>
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public EmailService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["ResendSettings:ApiKey"]
                ?? throw new ArgumentNullException("Resend API Key non configurata!");
        }

        // 1. Questo metodo viene chiamato in automatico da Identity alla REGISTRAZIONE
        public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
        {
            var message = new EmailMessage
            {
                To = email,
                Subject = "Vanitas Studios - Conferma il tuo account",
                Body = $"<h3>Benvenuto a bordo!</h3><p>Per attivare il tuo profilo su Vanitas Studios, clicca sul link seguente:</p><p><a href='{confirmationLink}'>Attiva Account</a></p>"
            };

            await SendEmailRawAsync(message);
        }

        // 2. Questo metodo viene chiamato in automatico da Identity per il RECUPERO PASSWORD
        public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
        {
            var message = new EmailMessage
            {
                To = email,
                Subject = "Vanitas Studios - Ripristino Password",
                Body = $"<p>Hai richiesto il reset della password. Clicca qui per reimpostarla:</p><p><a href='{resetLink}'>Reimposta Password</a></p>"
            };

            await SendEmailRawAsync(message);
        }

        // 3. Questo metodo viene chiamato per i codici a 2 fattori (se li attiverai)
        public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
        {
            var message = new EmailMessage
            {
                To = email,
                Subject = "Vanitas Studios - Codice di Sicurezza",
                Body = $"<p>Il tuo codice di sicurezza temporaneo è: <b>{resetCode}</b></p>"
            };

            await SendEmailRawAsync(message);
        }

        // Il motore interno che contatta Resend via HTTP (il codice che abbiamo scritto prima)
        private async Task<bool> SendEmailRawAsync(EmailMessage message)
        {
            var requestUri = "https://api.resend.com/emails";
            var payload = new
            {
                from = "Vanitas Studios <noreply@vanitas-studios.com>", // Usa un indirizzo del dominio verificato
                to = new[] { message.To },
                subject = message.Subject,
                html = message.Body
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.PostAsync(requestUri, content);

            return response.IsSuccessStatusCode;
        }
    }
    public class EmailMessage
    {
        public string To { get; set; } = null!;
        public string Subject { get; set; } = null!;
        public string Body { get; set; } = null!;
    }
}
