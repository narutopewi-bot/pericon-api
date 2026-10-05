using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PericonAPI.Data;
using PericonAPI.Models;

namespace PericonAPI.Controllers
{
    [ApiController]
    [Route("api/tournaments")]
    public class TournamentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TournamentController(AppDbContext context)
        {
            _context = context;
        }

        public class JoinTournamentDto
        {
            public int TournamentId { get; set; }
            public int UserId { get; set; }
            public string? Password { get; set; }
        }

        public class CreateTournamentDto
        {
            public string Name { get; set; } = "Torneo Piloto Casona";
            public bool IsPrivate { get; set; } = true;
            public string Password { get; set; } = "PERICON2026";
            public int BuyInCoins { get; set; } = 50;
            public int MaxParticipants { get; set; } = 8;
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveTournament()
        {
            var tournament = await _context.Tournaments
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync();

            if (tournament == null)
            {
                tournament = new Tournament
                {
                    Name = "Torneo Piloto Casona (Privado)",
                    IsPrivate = true,
                    Password = "PERICON2026",
                    BuyInCoins = 50,
                    MaxParticipants = 8,
                    CurrentParticipants = 0,
                    Status = "Inscripciones",
                    TotalPot = 400,
                    HouseCommission = 40,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Tournaments.Add(tournament);
                await _context.SaveChangesAsync();
            }

            var participants = await _context.TournamentParticipants
                .Where(p => p.TournamentId == tournament.Id)
                .OrderBy(p => p.SlotIndex)
                .ToListAsync();

            var matches = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournament.Id)
                .OrderBy(m => m.Round)
                .ThenBy(m => m.MatchIndex)
                .ToListAsync();

            return Ok(new
            {
                tournament = new
                {
                    id = tournament.Id,
                    name = tournament.Name,
                    isPrivate = tournament.IsPrivate,
                    buyInCoins = tournament.BuyInCoins,
                    maxParticipants = tournament.MaxParticipants,
                    currentParticipants = participants.Count,
                    status = tournament.Status,
                    totalPot = tournament.TotalPot,
                    houseCommission = tournament.HouseCommission,
                    winnerUsername = tournament.WinnerUsername,
                    runnerUpUsername = tournament.RunnerUpUsername,
                    startedAt = tournament.StartedAt,
                    finishedAt = tournament.FinishedAt
                },
                participants = participants.Select(p => new
                {
                    id = p.Id,
                    userId = p.UserId,
                    username = p.Username,
                    avatarUrl = p.AvatarUrl,
                    slotIndex = p.SlotIndex,
                    isEliminated = p.IsEliminated,
                    eliminatedInRound = p.EliminatedInRound
                }),
                matches = matches.Select(m => new
                {
                    id = m.Id,
                    round = m.Round,
                    matchIndex = m.MatchIndex,
                    playerOneId = m.PlayerOneId,
                    playerOneUsername = m.PlayerOneUsername,
                    playerOneScore = m.PlayerOneScore,
                    playerTwoId = m.PlayerTwoId,
                    playerTwoUsername = m.PlayerTwoUsername,
                    playerTwoScore = m.PlayerTwoScore,
                    winnerId = m.WinnerId,
                    winnerUsername = m.WinnerUsername,
                    status = m.Status,
                    gameId = m.GameId
                })
            });
        }

        [HttpPost("join")]
        public async Task<IActionResult> JoinTournament([FromBody] JoinTournamentDto dto)
        {
            if (dto == null || dto.UserId <= 0)
            {
                return BadRequest(new { message = "Datos de inscripción inválidos." });
            }

            var tournament = await _context.Tournaments.FindAsync(dto.TournamentId);
            if (tournament == null)
            {
                return NotFound(new { message = "Torneo no encontrado." });
            }

            if (tournament.Status != "Inscripciones")
            {
                return BadRequest(new { message = "Las inscripciones para este torneo ya están cerradas." });
            }

            if (tournament.IsPrivate)
            {
                string inputPass = (dto.Password ?? "").Trim();
                if (!string.Equals(inputPass, tournament.Password, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "Contraseña de torneo incorrecta. Solicítala al organizador." });
                }
            }

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            var existingParticipant = await _context.TournamentParticipants
                .FirstOrDefaultAsync(p => p.TournamentId == tournament.Id && p.UserId == user.Id);

