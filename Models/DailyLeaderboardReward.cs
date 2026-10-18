using System;
using System.ComponentModel.DataAnnotations;

namespace PericonAPI.Models
{
    public class DailyLeaderboardReward
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        public int Rank { get; set; } // 1, 2, 3

        public int CoinsAwarded { get; set; } // 500, 300, 200

        public int WinsCount { get; set; }

        [Required]
        [MaxLength(20)]
        public string RewardDate { get; set; } = string.Empty; // YYYY-MM-DD

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
