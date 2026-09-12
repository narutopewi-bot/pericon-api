using System;
using System.Collections.Generic;
using System.Linq;
using PericonAPI.Hubs;

namespace PericonAPI.Models
{
    public class GamePlayTwoVsTwo
    {
        public int Id { get; set; }
        public SpanishCards Deck { get; set; }

        // Identificadores de los 4 Jugadores
        // Equipo 1: Player 1 (Tú) y Player 3 (Compañero)
        // Equipo 2: Player 2 (Rival 1) y Player 4 (Rival 2)
        public string IdP1 { get; set; } = string.Empty;
        public string IdP2 { get; set; } = string.Empty;
        public string IdP3 { get; set; } = string.Empty;
        public string IdP4 { get; set; } = string.Empty;

        public string Name1 { get; set; } = "Jugador 1";
        public string Name2 { get; set; } = "Rival 1";
        public string Name3 { get; set; } = "Compañero";
        public string Name4 { get; set; } = "Rival 2";

        public bool IsBot1 { get; set; } = false;
        public bool IsBot2 { get; set; } = false;
        public bool IsBot3 { get; set; } = false;
        public bool IsBot4 { get; set; } = false;

        public bool IsActive { get; set; } = true;

        // Manos de cada uno de los 4 jugadores
        public List<Card> Cards1 { get; set; } = new List<Card>();
        public List<Card> Cards2 { get; set; } = new List<Card>();
        public List<Card> Cards3 { get; set; } = new List<Card>();
        public List<Card> Cards4 { get; set; } = new List<Card>();

        public Card Life { get; set; } = new Card();

        // Puntuación de piedras por EQUIPO
        public int PointsTeam1 { get; set; } = 0;
        public int PointsTeam2 { get; set; } = 0;

        // Bazas ganadas en la mano actual (2 de 3)
        public int TricksTeam1 { get; set; } = 0;
        public int TricksTeam2 { get; set; } = 0;

        // Baza actual: Lista de cartas tiradas en la mesa en orden (PlayerIndex 0..3, Card)
        public List<(int playerIndex, Card card)> CurrentTrick { get; set; } = new List<(int, Card)>();

        // Turno actual (0 = P1, 1 = P2, 2 = P3, 3 = P4)
        public int CurrentTurn { get; set; } = 0;
        public int LeadPlayer { get; set; } = 0; // Quien abrió la baza actual
        public int HandDealer { get; set; } = 0; // Quien repartió en la mano actual

        // Apuestas / Cantes
        public int CurrentStake { get; set; } = 1;
        public int Ask369 { get; set; } = 0;
        public int LastAskedByTeam { get; set; } = 0; // 1 o 2

        // Tumba
        public bool IsTumbaTeam1 { get; set; } = false;
        public bool IsTumbaTeam2 { get; set; } = false;
        public bool IsTumbaDeParaAtrasTeam1 { get; set; } = false;
        public bool IsTumbaDeParaAtrasTeam2 { get; set; } = false;

        public string InitHand { get; set; } = string.Empty;
        public int Coins { get; set; } = 100;

        public GamePlayTwoVsTwo()
        {
            Deck = new SpanishCards();
        }

        public GamePlayTwoVsTwo(string p1, string p2, string p3, string p4) : this()
        {
            IdP1 = p1;
            IdP2 = p2;
            IdP3 = p3;
            IdP4 = p4;
        }

        public void UpdateTumbaStatus(int oldP1 = -1, int oldP2 = -1)
        {
            // Regla idéntica al 1 vs 1:
            // Tumba de para atrás decreciente: solo si venía de >= 9 y cae a 8 puntos
            if (oldP1 != -1)
            {
                if (oldP1 >= 9 && PointsTeam1 == 8)
                {
                    IsTumbaDeParaAtrasTeam1 = true;
                }
                else if (PointsTeam1 != 8)
                {
                    IsTumbaDeParaAtrasTeam1 = false;
                }
            }

            if (PointsTeam1 >= 9)
            {
                IsTumbaTeam1 = true;
            }
            else if (IsTumbaDeParaAtrasTeam1 && PointsTeam1 == 8)
            {
                IsTumbaTeam1 = true;
            }
            else
            {
                IsTumbaTeam1 = false;
                IsTumbaDeParaAtrasTeam1 = false;
            }

            if (oldP2 != -1)
            {
                if (oldP2 >= 9 && PointsTeam2 == 8)
                {
                    IsTumbaDeParaAtrasTeam2 = true;
                }
                else if (PointsTeam2 != 8)
                {
                    IsTumbaDeParaAtrasTeam2 = false;
                }
            }

            if (PointsTeam2 >= 9)
            {
                IsTumbaTeam2 = true;
            }
            else if (IsTumbaDeParaAtrasTeam2 && PointsTeam2 == 8)
            {
                IsTumbaTeam2 = true;
            }
            else
            {
                IsTumbaTeam2 = false;
                IsTumbaDeParaAtrasTeam2 = false;
            }
        }

