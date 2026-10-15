using System;
using System.ComponentModel.DataAnnotations;

namespace PericonAPI.Models
{
    public class EmailVerificationCode
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Code { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);

        public bool IsUsed { get; set; } = false;
    }

    public class SendEmailVerificationCodeDto
    {
        [Required(ErrorMessage = "El correo es requerido")]
        [EmailAddress(ErrorMessage = "El correo no es válido")]
        public string Email { get; set; } = string.Empty;

        public string? Username { get; set; }
    }
}
