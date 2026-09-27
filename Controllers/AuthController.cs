using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PericonAPI.Data;
using PericonAPI.Models;

namespace PericonAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var cleanEmail = dto.Email.Trim().ToLowerInvariant();
            var cleanUsername = dto.Username.Trim();

            // 1. Bloqueo de correos temporales / desechables
            var disposableDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "tempmail.com", "temp-mail.org", "guerrillamail.com", "yopmail.com", "10minutemail.com",
                "mailinator.com", "throwawaymail.com", "trashmail.com", "getairmail.com", "dispostable.com",
                "sharklasers.com", "fakeinbox.com", "mohmal.com", "burnermail.io", "mytemp.email", "tempmail.net",
                "crazymailing.com", "armyspy.com", "cuvox.de", "dayrep.com", "fleckens.hu", "gustr.com"
            };
            var emailParts = cleanEmail.Split('@');
            if (emailParts.Length == 2 && disposableDomains.Contains(emailParts[1]))
            {
                return BadRequest(new { message = "No se permiten correos electrónicos temporales o desechables. Por favor usa un correo personal válido (Gmail, Outlook, Yahoo, etc.)." });
            }

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == cleanEmail))
            {
                return BadRequest(new { message = "El correo ya se encuentra registrado." });
            }

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == cleanUsername.ToLower()))
            {
                return BadRequest(new { message = "El nombre de usuario ya está en uso." });
            }

            // 2. Validación de Mayor de Edad (Fecha de Nacimiento >= 18 años)
            DateTime? parsedBirthDate = null;
            if (!string.IsNullOrWhiteSpace(dto.BirthDate))
            {
                if (DateTime.TryParse(dto.BirthDate, out var bDate))
                {
                    parsedBirthDate = DateTime.SpecifyKind(bDate, DateTimeKind.Utc);
                    var today = DateTime.UtcNow;
                    var age = today.Year - bDate.Year;
                    if (bDate.Date > today.AddYears(-age)) age--;
                    if (age < 18)
                    {
                        return BadRequest(new { message = "Debes tener al menos 18 años cumplidos para registrarte y jugar en El Pericón." });
                    }
                }
                else
                {
                    return BadRequest(new { message = "Formato de fecha de nacimiento inválido." });
                }
            }
            else
            {
                return BadRequest(new { message = "La fecha de nacimiento es obligatoria para verificar que seas mayor de edad." });
            }

            // 3. Validación de Cédula de Identidad (Única)
            var cleanCedula = dto.Cedula?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(cleanCedula))
            {
                return BadRequest(new { message = "La cédula de identidad es requerida para el registro." });
            }
            cleanCedula = System.Text.RegularExpressions.Regex.Replace(cleanCedula, @"\s+", "");
            if (!cleanCedula.StartsWith("V-") && !cleanCedula.StartsWith("E-"))
            {
                if (cleanCedula.StartsWith("V") || cleanCedula.StartsWith("E"))
                {
                    cleanCedula = cleanCedula.Substring(0, 1) + "-" + cleanCedula.Substring(1);
                }
                else
                {
                    cleanCedula = "V-" + cleanCedula;
                }
            }
            if (await _context.Users.AnyAsync(u => u.Cedula != null && u.Cedula.ToLower() == cleanCedula.ToLower()))
            {
                return BadRequest(new { message = "Esta cédula de identidad ya se encuentra registrada con otra cuenta en El Pericón." });
            }

            // 4. Validación de Banco
            var cleanBank = dto.BankName?.Trim();
            if (string.IsNullOrWhiteSpace(cleanBank))
            {
                return BadRequest(new { message = "Debes seleccionar tu banco para Pago Móvil." });
            }

            // 5. Validación de Teléfono / WhatsApp (Único)
            string? rawPhone = !string.IsNullOrWhiteSpace(dto.PhoneNumber) ? dto.PhoneNumber.Trim() : (!string.IsNullOrWhiteSpace(dto.Phone) ? dto.Phone.Trim() : null);
            if (string.IsNullOrWhiteSpace(rawPhone))
            {
                return BadRequest(new { message = "El número de teléfono de Pago Móvil es requerido." });
            }
            var digitsOnly = System.Text.RegularExpressions.Regex.Replace(rawPhone, @"\D", "");
            if (digitsOnly.Length < 10 || digitsOnly.Length > 12)
            {
                return BadRequest(new { message = "Ingresa un número telefónico venezolano válido (ej: 04121234567)." });
            }
            if (await _context.Users.AnyAsync(u => u.PhoneNumber != null && (u.PhoneNumber == rawPhone || u.PhoneNumber == digitsOnly)))
            {
                return BadRequest(new { message = "Este número de teléfono ya se encuentra registrado con otra cuenta." });
            }

            // 6. Validación de Huella Digital del Dispositivo (Device Fingerprint)
            var cleanFingerprint = dto.DeviceFingerprint?.Trim();
            if (!string.IsNullOrWhiteSpace(cleanFingerprint))
            {
                bool deviceAlreadyRegistered = await _context.Users.AnyAsync(u => u.DeviceFingerprint == cleanFingerprint);
                if (deviceAlreadyRegistered)
                {
                    return BadRequest(new { message = "🚫 Este dispositivo ya tiene una cuenta registrada en El Pericón. No está permitido registrar múltiples cuentas desde el mismo teléfono o equipo." });
                }
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var user = new User
            {
                Username = cleanUsername,
                Email = cleanEmail,
                PhoneNumber = rawPhone,
                Cedula = cleanCedula,
                BankName = cleanBank,
                DeviceFingerprint = cleanFingerprint,
                BirthDate = parsedBirthDate,
                PasswordHash = passwordHash,
                Coins = 200,
                BonusCoins = 200,
                Level = "Peón de Casona",
                Experience = 0,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new AuthResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Cedula = user.Cedula,
                BankName = user.BankName,
                Coins = user.Coins,
                BonusCoins = user.BonusCoins,
                RetirableCoins = user.GetRetirableCoins(),
                Wins = 0,
                Losses = 0,
                WinRate = 0,
                Level = user.GetCalculatedLevel(),
                Experience = user.Experience,
                AvatarUrl = user.AvatarUrl,
                Message = "Usuario registrado exitosamente."
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var identifier = dto.Email.Trim().ToLowerInvariant();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == identifier || u.Username.ToLower() == identifier);

            if (user == null)
            {
                return Unauthorized(new { message = "Correo/usuario o contraseña incorrectos." });
            }

            if (!string.IsNullOrEmpty(user.PasswordHash) && user.PasswordHash.StartsWith("GOOGLE_OAUTH_"))
            {
                return Unauthorized(new { message = "Esta cuenta fue registrada con Google. Por favor, inicia sesión con el botón 'Continuar con Google'." });
            }

            bool isPasswordValid = false;
            try
            {
                isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            }
            catch
            {
                isPasswordValid = false;
            }

            if (!isPasswordValid)
            {
                return Unauthorized(new { message = "Correo/usuario o contraseña incorrectos." });
            }

            if (!user.IsActive)
            {
                return BadRequest(new { message = "🚫 Tu cuenta ha sido suspendida por la administración de El Pericón." });
            }

            var totalMatches = user.Wins + user.Losses;
            var winRate = totalMatches > 0 ? Math.Round((double)user.Wins / totalMatches * 100, 1) : 0;
            var calculatedLevel = user.GetCalculatedLevel();

            if (user.Level != calculatedLevel)
            {
                user.Level = calculatedLevel;
                await _context.SaveChangesAsync();
            }

            return Ok(new AuthResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Cedula = user.Cedula,
                BankName = user.BankName,
                Coins = user.Coins,
                BonusCoins = user.BonusCoins,
                RetirableCoins = user.GetRetirableCoins(),
                Wins = user.Wins,
                Losses = user.Losses,
                WinRate = winRate,
                Level = calculatedLevel,
                Experience = user.Experience,
                AvatarUrl = user.AvatarUrl,
                Message = "Inicio de sesión exitoso."
            });
        }

        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleAuthDto dto)
        {
            string email = string.Empty;
            string name = string.Empty;
            string? picture = dto.Picture;
            string? googleId = null;

            if (!string.IsNullOrWhiteSpace(dto.Credential))
            {
                try
                {
                    var payload = await GoogleJsonWebSignature.ValidateAsync(dto.Credential);
                    email = payload.Email;
                    name = payload.Name ?? payload.GivenName ?? payload.Email.Split('@')[0];
                    picture = payload.Picture ?? picture;
                    googleId = payload.Subject;
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = "Token de Google no válido: " + ex.Message });
                }
            }
            else if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                // Modo prueba / desarrollo
                email = dto.Email;
                name = !string.IsNullOrWhiteSpace(dto.Name) ? dto.Name : dto.Email.Split('@')[0];
            }
            else
            {
                return BadRequest(new { message = "No se proporcionaron credenciales de Google." });
            }

            var cleanEmail = email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail);

            if (user == null)
            {
                // Generar un username único
                var baseUsername = string.IsNullOrWhiteSpace(name) ? cleanEmail.Split('@')[0] : name.Trim();
                baseUsername = System.Text.RegularExpressions.Regex.Replace(baseUsername, @"[^a-zA-Z0-9_]", "");
                if (baseUsername.Length < 3) baseUsername = "GooglePlayer";
                if (baseUsername.Length > 30) baseUsername = baseUsername.Substring(0, 30);

                var finalUsername = baseUsername;
                int suffix = 1;
                while (await _context.Users.AnyAsync(u => u.Username.ToLower() == finalUsername.ToLower()))
                {
                    finalUsername = $"{baseUsername}{suffix}";
                    suffix++;
                }

                user = new User
                {
                    Username = finalUsername,
                    Email = cleanEmail,
                    PasswordHash = "GOOGLE_OAUTH_" + Guid.NewGuid().ToString(),
                    Coins = 200,
                    BonusCoins = 200,
                    Level = "Peón de Casona",
                    Experience = 0,
                    CreatedAt = DateTime.UtcNow,
                    GoogleId = googleId,
                    AvatarUrl = picture,
                    IsActive = true
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                if (!user.IsActive)
                {
                    return BadRequest(new { message = "La cuenta se encuentra inactiva." });
                }
                if (!string.IsNullOrEmpty(picture) && string.IsNullOrEmpty(user.AvatarUrl))
                {
                    user.AvatarUrl = picture;
                    await _context.SaveChangesAsync();
                }
                if (!string.IsNullOrEmpty(googleId) && string.IsNullOrEmpty(user.GoogleId))
                {
                    user.GoogleId = googleId;
                    await _context.SaveChangesAsync();
                }
            }

            var totalMatches = user.Wins + user.Losses;
            var winRate = totalMatches > 0 ? Math.Round((double)user.Wins / totalMatches * 100, 1) : 0;
            var calculatedLevel = user.GetCalculatedLevel();

            if (user.Level != calculatedLevel)
            {
                user.Level = calculatedLevel;
                await _context.SaveChangesAsync();
            }

            return Ok(new AuthResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Coins = user.Coins,
                BonusCoins = user.BonusCoins,
                RetirableCoins = user.GetRetirableCoins(),
                Wins = user.Wins,
                Losses = user.Losses,
                WinRate = winRate,
                Level = calculatedLevel,
                Experience = user.Experience,
                AvatarUrl = user.AvatarUrl,
                Message = "Autenticación con Google exitosa."
            });
        }

        [HttpGet("profile/{id}")]
        public async Task<IActionResult> GetProfile(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            var totalMatches = user.Wins + user.Losses;
            var winRate = totalMatches > 0 ? Math.Round((double)user.Wins / totalMatches * 100, 1) : 0;
            var calculatedLevel = user.GetCalculatedLevel();

            if (user.Level != calculatedLevel)
            {
                user.Level = calculatedLevel;
                await _context.SaveChangesAsync();
            }

            return Ok(new AuthResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Coins = user.Coins,
                BonusCoins = user.BonusCoins,
                RetirableCoins = user.GetRetirableCoins(),
                Wins = user.Wins,
                Losses = user.Losses,
                WinRate = winRate,
                Level = calculatedLevel,
                Experience = user.Experience,
                AvatarUrl = user.AvatarUrl,
                Message = "Perfil obtenido."
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var identifier = dto.Identifier.Trim().ToLowerInvariant();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == identifier || u.Username.ToLower() == identifier);

            if (user == null)
            {
                return NotFound(new { message = "No se encontró ningún usuario con ese correo o nombre de usuario." });
            }

            if (!string.IsNullOrEmpty(user.PasswordHash) && user.PasswordHash.StartsWith("GOOGLE_OAUTH_"))
            {
                return BadRequest(new { message = "Esta cuenta utiliza inicio de sesión con Google. Inicia sesión con el botón 'Continuar con Google'." });
            }

            static string CleanDigits(string? phone)
            {
                if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
                var digits = new string(phone.Where(char.IsDigit).ToArray());
                if (digits.StartsWith("58") && digits.Length > 10) digits = digits.Substring(2);
                if (digits.StartsWith("0") && digits.Length > 10) digits = digits.Substring(1);
                return digits;
            }

            var userPhoneClean = CleanDigits(user.PhoneNumber);
            var inputPhoneClean = CleanDigits(dto.PhoneNumber);

            if (string.IsNullOrEmpty(userPhoneClean) || string.IsNullOrEmpty(inputPhoneClean) || userPhoneClean != inputPhoneClean)
            {
                return BadRequest(new { message = "El número de WhatsApp no coincide con el registrado en esta cuenta." });
            }

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "La nueva contraseña debe tener al menos 6 caracteres." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "¡Contraseña restablecida exitosamente! Ya puedes iniciar sesión con tu nueva clave." });
        }
    }
}
