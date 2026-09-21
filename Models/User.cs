using System;
using System.ComponentModel.DataAnnotations;

namespace PericonAPI.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public int Coins { get; set; } = 300;

        public int Wins { get; set; } = 0;

        public int Losses { get; set; } = 0;

        public string Level { get; set; } = "Peón de Casona";

        public int Experience { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? GoogleId { get; set; }

        public string? AvatarUrl { get; set; }

        [MaxLength(30)]
        public string? PhoneNumber { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsAdmin { get; set; } = false;

        public DateTime? LastDailyClaim { get; set; }

        public bool HasClaimedInstagramReward { get; set; } = false;

        [MaxLength(50)]
        public string? InstagramHandle { get; set; }

        public string GetCalculatedLevel()
        {
            if (Wins <= 10) return "Peón de Casona";
            if (Wins <= 30) return "Arriero de Chivos";
            if (Wins <= 60) return "Catador de Cocuy";
            if (Wins <= 100) return "Tocador de Cuatro";
            if (Wins <= 200) return "Patrón de Hacienda";
            return "Leyenda de Carora";
        }
    }
}
