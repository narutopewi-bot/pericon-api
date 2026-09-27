using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PericonAPI.Data;
using PericonAPI.Models;

namespace PericonAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedbackController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FeedbackController(AppDbContext context)
        {
            _context = context;
        }

        public class CreateFeedbackDto
        {
            public int? UserId { get; set; }
            public string? Username { get; set; }
            public int Rating { get; set; } = 5;
            public string? Category { get; set; } = "General";
            public string Message { get; set; } = string.Empty;
            public bool CanPublish { get; set; } = true;
        }

        /// <summary>
        /// Enviar una opinión, sugerencia o testimonio del jugador
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SubmitFeedback([FromBody] CreateFeedbackDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
            {
                return BadRequest(new { success = false, message = "Por favor escribe tu opinión o sugerencia antes de enviar." });
            }

            if (dto.Rating < 1 || dto.Rating > 5)
            {
                return BadRequest(new { success = false, message = "La calificación debe ser entre 1 y 5 estrellas." });
            }

            int? userId = dto.UserId;
            string username = dto.Username?.Trim() ?? "Jugador";
            string? email = null;
            string? phone = null;

            // Si el usuario envió su ID o podemos detectarlo del token
            if (userId.HasValue && userId.Value > 0)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
                if (user != null)
                {
                    username = user.Username;
                    email = user.Email;
                    phone = user.PhoneNumber;
                }
            }
            else if (!string.IsNullOrWhiteSpace(username) && username != "Jugador")
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
                if (user != null)
                {
                    userId = user.Id;
                    email = user.Email;
                    phone = user.PhoneNumber;
                }
            }

            var feedback = new PlayerFeedback
            {
                UserId = userId,
                Username = string.IsNullOrWhiteSpace(username) ? "Jugador Anónimo" : username,
                UserEmail = email,
                UserPhone = phone,
                Rating = dto.Rating,
                Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category.Trim(),
                Message = dto.Message.Trim(),
                CanPublish = dto.CanPublish,
                IsFeatured = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.PlayerFeedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "¡Muchas gracias por tus comentarios! Tu opinión nos ayuda a hacer crecer y mejorar El Pericón cada día.",
                id = feedback.Id
            });
        }

        /// <summary>
        /// Obtener testimonios aprobados y destacados para mostrar en la web
        /// </summary>
        [HttpGet("testimonials")]
        public async Task<IActionResult> GetPublicTestimonials()
        {
            var testimonials = await _context.PlayerFeedbacks
                .Where(f => (f.IsFeatured || (f.CanPublish && f.Rating >= 4)) && !string.IsNullOrWhiteSpace(f.Message))
                .OrderByDescending(f => f.IsFeatured)
                .ThenByDescending(f => f.CreatedAt)
                .Take(12)
                .Select(f => new
                {
                    id = f.Id,
                    username = f.Username,
                    rating = f.Rating,
                    category = f.Category,
                    message = f.Message,
                    createdAt = f.CreatedAt.ToString("yyyy-MM-dd")
                })
                .ToListAsync();

            var totalReviews = await _context.PlayerFeedbacks.CountAsync();
            var avgRating = totalReviews > 0 ? await _context.PlayerFeedbacks.AverageAsync(f => f.Rating) : 5.0;

            return Ok(new
            {
                success = true,
                totalReviews,
                averageRating = Math.Round(avgRating, 1),
                testimonials
            });
        }
    }
}
