using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PericonAPI.Data;
using PericonAPI.Models;

namespace PericonAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("{identifier}/profile")]
        public async Task<IActionResult> GetProfile(string identifier, [FromQuery] string? email = null, [FromQuery] string? username = null)
        {
            User? user = null;
            if (int.TryParse(identifier, out int id) && id > 0)
            {
                user = await _context.Users.FindAsync(id);
            }

            if (user == null && !string.IsNullOrWhiteSpace(identifier))
            {
                var clean = identifier.Trim().ToLowerInvariant();
                user = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == clean ||
                    u.Username.ToLower() == clean);
            }

            if (user == null && !string.IsNullOrWhiteSpace(email))
            {
                var cleanEmail = email.Trim().ToLowerInvariant();
                user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail);
            }

            if (user == null && !string.IsNullOrWhiteSpace(username))
            {
                var cleanUsername = username.Trim().ToLowerInvariant();
                user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername);
            }

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

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                coins = user.Coins,
                wins = user.Wins,
                losses = user.Losses,
                totalMatches,
                winRate,
                level = calculatedLevel,
                experience = user.Experience,
                avatarUrl = user.AvatarUrl,
                hasClaimedInstagramReward = user.HasClaimedInstagramReward,
                instagramHandle = user.InstagramHandle,
                createdAt = user.CreatedAt
            });
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfileByQuery([FromQuery] string? id = null, [FromQuery] string? email = null, [FromQuery] string? username = null)
        {
            string identifier = id ?? email ?? username ?? "";
            return await GetProfile(identifier, email, username);
        }

        [HttpPost("record-match")]
        public async Task<IActionResult> RecordMatch([FromBody] RecordMatchDto dto)
        {
            User? user = null;
            if (dto.UserId > 0)
            {
                user = await _context.Users.FindAsync(dto.UserId);
            }

            if (user == null && !string.IsNullOrWhiteSpace(dto.Email))
            {
                var cleanEmail = dto.Email.Trim().ToLowerInvariant();
                user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail);
            }

            if (user == null && !string.IsNullOrWhiteSpace(dto.Username))
            {
                var cleanUsername = dto.Username.Trim().ToLowerInvariant();
                user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername);
            }

            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            if (dto.Won)
            {
                user.Wins += 1;
            }
            else
            {
                user.Losses += 1;
            }

            user.Coins = Math.Max(0, user.Coins + dto.CoinsChange);
            user.Level = user.GetCalculatedLevel();

            await _context.SaveChangesAsync();

            var totalMatches = user.Wins + user.Losses;
            var winRate = totalMatches > 0 ? Math.Round((double)user.Wins / totalMatches * 100, 1) : 0;

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                coins = user.Coins,
                wins = user.Wins,
                losses = user.Losses,
                totalMatches,
                winRate,
                level = user.Level,
                message = dto.Won ? "¡Victoria registrada con éxito!" : "Partida registrada."
            });
        }

        [HttpPost("claim-daily")]
        public async Task<IActionResult> ClaimDaily([FromBody] ClaimDailyDto dto)
        {
            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            if (user.LastDailyClaim.HasValue && user.LastDailyClaim.Value.AddHours(20) > DateTime.UtcNow)
            {
                var nextClaimTime = user.LastDailyClaim.Value.AddHours(24);
                var diff = nextClaimTime - DateTime.UtcNow;
                var hoursLeft = Math.Max(1, (int)diff.TotalHours);
                return BadRequest(new { message = $"Ya reclamaste tu bono diario de hoy. Podrás reclamar 10 monedas nuevamente en {hoursLeft} horas." });
            }

            const int dailyBonus = 10;
            user.Coins += dailyBonus;
            user.LastDailyClaim = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = user.Id,
                coins = user.Coins,
                bonus = dailyBonus,
                message = "¡Has recibido tus 10 monedas diarias de cortesía del Chivo! 🐐💰"
            });
        }

        [HttpPost("redeem-promo")]
        public async Task<IActionResult> RedeemPromo([FromBody] RedeemPromoDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Code) || dto.UserId <= 0)
            {
                return BadRequest(new { message = "Datos de cupón inválidos." });
            }

            var cleanCode = dto.Code.Trim().ToUpperInvariant();
            var promo = await _context.PromoCodes.FirstOrDefaultAsync(p => p.Code == cleanCode);

            if (promo == null)
            {
                return NotFound(new { message = "El código promocional no existe o es incorrecto." });
            }

            if (!promo.IsActive)
            {
                return BadRequest(new { message = "Este código promocional ha sido desactivado." });
            }

            if (promo.ExpiresAt.HasValue && promo.ExpiresAt.Value < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Este código promocional ha expirado." });
            }

            if (promo.TimesUsed >= promo.MaxUses)
            {
                return BadRequest(new { message = "Este código promocional ha alcanzado el límite máximo de canjes." });
            }

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            // Verificar si el usuario ya canjeó este cupón
            var alreadyRedeemed = await _context.PromoCodeRedemptions
                .AnyAsync(r => r.PromoCodeId == promo.Id && r.UserId == user.Id);

            if (alreadyRedeemed)
            {
                return BadRequest(new { message = $"Ya has canjeado el código '{promo.Code}' anteriormente." });
            }

            // Otorgar monedas
            user.Coins += promo.CoinsReward;
            promo.TimesUsed += 1;

            var redemption = new PromoCodeRedemption
            {
                PromoCodeId = promo.Id,
                UserId = user.Id,
                CoinsAwarded = promo.CoinsReward,
                RedeemedAt = DateTime.UtcNow
            };

            _context.PromoCodeRedemptions.Add(redemption);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                newBalance = user.Coins,
                coinsAwarded = promo.CoinsReward,
                code = promo.Code,
                message = $"🎉 ¡Código canjeado con éxito! Has recibido +{promo.CoinsReward} monedas. Tu nuevo saldo es de {user.Coins.ToString("N0")} monedas."
            });
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard()
        {
            var topPlayersList = await _context.Users
                .Where(u => u.IsActive && u.Username != "Guardian")
                .OrderByDescending(u => u.Wins)
                .ThenByDescending(u => u.Coins)
                .Take(10)
                .ToListAsync();

            var topPlayers = topPlayersList.Select(u =>
            {
                var total = u.Wins + u.Losses;
                var rate = total > 0 ? Math.Round((double)u.Wins / total * 100, 1) : 0;
                return new
                {
                    id = u.Id,
                    username = u.Username,
                    wins = u.Wins,
                    losses = u.Losses,
                    coins = u.Coins,
                    level = u.GetCalculatedLevel(),
                    avatarUrl = u.AvatarUrl,
                    winRate = rate
                };
            }).ToList();

            return Ok(topPlayers);
        }

        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            if (dto == null || dto.UserId <= 0)
            {
                return BadRequest(new { message = "ID de usuario inválido." });
            }

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            // 1. Validar y actualizar Nombre de Usuario
            if (!string.IsNullOrWhiteSpace(dto.NewUsername))
            {
                var cleanUsername = dto.NewUsername.Trim();
                if (cleanUsername.Length < 3 || cleanUsername.Length > 25)
                {
                    return BadRequest(new { message = "El nombre de usuario debe tener entre 3 y 25 caracteres." });
                }

                if (!string.Equals(user.Username, cleanUsername, StringComparison.OrdinalIgnoreCase))
                {
                    var exists = await _context.Users
                        .AnyAsync(u => u.Id != user.Id && u.Username.ToLower() == cleanUsername.ToLower());
                    if (exists)
                    {
                        return BadRequest(new { message = $"El nombre '{cleanUsername}' ya pertenece a otro jugador. Por favor elige otro." });
                    }
                    user.Username = cleanUsername;
                }
            }

            // 2. Validar y actualizar Logotipo / Avatar
            if (dto.NewAvatarUrl != null)
            {
                user.AvatarUrl = dto.NewAvatarUrl.Trim();
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "¡Perfil actualizado con éxito!",
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    coins = user.Coins,
                    wins = user.Wins,
                    losses = user.Losses,
                    level = user.GetCalculatedLevel(),
                    avatarUrl = user.AvatarUrl
                }
            });
        }

        [HttpPost("claim-instagram-reward")]
        public async Task<IActionResult> ClaimInstagramReward([FromBody] ClaimInstagramRewardDto dto)
        {
            if (dto == null || dto.UserId <= 0)
            {
                return BadRequest(new { message = "Datos de usuario inválidos." });
            }

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            if (user.HasClaimedInstagramReward)
            {
                return BadRequest(new { message = "Ya has reclamado tu recompensa de 300 monedas de Instagram anteriormente." });
            }

            const int instagramReward = 300;
            user.Coins += instagramReward;
            user.HasClaimedInstagramReward = true;
            if (!string.IsNullOrWhiteSpace(dto.InstagramHandle))
            {
                var cleanHandle = dto.InstagramHandle.Trim();
                if (!cleanHandle.StartsWith("@")) cleanHandle = "@" + cleanHandle;
                user.InstagramHandle = cleanHandle;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = user.Id,
                coins = user.Coins,
                bonus = instagramReward,
                instagramHandle = user.InstagramHandle,
                message = "¡Recompensa activada con éxito! Te hemos acreditado 300 monedas por apoyar a @pericon.lat en Instagram. 🐐📸🪙"
            });
        }
    }

    public class ClaimInstagramRewardDto
    {
        public int UserId { get; set; }
        public string? InstagramHandle { get; set; }
    }

    public class UpdateProfileDto
    {
        public int UserId { get; set; }
        public string? NewUsername { get; set; }
        public string? NewAvatarUrl { get; set; }
    }

    public class RedeemPromoDto
    {
        public int UserId { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    public class RecordMatchDto
    {
        public int UserId { get; set; }
        public string? Email { get; set; }
        public string? Username { get; set; }
        public bool Won { get; set; }
        public int CoinsChange { get; set; } = 0;
    }

    public class ClaimDailyDto
    {
        public int UserId { get; set; }
    }
}
