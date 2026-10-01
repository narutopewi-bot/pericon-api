using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PericonAPI.Classes
{
    public interface ITesoroPagosService
    {
        Task<(bool Success, bool Approved, string Message)> ValidatePaymentAsync(decimal amountBs, string? originBank, string? originPhone, string reference);
    }

    public class TesoroPagosService : ITesoroPagosService
    {
        private readonly ILogger<TesoroPagosService> _logger;
        private readonly IConfiguration _config;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private CookieContainer _cookieContainer = new();
        private HttpClient _httpClient;
        private DateTime _lastSessionTime = DateTime.MinValue;
        private bool _isAuthenticated = false;

        private const string BASE_URL = "https://tesoropagos.bt.com.ve";
        private const string SUCURSAL_DEFAULT = "01597001";
        private const string CAJA_DEFAULT = "03";
        private const string PASSWORD_DEFAULT = "0000";

        public TesoroPagosService(ILogger<TesoroPagosService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _httpClient = CreateHttpClient();
        }

        private HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                UseCookies = true,
                AllowAutoRedirect = true,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(BASE_URL),
                Timeout = TimeSpan.FromSeconds(25)
            };

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "es-ES,es;q=0.9");

            return client;
        }

        private void ResetClient()
        {
            _cookieContainer = new CookieContainer();
            _httpClient = CreateHttpClient();
            _isAuthenticated = false;
        }

        private string ExtractBankCode(string? bank)
        {
            if (string.IsNullOrWhiteSpace(bank)) return "0102"; // Default Venezuela
            var match = Regex.Match(bank, @"\b(\d{4})\b");
            if (match.Success) return match.Groups[1].Value;
            return "0102";
        }

        private string CleanPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "04120000000";
            var clean = Regex.Replace(phone, @"[^\d]", "");
            if (clean.Length == 10 && clean.StartsWith("4")) clean = "0" + clean;
            if (clean.Length > 11) clean = clean.Substring(clean.Length - 11);
            if (clean.Length < 11) clean = clean.PadLeft(11, '0');
            return clean;
        }

        private string CleanReference(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return "000000";
            var digits = Regex.Replace(reference, @"[^\d]", "");
            if (digits.Length >= 6) return digits.Substring(digits.Length - 6);
            return digits.PadLeft(6, '0');
        }

        private string FormatAmount(decimal amount)
        {
            // Tesoro Pagos formatCurrency: Ej. "800,00" o "1.500,00"
            return amount.ToString("N2", new CultureInfo("es-VE"));
        }

        private async Task<bool> EnsureAuthenticatedAsync()
        {
            // Si la sesión tiene menos de 2.5 minutos y ya autenticó, la consideramos activa
            if (_isAuthenticated && (DateTime.UtcNow - _lastSessionTime).TotalMinutes < 2.5)
            {
                return true;
            }

            try
            {
                var sucursal = _config["TesoroPagos:Sucursal"] ?? SUCURSAL_DEFAULT;
                var caja = _config["TesoroPagos:Caja"] ?? CAJA_DEFAULT;
                var password = _config["TesoroPagos:Password"] ?? PASSWORD_DEFAULT;

                _logger.LogInformation("[TesoroPagos] Iniciando sesión para Sucursal {Sucursal}, Caja {Caja}...", sucursal, caja);

                // 1. Obtener página de login para CSRF token
                var loginGetResp = await _httpClient.GetAsync("/login");
                var loginHtml = await loginGetResp.Content.ReadAsStringAsync();

                var tokenMatch = Regex.Match(loginHtml, @"name=""_token"" value=""([^""]+)""");
                if (!tokenMatch.Success)
                {
                    _logger.LogWarning("[TesoroPagos] No se pudo encontrar token CSRF en página de login");
                    return false;
                }
                var csrfToken = tokenMatch.Groups[1].Value;

                // 2. Enviar POST login
                var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "_token", csrfToken },
                    { "security_code", sucursal },
                    { "box_number", caja },
                    { "password", password }
                });

                var loginPostResp = await _httpClient.PostAsync("/login", postContent);
                var postHtml = await loginPostResp.Content.ReadAsStringAsync();

                if (postHtml.Contains("La caja ya tiene una sesión activa", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("[TesoroPagos] La caja ya tiene sesión activa. Verificando acceso directo...");
                    // Si ya tiene sesión activa, intentamos acceder a /pago-movil
                    var testPm = await _httpClient.GetAsync("/pago-movil");
                    var testHtml = await testPm.Content.ReadAsStringAsync();
                    if (testHtml.Contains("form-confirmacion-pago"))
                    {
                        _isAuthenticated = true;
                        _lastSessionTime = DateTime.UtcNow;
                        return true;
                    }
                }

                if (loginPostResp.RequestMessage?.RequestUri?.ToString().Contains("dashboard") == true ||
                    loginPostResp.RequestMessage?.RequestUri?.ToString().Contains("pago-movil") == true ||
                    postHtml.Contains("form-confirmacion-pago") ||
                    postHtml.Contains("btn-validar-pago"))
                {
                    _logger.LogInformation("[TesoroPagos] Sesión iniciada con éxito");
                    _isAuthenticated = true;
                    _lastSessionTime = DateTime.UtcNow;
                    return true;
                }

                // Verificar si llegó al dashboard
                var checkDashboard = await _httpClient.GetAsync("/pago-movil");
                var checkHtml = await checkDashboard.Content.ReadAsStringAsync();
                if (checkHtml.Contains("form-confirmacion-pago") || checkHtml.Contains("btn-validar-pago"))
                {
                    _logger.LogInformation("[TesoroPagos] Acceso confirmado a /pago-movil");
                    _isAuthenticated = true;
                    _lastSessionTime = DateTime.UtcNow;
                    return true;
                }

                _logger.LogWarning("[TesoroPagos] No se pudo validar la sesión tras el login");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TesoroPagos] Error en EnsureAuthenticatedAsync");
                return false;
            }
        }

        public async Task<(bool Success, bool Approved, string Message)> ValidatePaymentAsync(decimal amountBs, string? originBank, string? originPhone, string reference)
        {
            await _lock.WaitAsync();
            try
            {
                bool authOk = await EnsureAuthenticatedAsync();
                if (!authOk)
                {
                    // Si falló por sesión desfasada, reiniciamos el cliente y reintentamos una vez
                    ResetClient();
                    authOk = await EnsureAuthenticatedAsync();
                    if (!authOk)
                    {
                        return (false, false, "No se pudo establecer conexión segura con el Banco del Tesoro");
                    }
                }

                var cleanBank = ExtractBankCode(originBank);
                var cleanPhone = CleanPhone(originPhone);
                var cleanRef = CleanReference(reference);
                var cleanAmount = FormatAmount(amountBs);

                _logger.LogInformation("[TesoroPagos] Validando pago: Monto={Monto}, Banco={Banco}, Teléfono={Telefono}, Ref={Ref}",
                    cleanAmount, cleanBank, cleanPhone, cleanRef);

                // 1. Obtener CSRF token fresco de la página /pago-movil
                var pmResp = await _httpClient.GetAsync("/pago-movil");
                var pmHtml = await pmResp.Content.ReadAsStringAsync();

                var formTokenMatch = Regex.Match(pmHtml, @"id=""form-confirmacion-pago""[^>]*>.*?name=""_token"" value=""([^""]+)""", RegexOptions.Singleline);
                string formToken = formTokenMatch.Success 
                    ? formTokenMatch.Groups[1].Value 
                    : Regex.Match(pmHtml, @"<meta name=""csrf-token"" content=""([^""]+)""").Groups[1].Value;

                if (string.IsNullOrWhiteSpace(formToken))
                {
                    _logger.LogWarning("[TesoroPagos] No se pudo obtener el token de formulario en /pago-movil");
                    return (false, false, "Error obteniendo token de validación bancario");
                }

                // 2. Enviar validación a /pago-movil
                var valContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "_token", formToken },
                    { "monto", cleanAmount },
                    { "banco", cleanBank },
                    { "telefono", cleanPhone },
                    { "referencia", cleanRef }
                });

                var valResp = await _httpClient.PostAsync("/pago-movil", valContent);
                var valHtml = await valResp.Content.ReadAsStringAsync();

                _logger.LogInformation("[TesoroPagos] Respuesta HTTP de validación: {Status}, longitud {Length}", valResp.StatusCode, valHtml.Length);

                // 3. Analizar respuesta del banco
                if (valHtml.Contains("Pago Movil No encontrado", StringComparison.OrdinalIgnoreCase) ||
                    valHtml.Contains("no encontrado", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("[TesoroPagos] Resultado: Pago NO encontrado en el banco");
                    return (true, false, "Pago no encontrado en Banco del Tesoro. Verifica el monto, banco emisor y referencia.");
                }

                // Si contiene indicadores de éxito o aprobación
                if (valHtml.Contains("Confirmado", StringComparison.OrdinalIgnoreCase) ||
                    valHtml.Contains("Aprobado", StringComparison.OrdinalIgnoreCase) ||
                    valHtml.Contains("Exitoso", StringComparison.OrdinalIgnoreCase) ||
                    valHtml.Contains("Pago Movil Confirmado", StringComparison.OrdinalIgnoreCase) ||
                    (valHtml.Contains("border-green") && !valHtml.Contains("border-red")))
                {
                    _logger.LogInformation("[TesoroPagos] Resultado: ¡PAGO CONFIRMADO EXITOSAMENTE!");
                    return (true, true, "Pago móvil confirmado y verificado con éxito en Banco del Tesoro");
                }

                // Si la sesión expiró justo en este momento
                if (valHtml.Contains("Sesión Expirada", StringComparison.OrdinalIgnoreCase) ||
                    valResp.RequestMessage?.RequestUri?.ToString().Contains("login") == true)
                {
                    _logger.LogWarning("[TesoroPagos] Sesión expiró durante la consulta. Reiniciando...");
                    _isAuthenticated = false;
                    return (false, false, "Sesión bancaria renovándose. Por favor intenta de nuevo en unos segundos.");
                }

                _logger.LogWarning("[TesoroPagos] Respuesta indeterminada de Tesoro Pagos. Guardado para revisión");
                return (false, false, "El banco no devolvió un estado concluyente inmediato.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TesoroPagos] Excepción en ValidatePaymentAsync");
                return (false, false, $"Error técnico al consultar el banco: {ex.Message}");
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
