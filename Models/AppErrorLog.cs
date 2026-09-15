using System;

namespace PericonAPI.Models
{
    public class AppErrorLog
    {
        public int Id { get; set; }
        public string Source { get; set; } = "Client"; // "Client", "SignalR", "Server", "Game2v2", "Game1v1"
        public string? RoomName { get; set; }
        public string? Username { get; set; }
        public int? UserId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string? ExtraData { get; set; } // JSON o metadatos (e.g. estado de juego, turno, etc.)
        public string Status { get; set; } = "NUEVO"; // "NUEVO", "REVISADO", "RESUELTO"
        public string? AdminNotes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
    }

    public class CreateErrorLogDto
    {
        public string Source { get; set; } = "Client";
        public string? RoomName { get; set; }
        public string? Username { get; set; }
        public int? UserId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string? ExtraData { get; set; }
    }

    public class UpdateErrorStatusDto
    {
        public string Status { get; set; } = "REVISADO"; // "REVISADO" o "RESUELTO"
        public string? AdminNotes { get; set; }
    }
}
