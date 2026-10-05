using System;
using System.ComponentModel.DataAnnotations;

namespace PericonAPI.Models
{
    public class ReferralRecord
    {
        [Key]
        public int Id { get; set; }

        public int ReferrerUserId { get; set; }

        public int ReferredUserId { get; set; }

        [Required]
        [MaxLength(30)]
        public string ReferralCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ReferredUsername { get; set; } = string.Empty;

        public int CoinsAwardedReferred { get; set; } = 100; // 100 BonusCoins otorgados de entrada

        public int CoinsAwardedReferrer { get; set; } = 150; // 150 Monedas liberadas al calificar

        [MaxLength(30)]
        public string Status { get; set; } = "Pendiente"; // "Pendiente", "Calificado"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? QualifiedAt { get; set; }
    }
}
