using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PericonAPI.Classes
{
    public static class AdminSecurity
    {
        public static string GetAdminSecret()
        {
            return Environment.GetEnvironmentVariable("ADMIN_SECRET_KEY")
                ?? "Guardian_SecKey_2026_Pericon$AdminToken!X9#Venezuela";
        }

        public static string GenerateAdminToken()
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string secret = GetAdminSecret();
            string payload = $"{timestamp}:{secret}";
            
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            string hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
            
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{timestamp}:{hash}"));
        }

        public static bool ValidateAdminToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            string secret = GetAdminSecret();

            // Acepta coincidencia directa con la clave maestra configurada
            if (token == secret) return true;

            try
            {
                byte[] bytes = Convert.FromBase64String(token);
                string decoded = Encoding.UTF8.GetString(bytes);
                var parts = decoded.Split(':', 2);
                if (parts.Length != 2) return false;

                if (!long.TryParse(parts[0], out long timestamp)) return false;

                // Válido por 72 horas para comodidad del administrador
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now - timestamp > 72 * 3600 || timestamp > now + 300) return false;

                string payload = $"{timestamp}:{secret}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                string expectedHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(parts[1]),
                    Encoding.UTF8.GetBytes(expectedHash)
                );
            }
            catch
            {
                return false;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AdminAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.HttpContext.Request;
            string path = request.Path.Value?.ToLowerInvariant() ?? "";

            // Endpoints públicos dentro de AdminController:
            // 1. /api/admin/login (para autenticarse y recibir el token firmado)
            // 2. /api/admin/announcement/active (para que los jugadores en /desk lean el aviso activo)
            // 3. /api/admin/errors/report (para que los clientes reporten incidencias y errores en tiempo real)
            if (path.EndsWith("/login") || path.EndsWith("/announcement/active") || path.EndsWith("/errors/report"))
            {
                base.OnActionExecuting(context);
                return;
            }

            string? token = request.Headers["X-Admin-Token"].FirstOrDefault()
                ?? request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();

            if (!AdminSecurity.ValidateAdminToken(token))
            {
                context.Result = new ObjectResult(new
                {
                    success = false,
                    message = "Acceso denegado. Se requiere un Token Secreto de Administrador válido para ejecutar esta acción."
                })
                {
                    StatusCode = 403
                };
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
