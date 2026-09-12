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

        [HttpGet("{id}/profile")]
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
                createdAt = user.CreatedAt
            });
        }

        [HttpPost("record-match")]
        public async Task<IActionResult> RecordMatch([FromBody] RecordMatchDto dto)
        {
            var user = await _context.Users.FindAsync(dto.UserId);
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
            var topPlayers = await _context.Users
                .Where(u => u.IsActive && u.Username != "Guardian")
                .OrderByDescending(u => u.Wins)
                .ThenByDescending(u => u.Coins)
                .Take(10)
                .Select(u => new
                {
                    id = u.Id,
                    username = u.Username,
                    wins = u.Wins,
                    losses = u.Losses,
                    coins = u.Coins,
                    level = u.GetCalculatedLevel(),
                    avatarUrl = u.AvatarUrl,
                    winRate = (u.Wins + u.Losses) > 0 ? Math.Round((double)u.Wins / (u.Wins + u.Losses) * 100, 1) : 0
                })
                .ToListAsync();

            return Ok(topPlayers);
        }
    }

    public class RedeemPromoDto
    {
        public int UserId { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    public class RecordMatchDto
    {
        public int UserId { get; set; }
        public bool Won { get; set; }
        public int CoinsChange { get; set; } = 0;
    }

    public class ClaimDailyDto
    {
        public int UserId { get; set; }
    }
}
