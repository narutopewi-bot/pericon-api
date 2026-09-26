using System;

namespace PericonAPI.Models
{
    public class BotMatchRecord
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string BotName { get; set; } = "Pericón (Bot IA)";
        public int BetAmount { get; set; }
        public bool UserWon { get; set; }
        public int CoinsWon { get; set; }        // Monedas ganadas por el usuario (+bet si ganó, 0 si perdió)
        public int CoinsLost { get; set; }       // Monedas perdidas por el usuario (0 si ganó, bet si perdió)
        public int HouseProfit { get; set; }     // Impacto neto en la casa (+CoinsLost cuando bot gana, -CoinsWon cuando usuario gana)
        public int UserCoinsBefore { get; set; }
        public int UserCoinsAfter { get; set; }
        public string EndReason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
