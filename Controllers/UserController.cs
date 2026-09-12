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
