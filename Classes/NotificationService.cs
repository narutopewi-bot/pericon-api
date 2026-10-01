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
        Task SendRechargeNotificationAsync(string username, decimal amountBs, int coins, string reference, string? receiptImageUrl, bool isAutoApproved = false, string? reason = null);
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

        public async Task SendRechargeNotificationAsync(string username, decimal amountBs, int coins, string reference, string? receiptImageUrl, bool isAutoApproved = false, string? reason = null)
        {
            try
            {
                var section = _config.GetSection("Notifications");
                bool enabled = section.GetValue<bool>("Enabled", true);
                if (!enabled) return;

                string headerTitle = isAutoApproved
                    ? "✅ *PERICÓN: RECARGA APROBADA Y PAGADA AL INSTANTE* ⚡"
                    : "🚨 *ALERTA PERICÓN: RECARGA CON ERROR / NO COINCIDE* ⚠️";

                string statusDesc = isAutoApproved
                    ? "*Estado:* ✅ APROBADA Y ACREDITADA AUTOMÁTICAMENTE por Banco del Tesoro (Caja 03)."
                    : $"*Estado:* ⏳ EN REVISIÓN MANUAL (NO se entregaron monedas).\n*Motivo:* {reason ?? "El pago no coincide, no existe o no fue encontrado en el banco."}";

                string actionCall = isAutoApproved
                    ? "✨ _Esta recarga ya fue pagada y acreditada al usuario en vivo. ¡NO tienes que revisar ni hacer nada en el panel!_"
                    : "👉 *ACCIÓN REQUERIDA:* Chequea manualmente el comprobante y aprueba o rechaza en el panel:\nhttps://elpericon.com/admin";

                string notifyMessage = $"{headerTitle}\n\n" +
                                       $"*Usuario:* {username}\n" +
                                       $"*Monto:* {amountBs:N2} Bs. ({coins:N0} Monedas)\n" +
                                       $"*Referencia:* {reference}\n" +
                                       $"{statusDesc}\n" +
                                       $"*Fecha:* {VenezuelaTime.Now:dd/MM/yyyy hh:mm tt} (Hora Vzla)\n\n" +
                                       $"{actionCall}";

                // 1. Notificación WhatsApp (CallMeBot)
                var waPhone = section["WhatsAppPhone"];
                var waApiKey = section["WhatsAppApiKey"];
                if (!string.IsNullOrWhiteSpace(waPhone) && !string.IsNullOrWhiteSpace(waApiKey))
                {
                    await SendWhatsAppAsync(waPhone, waApiKey, notifyMessage);
                }

                // 2. Notificación Bot de Telegram
                var tgToken = section["TelegramBotToken"] ?? Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
                var tgChatId = section["TelegramChatId"] ?? Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");
                if (!string.IsNullOrWhiteSpace(tgToken) && !string.IsNullOrWhiteSpace(tgChatId))
                {
                    await SendTelegramAsync(tgToken, tgChatId, notifyMessage);
                }

                // 3. Notificación Correo Electrónico
                var adminEmail = section["AdminEmail"] ?? "narutopewi@gmail.com";
                var smtpUser = section["SmtpUser"] ?? adminEmail;
                var smtpPass = section["SmtpPass"];

                if (!string.IsNullOrWhiteSpace(smtpPass))
                {
                    string subject = isAutoApproved
                        ? $"✅ [El Pericón] Recarga APROBADA Automáticamente: {username} ({amountBs:N2} Bs.)"
                        : $"🚨 [El Pericón] Recarga en REVISIÓN MANUAL: {username} ({amountBs:N2} Bs.)";

                    string bannerColor = isAutoApproved ? "#10b981" : "#ef4444";
                    string bannerTitle = isAutoApproved ? "✅ Recarga Aprobada y Pagada Automáticamente" : "🚨 Recarga Requiere Revisión Manual";
                    string bannerSub = isAutoApproved ? "Validada y acreditada al instante por Banco del Tesoro (Caja 03). ¡No requiere acción!" : "El pago no coincidió con el banco. Requiere verificación manual del capture.";

                    string bodyHtml = $@"
                    <div style='font-family: Arial, sans-serif; background-color: #0f172a; color: #f8fafc; padding: 24px; border-radius: 12px; max-width: 600px;'>
                        <div style='border-bottom: 2px solid {bannerColor}; padding-bottom: 12px; margin-bottom: 16px;'>
                            <h2 style='color: {bannerColor}; margin: 0;'>{bannerTitle}</h2>
                            <p style='color: #94a3b8; margin: 4px 0 0 0; font-size: 13px;'>{bannerSub}</p>
                        </div>
                        <div style='background-color: #1e293b; padding: 16px; border-radius: 8px; margin-bottom: 16px;'>
                            <p style='margin: 8px 0;'><strong>👤 Usuario:</strong> <span style='color: #38bdf8;'>{username}</span></p>
                            <p style='margin: 8px 0;'><strong>💰 Monto:</strong> <span style='color: #4ade80; font-size: 18px; font-weight: bold;'>{amountBs:N2} Bs.</span> ({coins:N0} Monedas)</p>
                            <p style='margin: 8px 0;'><strong>📝 Referencia:</strong> <code style='background: #334155; padding: 2px 6px; border-radius: 4px;'>{reference}</code></p>
                            <p style='margin: 8px 0;'><strong>📅 Fecha:</strong> {VenezuelaTime.Now:dd/MM/yyyy hh:mm:ss tt} (Hora Vzla)</p>
                            <p style='margin: 8px 0;'><strong>⚙️ Estado:</strong> <span style='color: {bannerColor}; font-weight: bold;'>{(isAutoApproved ? "APROBADO AUTOMÁTICAMENTE" : "EN REVISIÓN MANUAL")}</span></p>
                            {(!isAutoApproved ? $"<p style='margin: 8px 0; color: #fca5a5;'><strong>⚠️ Motivo:</strong> {reason ?? "No encontrado en Banco del Tesoro o monto no coincide"}</p>" : "")}
                        </div>
                        <p style='margin-bottom: 20px;'>
                            <a href='https://elpericon.com/admin' style='background: linear-gradient(135deg, {bannerColor}, #b91c1c); color: #fff; padding: 12px 24px; text-decoration: none; font-weight: bold; border-radius: 8px; display: inline-block;'>
                                {(isAutoApproved ? "Ver en Panel Administrativo" : "Ir al Panel para Aprobar o Rechazar")}
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
                string cleanPhone = phone.Trim().Replace(" ", "").Replace("-", "");
                if (!cleanPhone.StartsWith("+"))
                {
                    cleanPhone = "+" + cleanPhone;
                }
                string encoded = Uri.EscapeDataString(message);
                string url = $"https://api.callmebot.com/whatsapp.php?phone={cleanPhone}&text={encoded}&apikey={apiKey}";
                var response = await _httpClient.GetAsync(url);
                _logger.LogInformation("WhatsApp CallMeBot response status: {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el WhatsApp a través de CallMeBot");
            }
        }

        private async Task SendTelegramAsync(string botToken, string chatId, string message)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
                var parameters = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "chat_id", chatId },
                    { "text", message },
                    { "parse_mode", "Markdown" }
                };
                var response = await _httpClient.PostAsync(url, new FormUrlEncodedContent(parameters));
                _logger.LogInformation("Telegram Bot notification response status: {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar la notificación por Telegram Bot");
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