            if (existingParticipant != null)
            {
                return BadRequest(new { message = "Ya estás inscrito en este torneo." });
            }

            var currentCount = await _context.TournamentParticipants
                .CountAsync(p => p.TournamentId == tournament.Id);

            if (currentCount >= tournament.MaxParticipants)
            {
                return BadRequest(new { message = "El torneo ya ha alcanzado el cupo máximo de 8 jugadores." });
            }

            if (user.Coins < tournament.BuyInCoins)
            {
                return BadRequest(new { message = $"Saldo insuficiente. La entrada requiere {tournament.BuyInCoins} monedas y dispones de {user.Coins}." });
            }

            user.Coins -= tournament.BuyInCoins;

            var participant = new TournamentParticipant
            {
                TournamentId = tournament.Id,
                UserId = user.Id,
                Username = user.Username,
                AvatarUrl = user.AvatarUrl ?? "/avatar.png",
                SlotIndex = currentCount,
                JoinedAt = DateTime.UtcNow
            };

            _context.TournamentParticipants.Add(participant);
            tournament.CurrentParticipants = currentCount + 1;
            tournament.TotalPot = tournament.CurrentParticipants * tournament.BuyInCoins;
            tournament.HouseCommission = (int)Math.Round(tournament.TotalPot * 0.10);

            // Si se completa el cupo de 8 jugadores, inicializar el bracket
            if (tournament.CurrentParticipants == tournament.MaxParticipants)
            {
                tournament.Status = "Cuartos";
                tournament.StartedAt = DateTime.UtcNow;

                var allParticipants = await _context.TournamentParticipants
                    .Where(p => p.TournamentId == tournament.Id)
                    .OrderBy(p => p.SlotIndex)
                    .ToListAsync();
                allParticipants.Add(participant);

                // Cuartos de Final (4 partidas)
                for (int i = 0; i < 4; i++)
                {
                    var p1 = allParticipants[i * 2];
                    var p2 = allParticipants[i * 2 + 1];

                    _context.TournamentMatches.Add(new TournamentMatch
                    {
                        TournamentId = tournament.Id,
                        Round = "Cuartos",
                        MatchIndex = i,
                        PlayerOneId = p1.UserId,
                        PlayerOneUsername = p1.Username,
                        PlayerTwoId = p2.UserId,
                        PlayerTwoUsername = p2.Username,
                        Status = "EnJuego",
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                // Semifinales (2 partidas vacías esperando ganadores)
                for (int i = 0; i < 2; i++)
                {
                    _context.TournamentMatches.Add(new TournamentMatch
                    {
                        TournamentId = tournament.Id,
                        Round = "Semifinal",
                        MatchIndex = i,
                        Status = "Pendiente",
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                // Gran Final (1 partida vacía)
                _context.TournamentMatches.Add(new TournamentMatch
                {
                    TournamentId = tournament.Id,
                    Round = "Final",
                    MatchIndex = 0,
                    Status = "Pendiente",
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "¡Inscripción exitosa al torneo!",
                slotIndex = participant.SlotIndex,
                currentParticipants = tournament.CurrentParticipants,
                status = tournament.Status,
                userCoins = user.Coins
            });
        }

        [HttpPost("reset-pilot")]
        public async Task<IActionResult> ResetPilotTournament([FromQuery] string? key)
        {
            if (key != "PERICON2026")
            {
                return Unauthorized(new { message = "Clave de reinicio inválida." });
            }

            var oldTournaments = await _context.Tournaments.ToListAsync();
            var oldParticipants = await _context.TournamentParticipants.ToListAsync();
            var oldMatches = await _context.TournamentMatches.ToListAsync();

            _context.TournamentParticipants.RemoveRange(oldParticipants);
            _context.TournamentMatches.RemoveRange(oldMatches);
            _context.Tournaments.RemoveRange(oldTournaments);

            var newTournament = new Tournament
            {
                Name = "Torneo Piloto Casona (Privado)",
                IsPrivate = true,
                Password = "PERICON2026",
                BuyInCoins = 50,
                MaxParticipants = 8,
                CurrentParticipants = 0,
                Status = "Inscripciones",
                TotalPot = 400,
                HouseCommission = 40,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tournaments.Add(newTournament);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Torneo piloto reiniciado exitosamente para nuevas pruebas.", tournamentId = newTournament.Id });
        }
    }
}
