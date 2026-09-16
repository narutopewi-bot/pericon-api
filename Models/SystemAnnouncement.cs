using System;
using System.ComponentModel.DataAnnotations;

namespace PericonAPI.Models
{
    public class SystemAnnouncement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Type { get; set; } = "info"; // "tournament", "info", "alert", "bonus"

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? CreatedBy { get; set; } = "Guardian";
    }
}
