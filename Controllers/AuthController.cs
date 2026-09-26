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

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == cleanEmail))
            {
                return BadRequest(new { message = "El correo ya se encuentra registrado." });
            }

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == cleanUsername.ToLower()))
            {
                return BadRequest(new { message = "El nombre de usuario ya está en uso." });
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            string? rawPhone = !string.IsNullOrWhiteSpace(dto.PhoneNumber) ? dto.PhoneNumber.Trim() : (!string.IsNullOrWhiteSpace(dto.Phone) ? dto.Phone.Trim() : null);

            var user = new User
            {
                Username = cleanUsername,
                Email = cleanEmail,
                PhoneNumber = rawPhone,
                PasswordHash = passwordHash,
                Coins = 500,
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
                Coins = user.Coins,
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
                Coins = user.Coins,
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
                    Coins = 500,
                    Level = "Aprendiz",
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
