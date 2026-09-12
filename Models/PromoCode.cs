using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PericonAPI.Models
{
    public class PromoCode
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required]
        public int CoinsReward { get; set; } = 200;

        public int MaxUses { get; set; } = 1000;

        public int TimesUsed { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }
    }

    public class PromoCodeRedemption
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PromoCodeId { get; set; }

        [Required]
        public int UserId { get; set; }

        public int CoinsAwarded { get; set; }

        public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("PromoCodeId")]
        public PromoCode? PromoCode { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}