        public void ShuffleCards_2vs2()
        {
            Cards1.Clear();
            Cards2.Clear();
            Cards3.Clear();
            Cards4.Clear();
            CurrentTrick.Clear();
            Deck.Reload();
            CurrentStake = 1;
            Ask369 = 0;
            LastAskedByTeam = 0;
            TricksTeam1 = 0;
            TricksTeam2 = 0;
            UpdateTumbaStatus();

            // Repartir 3 cartas a cada uno de los 4 jugadores (12 cartas en total)
            for (int r = 0; r < 3; r++)
            {
                Cards1.Add(Deck.OutCard());
                Cards2.Add(Deck.OutCard());
                Cards3.Add(Deck.OutCard());
                Cards4.Add(Deck.OutCard());
            }

            // Virar la carta de La Vida
            Life = Deck.OutCard();

            // Formato de InitHand para 2 vs 2:
            // P1(3 cartas) - P2(3 cartas) - P3(3 cartas) - P4(3 cartas) - Vida
            List<string> parts = new List<string>();
            foreach (var c in Cards1) parts.Add(c.Id.ToString("D2"));
            foreach (var c in Cards2) parts.Add(c.Id.ToString("D2"));
            foreach (var c in Cards3) parts.Add(c.Id.ToString("D2"));
            foreach (var c in Cards4) parts.Add(c.Id.ToString("D2"));
            parts.Add(Life.Id.ToString("D2"));

            InitHand = string.Join("-", parts);
        }

        public List<Card> GetPlayerCards(int playerIndex)
        {
            return playerIndex switch
            {
                0 => Cards1,
                1 => Cards2,
                2 => Cards3,
                3 => Cards4,
                _ => Cards1
            };
        }

        public Card? PopPlayerCard(int playerIndex, int cardId)
        {
            var list = GetPlayerCards(playerIndex);
            var card = list.FirstOrDefault(c => c.Id == cardId);
            if (card != null)
            {
                list.Remove(card);
                return card;
            }
            if (list.Count > 0)
            {
                var fallback = list[0];
                list.RemoveAt(0);
                return fallback;
            }
            return null;
        }

        // Determina si candidateCard supera a currentBestCard según las reglas oficiales de Pericón
        public static bool DoesCandidateBeatBest(int currentBestCard, int candidateCard, int leadCard, int lifeCardId)
        {
            int powerBest = GamePlayOneVsOne.EvaluateCard(currentBestCard, lifeCardId);
            int powerCandidate = GamePlayOneVsOne.EvaluateCard(candidateCard, lifeCardId);

            bool bestIsTriumph = powerBest >= 11;
            bool candidateIsTriumph = powerCandidate >= 11;

            if (candidateIsTriumph && !bestIsTriumph) return true;
            if (!candidateIsTriumph && bestIsTriumph) return false;

            if (candidateIsTriumph && bestIsTriumph)
            {
                return powerCandidate > powerBest;
            }

            // Ambas son cartas comunes
            char suitLead = SpanishCards.GetSuit(leadCard);
            char suitBest = SpanishCards.GetSuit(currentBestCard);
            char suitCandidate = SpanishCards.GetSuit(candidateCard);

            // Si el candidato no es del palo de salida, es descarte y no puede superar a una carta del palo
            if (suitCandidate != suitLead) return false;

            // Si la mejor hasta ahora no era del palo de salida pero el candidato sí, gana el candidato
            if (suitBest != suitLead && suitCandidate == suitLead) return true;

            // Ambas son del palo de salida: gana el valor facial más alto
            int faceBest = SpanishCards.GetFaceValue(currentBestCard);
            int faceCandidate = SpanishCards.GetFaceValue(candidateCard);
            return faceCandidate > faceBest;
        }

        // Determina el ganador de una baza completa de 4 cartas
        // Retorna el índice del jugador ganador (0, 1, 2, o 3) y el equipo ganador (1 o 2)
        public (int winningPlayerIndex, int winningTeam) DetermineTrickWinner()
        {
            if (CurrentTrick.Count == 0) return (LeadPlayer, (LeadPlayer % 2 == 0) ? 1 : 2);

            int leadCardId = CurrentTrick[0].card.Id;
            int bestPlayer = CurrentTrick[0].playerIndex;
            int bestCardId = CurrentTrick[0].card.Id;

            for (int i = 1; i < CurrentTrick.Count; i++)
            {
                int candidateCardId = CurrentTrick[i].card.Id;
                int candidatePlayer = CurrentTrick[i].playerIndex;

                if (DoesCandidateBeatBest(bestCardId, candidateCardId, leadCardId, Life.Id))
                {
                    bestCardId = candidateCardId;
                    bestPlayer = candidatePlayer;
                }
            }

            int winningTeam = (bestPlayer == 0 || bestPlayer == 2) ? 1 : 2;
            return (bestPlayer, winningTeam);
        }

        public int GenerateSeed(List<GamePlayTwoVsTwo> buffer)
        {
            bool flagSeed;
            int gameSeed = 0;
            Random random = new Random();
            do
            {
                flagSeed = false;
                gameSeed = random.Next(1, int.MaxValue);
                foreach (var g in buffer)
                {
                    if (g.Id == gameSeed)
                    {
                        flagSeed = true;
                        break;
                    }
                }
            } while (flagSeed);
            return gameSeed;
        }
    }
}
