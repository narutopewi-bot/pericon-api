using System;
using System.ComponentModel.DataAnnotations;

namespace PericonAPI.Models
{
    public class Tournament
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "Torneo Casona";

        public bool IsPrivate { get; set; } = true;

        [MaxLength(50)]
        public string Password { get; set; } = "PERICON2026";

        public int BuyInCoins { get; set; } = 50;

        public int MaxParticipants { get; set; } = 8;

        public int CurrentParticipants { get; set; } = 0;

        [MaxLength(30)]
        public string Status { get; set; } = "Inscripciones"; // Inscripciones, Cuartos, Semifinal, Final, Finalizado

        public int TotalPot { get; set; } = 0;

        public int HouseCommission { get; set; } = 0;

        public int? WinnerUserId { get; set; }

        [MaxLength(50)]
        public string? WinnerUsername { get; set; }

        public int? RunnerUpUserId { get; set; }

        [MaxLength(50)]
        public string? RunnerUpUsername { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? StartedAt { get; set; }

        public DateTime? FinishedAt { get; set; }
    }

    public class TournamentParticipant
    {
        [Key]
        public int Id { get; set; }

        public int TournamentId { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public int SlotIndex { get; set; } // 0..7

        public bool IsEliminated { get; set; } = false;

        [MaxLength(30)]
        public string? EliminatedInRound { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }

    public class TournamentMatch
    {
        [Key]
        public int Id { get; set; }

        public int TournamentId { get; set; }

        [Required]
        [MaxLength(30)]
        public string Round { get; set; } = "Cuartos"; // Cuartos, Semifinal, Final

        public int MatchIndex { get; set; } // Cuartos: 0..3, Semis: 0..1, Final: 0

        public int? PlayerOneId { get; set; }

        [MaxLength(50)]
        public string? PlayerOneUsername { get; set; }

        public int PlayerOneScore { get; set; } = 0;

        public int? PlayerTwoId { get; set; }

        [MaxLength(50)]
        public string? PlayerTwoUsername { get; set; }

        public int PlayerTwoScore { get; set; } = 0;

        public int? WinnerId { get; set; }

        [MaxLength(50)]
        public string? WinnerUsername { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = "Pendiente"; // Pendiente, EnJuego, Finalizada

        public int? GameId { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
