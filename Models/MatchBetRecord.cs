using System;

namespace PericonAPI.Models
{
    public class MatchBetRecord
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public string PlayerOneName { get; set; } = string.Empty;
        public string PlayerTwoName { get; set; } = string.Empty;
        public int BetPerPlayer { get; set; }
        public int TotalPot { get; set; }
        public int HouseCommission { get; set; } // 20% retenido por el administrador
        public int WinnerPrize { get; set; }    // 80% entregado al ganador
        public string WinnerUsername { get; set; } = string.Empty;
        public string LoserUsername { get; set; } = string.Empty;
        public string EndReason { get; set; } = string.Empty; // "VictoriaPorPuntos", "Rendicion", "TiempoAgotado"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
