using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PericonAPI.Models
{
    [Table("PlayerFeedbacks")]
    public class PlayerFeedback
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int? UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = "Jugador";

        [MaxLength(150)]
        public string? UserEmail { get; set; }

        [MaxLength(30)]
        public string? UserPhone { get; set; }

        /// <summary>
        /// Calificación de 1 a 5 estrellas
        /// </summary>
        [Range(1, 5)]
        public int Rating { get; set; } = 5;

        /// <summary>
        /// Categoría: "General", "Sugerencia", "Experiencia", "Juego", "Recargas"
        /// </summary>
        [MaxLength(50)]
        public string Category { get; set; } = "General";

        [Required]
        [MaxLength(2000)]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Si el usuario autoriza a que su reseña sea pública en la plataforma
        /// </summary>
        public bool CanPublish { get; set; } = true;

        /// <summary>
        /// Destacado por el administrador para mostrarse como testimonio
        /// </summary>
        public bool IsFeatured { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
