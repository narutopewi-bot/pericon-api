using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PericonAPI.Classes
{
    public interface INotificationService
    {
        Task SendRechargeNotificationAsync(string username, decimal amountBs, int coins, string reference, string? receiptImageUrl);
        Task SendWithdrawalNotificationAsync(string username, decimal amountBs, int coins, string bankName, string phone, string idCard);
    }

    public class NotificationService : INotificationService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<NotificationService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _webRootPath;

        public NotificationService(IConfiguration config, ILogger<NotificationService> logger, IHttpClientFactory httpClientFactory, IWebHostEnvironment env)
        {
            _config = config;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _webRootPath = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        }

        public async Task SendRechargeNotificationAsync(string username, decimal amountBs, int coins, string reference, string? receiptImageUrl)
        {
            try
            {
                var section = _config.GetSection("Notifications");
                bool enabled = section.GetValue<bool>("Enabled", true);
                if (!enabled) return;

                // 1. Notificación WhatsApp
                var waPhone = section["WhatsAppPhone"];
                var waApiKey = section["WhatsAppApiKey"];
                if (!string.IsNullOrWhiteSpace(waPhone) && !string.IsNullOrWhiteSpace(waApiKey))
                {
                    string waMessage = $"*PERICÓN: NUEVA RECARGA REPORTADA*\n\n" +
                                      $"*Usuario:* {username}\n" +
                                      $"*Monto:* {amountBs:N2} Bs. ({coins:N0} Monedas)\n" +
                                      $"*Referencia:* {reference}\n" +
                                      $"*Fecha:* {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC\n\n" +
                                      $"Revisa y aprueba en el panel:\nhttps://elpericon.com/admin";

                    await SendWhatsAppAsync(waPhone, waApiKey, waMessage);
                }

                // 2. Notificación Correo Electrónico
                var adminEmail = section["AdminEmail"] ?? "narutopewi@gmail.com";
                var smtpUser = section["SmtpUser"] ?? adminEmail;
                var smtpPass = section["SmtpPass"];

                if (!string.IsNullOrWhiteSpace(smtpPass))
                {
                    string subject = $"🔔 [El Pericón] Nueva Recarga: {username} ({amountBs:N2} Bs.)";
                    string bodyHtml = $@"
                    <div style='font-family: Arial, sans-serif; background-color: #0f172a; color: #f8fafc; padding: 24px; border-radius: 12px; max-width: 600px;'>
                        <div style='border-bottom: 2px solid #eab308; padding-bottom: 12px; margin-bottom: 16px;'>
                            <h2 style='color: #eab308; margin: 0;'>🔔 Nueva Recarga Reportada</h2>
                            <p style='color: #94a3b8; margin: 4px 0 0 0; font-size: 13px;'>Plataforma Oficial El Pericón</p>
                        </div>
                        <div style='background-color: #1e293b; padding: 16px; border-radius: 8px; margin-bottom: 16px;'>
                            <p style='margin: 8px 0;'><strong>👤 Usuario:</strong> <span style='color: #38bdf8;'>{username}</span></p>
                            <p style='margin: 8px 0;'><strong>💰 Monto:</strong> <span style='color: #4ade80; font-size: 18px; font-weight: bold;'>{amountBs:N2} Bs.</span> ({coins:N0} Monedas)</p>
                            <p style='margin: 8px 0;'><strong>📝 Referencia:</strong> <code style='background: #334155; padding: 2px 6px; border-radius: 4px;'>{reference}</code></p>
                            <p style='margin: 8px 0;'><strong>📅 Fecha:</strong> {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss} UTC</p>
                        </div>
                        <p style='margin-bottom: 20px;'>
                            <a href='https://elpericon.com/admin' style='background: linear-gradient(135deg, #eab308, #ca8a04); color: #000; padding: 12px 24px; text-decoration: none; font-weight: bold; border-radius: 8px; display: inline-block;'>
                                Ir al Panel Administrativo para Aprobar
                            </a>
                        </p>
                    </div>";

                    string? localImagePath = null;
                    if (!string.IsNullOrWhiteSpace(receiptImageUrl))
                    {
                        var cleanRel = receiptImageUrl.TrimStart('/', '\\');
                        var testPath = Path.Combine(_webRootPath, cleanRel);
                        if (File.Exists(testPath))
                        {
                            localImagePath = testPath;
                        }
                    }

                    await SendEmailAsync(adminEmail, smtpUser, smtpPass, subject, bodyHtml, localImagePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando notificación de recarga");
            }
        }

        public async Task SendWithdrawalNotificationAsync(string username, decimal amountBs, int coins, string bankName, string phone, string idCard)
        {
            try
            {
                var section = _config.GetSection("Notifications");
                bool enabled = section.GetValue<bool>("Enabled", true);
                if (!enabled) return;

                // 1. Notificación WhatsApp
                var waPhone = section["WhatsAppPhone"];
                var waApiKey = section["WhatsAppApiKey"];
                if (!string.IsNullOrWhiteSpace(waPhone) && !string.IsNullOrWhiteSpace(waApiKey))
                {
                    string waMessage = $"*PERICÓN: SOLICITUD DE RETIRO*\n\n" +
                                      $"*Usuario:* {username}\n" +
                                      $"*Monto a Pagar:* {amountBs:N2} Bs. ({coins:N0} Monedas)\n" +
                                      $"*Banco:* {bankName}\n" +
                                      $"*Teléfono Pago Móvil:* {phone}\n" +
                                      $"*Cédula:* {idCard}\n" +
                                      $"*Fecha:* {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC\n\n" +
                                      $"Procesa el retiro en el panel:\nhttps://elpericon.com/admin";

                    await SendWhatsAppAsync(waPhone, waApiKey, waMessage);
                }

                // 2. Notificación Correo Electrónico
                var adminEmail = section["AdminEmail"] ?? "narutopewi@gmail.com";
                var smtpUser = section["SmtpUser"] ?? adminEmail;
                var smtpPass = section["SmtpPass"];

                if (!string.IsNullOrWhiteSpace(smtpPass))
                {
                    string subject = $"💸 [El Pericón] Solicitud de Retiro: {username} ({amountBs:N2} Bs.)";
                    string bodyHtml = $@"
                    <div style='font-family: Arial, sans-serif; background-color: #0f172a; color: #f8fafc; padding: 24px; border-radius: 12px; max-width: 600px;'>
                        <div style='border-bottom: 2px solid #ef4444; padding-bottom: 12px; margin-bottom: 16px;'>
                            <h2 style='color: #ef4444; margin: 0;'>💸 Solicitud de Retiro de Bolívares</h2>
                            <p style='color: #94a3b8; margin: 4px 0 0 0; font-size: 13px;'>Plataforma Oficial El Pericón</p>
                        </div>
                        <div style='background-color: #1e293b; padding: 16px; border-radius: 8px; margin-bottom: 16px;'>
                            <p style='margin: 8px 0;'><strong>👤 Usuario:*</strong> <span style='color: #38bdf8;'>{username}</span></p>
                            <p style='margin: 8px 0;'><strong>💰 Monto a Transferir:</strong> <span style='color: #f87171; font-size: 18px; font-weight: bold;'>{amountBs:N2} Bs.</span> ({coins:N0} Monedas)</p>
                            <hr style='border: 0; border-top: 1px solid #334155; margin: 12px 0;' />
                            <h4 style='color: #e2e8f0; margin: 8px 0;'>Datos de Pago Móvil Receptor:</h4>
                            <p style='margin: 6px 0;'><strong>🏛️ Banco:</strong> {bankName}</p>
                            <p style='margin: 6px 0;'><strong>📱 Teléfono:</strong> <code>{phone}</code></p>
                            <p style='margin: 6px 0;'><strong>🪪 Cédula:</strong> <code>{idCard}</code></p>
                            <p style='margin: 6px 0;'><strong>📅 Fecha:</strong> {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss} UTC</p>
                        </div>
                        <p style='margin-bottom: 20px;'>
                            <a href='https://elpericon.com/admin' style='background: linear-gradient(135deg, #ef4444, #dc2626); color: #fff; padding: 12px 24px; text-decoration: none; font-weight: bold; border-radius: 8px; display: inline-block;'>
                                Abrir Panel y Registrar Referencia de Pago
                            </a>
                        </p>
                    </div>";

                    await SendEmailAsync(adminEmail, smtpUser, smtpPass, subject, bodyHtml, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando notificación de retiro");
            }
        }

        private async Task SendWhatsAppAsync(string phone, string apiKey, string message)
        {
            try
            {
                string encoded = Uri.EscapeDataString(message);
                string url = $"https://api.callmebot.com/whatsapp.php?phone={phone}&text={encoded}&apikey={apiKey}";
                var response = await _httpClient.GetAsync(url);
                _logger.LogInformation("WhatsApp CallMeBot response status: {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el WhatsApp a través de CallMeBot");
            }
        }

        private async Task SendEmailAsync(string toEmail, string smtpUser, string smtpPass, string subject, string htmlBody, string? attachmentPath)
        {
            try
            {
                using var mail = new MailMessage();
                mail.From = new MailAddress(smtpUser, "El Pericón Notificaciones");
                mail.To.Add(toEmail);
                mail.Subject = subject;
                mail.Body = htmlBody;
                mail.IsBodyHtml = true;

                if (!string.IsNullOrWhiteSpace(attachmentPath) && File.Exists(attachmentPath))
                {
                    mail.Attachments.Add(new Attachment(attachmentPath));
                }

                using var smtp = new SmtpClient("smtp.gmail.com", 587);
                smtp.EnableSsl = true;
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new NetworkCredential(smtpUser, smtpPass.Replace(" ", ""));

                await smtp.SendMailAsync(mail);
                _logger.LogInformation("Correo de notificación enviado exitosamente a {ToEmail}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el correo de notificación por SMTP");
            }
        }
    }
}
