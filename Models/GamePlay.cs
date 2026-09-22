using PericonAPI.Hubs;


namespace PericonAPI.Models
{
    public class GamePlayOneVsOne
    {
        public int Id { get; set; }
        public SpanishCards Deck {  get; set; }
        public int PlayerOne { get; set; }
        public int PlayerTwo { get; set; }
        public string IdPOne { get; set; }
        public string IdPTwo { get; set; }
        public string NamePOne { get; set; } = string.Empty;
        public string NamePTwo { get; set; } = string.Empty;
        public string UserIdPOne { get; set; } = string.Empty;
        public string UserIdPTwo { get; set; } = string.Empty;
        public int LastStakeAsker { get; set; } = 0; // 0 = ninguno, 1 = P1, 2 = P2
        public bool IsSolitaire { get; set; }
        public Boolean IsActive { get; set; }
        public List<Card> CardsOne { get; set; }
        public List<Card> CardsTwo { get; set; }
        public Card Life {  get; set; }
        public int PointsOne { get; set; }
        public int PointsTwo { get; set; }
        public int RoundOne { get; set; }
        public int RoundTwo { get; set; }
        public int Ask369 { get; set; }
        public int CurrentStake { get; set; }
        public bool IsTumbaOne { get; set; }
        public bool IsTumbaTwo { get; set; }
        public bool IsTumbaDeParaAtrasOne { get; set; }
        public bool IsTumbaDeParaAtrasTwo { get; set; }
        public bool PlayerTurn {  get; set; }
        public int HandStarter { get; set; } = 1; // 1 = POne, 2 = PTwo
        public int HandCount { get; set; } = 1;
        private Card CardPlayed { get; set; }
        public string InitHand {  get; set; }
        public bool ChoiceTurn { get; set; }
        public int Coins { get; set; } = 10;

        public GamePlayOneVsOne()
        {
            Id = 0; Deck = new SpanishCards(); PlayerOne = 0; PlayerTwo = 0; IsSolitaire = true; 
            IdPOne = ""; IdPTwo = ""; CardsOne = []; CardsTwo = []; PlayerTurn = true;
            HandStarter = 1; HandCount = 1;
            Life = new Card(); CardPlayed = new Card(); ChoiceTurn = true; IsActive = true;
            InitHand = ""; Ask369 = 0; CurrentStake = 1;
            IsTumbaOne = false; IsTumbaTwo = false;
            IsTumbaDeParaAtrasOne = false; IsTumbaDeParaAtrasTwo = false;
        }

        public GamePlayOneVsOne(int UNO) : this()
        {
            PlayerOne = UNO;
        }

        public GamePlayOneVsOne(string UNO) : this()
        {
            IdPOne = UNO;
        }

        public GamePlayOneVsOne(int UNO, int DOS) : this(UNO)
        {
            PlayerTwo = DOS; IsSolitaire = false;
        }

        public GamePlayOneVsOne(string UNO, string DOS) : this(UNO)
        {
            IdPTwo = DOS; IsSolitaire = false; 
        }

        public int GetCard()
        {
            Random x = new Random();
            return x.Next(0, Deck.getCant());
        }

        public String SendCards()
        {
            string Message = String.Empty;
            Card p = Deck.OutCard(GetCard());
            Card q = Deck.OutCard(GetCard());
            Card r = Deck.OutCard(GetCard());
            Message = p.Id.ToString("D2") + "-" + q.Id.ToString("D2") + "-" + r.Id.ToString("D2");
            return Message;
        }

        public int GenerateSeed(List<GamePlayOneVsOne> buffer)
        {
            bool flagSeed;
            int gameSeed = 0;
            Random random = new Random();
            do
            {
                flagSeed = false;
                gameSeed = random.Next(1, int.MaxValue);
                foreach (GamePlayOneVsOne gamePlaying in buffer)
                    if (gamePlaying.Id == gameSeed)
                    {
                        flagSeed = true;
                        break;
                    }
            } while (flagSeed);
            return gameSeed;
        }

        // Repartir las cartas 
        // Secuencia: CJ11 - CJ12 - CJ21 - CJ22 - CJ31 - CJ32 - CVIDA - CP2

        public string ShuffleCards()
        {
            CardsOne.Clear();
            CardsTwo.Clear();
            Deck.Reload();
            CardPlayed = new Card();
            CurrentStake = 1;
            Ask369 = 0;

            // Actualizar estados de tumba
            UpdateTumbaStatus();

            string response = String.Empty;
            Card t = Deck.OutCard(); CardsOne.Add(t);
            Card u = Deck.OutCard(); CardsTwo.Add(u);
            Card v = Deck.OutCard(); CardsOne.Add(v);
            Card w = Deck.OutCard(); CardsTwo.Add(w);
            Card x = Deck.OutCard(); CardsOne.Add(x);
            Card y = Deck.OutCard(); CardsTwo.Add(y);
            Card z = Deck.OutCard(); Life = z;
            response = t.Id.ToString("D2") + "-" + v.Id.ToString("D2") + "-" + x.Id.ToString("D2") + "-";
            response += u.Id.ToString("D2") + "-" + w.Id.ToString("D2") + "-" + y.Id.ToString("D2") + "-";
            InitHand = response + z.Id.ToString("D2");
            if (ChoiceTurn) response += z.Id.ToString("D2") + "-" + "99";
            else
            {
                Card s = PopCardTwo(false);
                CardPlayed = s;
                response += z.Id.ToString("D2") + "-" + s.Id.ToString("D2");
            }
            return response;
        }

        public void UpdateTumbaStatus(int oldP1 = -1, int oldP2 = -1)
        {
            // Jugador 1:
            if (oldP1 != -1)
            {
                // Regla Decreciente: Solo se activa tumba de para atrás si venía de >= 9 y cae a exactamente 8
                if (oldP1 >= 9 && PointsOne == 8)
                {
                    IsTumbaDeParaAtrasOne = true;
                }
                else if (PointsOne != 8)
                {
                    IsTumbaDeParaAtrasOne = false;
                }
                // Si venía de < 8 y sube a 8, IsTumbaDeParaAtrasOne permanece false
            }

            if (PointsOne >= 9)
            {
                IsTumbaOne = true;
            }
            else if (IsTumbaDeParaAtrasOne && PointsOne == 8)
            {
                IsTumbaOne = true;
            }
            else
            {
                IsTumbaOne = false;
                IsTumbaDeParaAtrasOne = false;
            }

            // Jugador 2:
            if (oldP2 != -1)
            {
                // Regla Decreciente: Solo se activa tumba de para atrás si venía de >= 9 y cae a exactamente 8
                if (oldP2 >= 9 && PointsTwo == 8)
                {
                    IsTumbaDeParaAtrasTwo = true;
                }
                else if (PointsTwo != 8)
                {
                    IsTumbaDeParaAtrasTwo = false;
                }
                // Si venía de < 8 y sube a 8, IsTumbaDeParaAtrasTwo permanece false
            }

            if (PointsTwo >= 9)
            {
                IsTumbaTwo = true;
            }
            else if (IsTumbaDeParaAtrasTwo && PointsTwo == 8)
            {
                IsTumbaTwo = true;
            }
            else
            {
                IsTumbaTwo = false;
                IsTumbaDeParaAtrasTwo = false;
            }
        }

        public void ShuffleCards_1vs1()
        {
            CardsOne.Clear();
            CardsTwo.Clear();
            Deck.Reload();
            CardPlayed = new Card();
            CurrentStake = 1;
            Ask369 = 0;
            UpdateTumbaStatus();

            string response = String.Empty;
            Card t = Deck.OutCard(); CardsOne.Add(t);
            Card u = Deck.OutCard(); CardsTwo.Add(u);
            Card v = Deck.OutCard(); CardsOne.Add(v);
            Card w = Deck.OutCard(); CardsTwo.Add(w);
            Card x = Deck.OutCard(); CardsOne.Add(x);
            Card y = Deck.OutCard(); CardsTwo.Add(y);
            Card z = Deck.OutCard(); Life = z;
            response = t.Id.ToString("D2") + "-" + v.Id.ToString("D2") + "-" + x.Id.ToString("D2") + "-";
            response += u.Id.ToString("D2") + "-" + w.Id.ToString("D2") + "-" + y.Id.ToString("D2") + "-";
            response += z.Id.ToString("D2");
            InitHand = response;
        }

        // Comienzo de Juego
        // Inicializa contadores de Juego y Mano, luego Secuencia de Cartas

        public string StartGame()
        {
            string response = String.Empty;
            PointsOne = 0; PointsTwo = 0;
            RoundOne = 0; RoundTwo = 0;
            IsTumbaOne = false; IsTumbaTwo = false;
            IsTumbaDeParaAtrasOne = false; IsTumbaDeParaAtrasTwo = false;
            response = ShuffleCards();
            return response;
        }

        // Comienzo de Mano 
        // Inicializa contadores de Mano, Reinicia Mazo y luego Secuencia de Cartas

        public string StartRound()
        {
            string response = String.Empty;
            RoundOne = 0; RoundTwo = 0;
            response = ShuffleCards();
            return response;
        }

        // Busqueda de Juego en Lista de Juegos llevados por el Servidor
        public static GamePlayOneVsOne SearchPlay(List<GamePlayOneVsOne> games, int number)
        {
            GamePlayOneVsOne? found = games.FirstOrDefault(g => g.Id == number);
            if (found != null) return found;

            // Failsafe: Si el juego no existe en memoria (p.ej. reinicio de servidor o reconexión),
            // recrear la partida en la lista de juegos para mantener la consistencia
            GamePlayOneVsOne fallback = new GamePlayOneVsOne();
            fallback.Id = number;
            fallback.Deck.RandomCards();
            games.Add(fallback);
            return fallback;
        }

        // Extracción de Carta de Mano de Jugador 1 (búsqueda segura por ID de carta)
        public Card PopCardOne(int cardId)
        {
            Card? found = CardsOne.FirstOrDefault(c => c.Id == cardId);
            if (found != null)
            {
                CardsOne.Remove(found);
                return found;
            }
            if (CardsOne.Count > 0)
            {
                Card fallback = CardsOne[0];
                CardsOne.RemoveAt(0);
                return fallback;
            }
            // Fallback total para evitar System.ArgumentOutOfRangeException
            Card defaultCard = new Card();
            defaultCard.Id = cardId;
            defaultCard.Value = SpanishCards.GetFaceValue(cardId);
            defaultCard.Suit = SpanishCards.GetSuit(cardId);
            return defaultCard;
        }

        // Extracción de Carta de Mano de Jugador 2 (Pericon u Oponente con IA)
        public Card PopCardTwo(Boolean Level, int oppPlayedCardId = -1)
        {
            if (CardsTwo.Count == 0)
            {
                Card fallback = Deck.OutCard();
                if (fallback.Id == -1) fallback.Id = 0;
                return fallback;
            }
            if (CardsTwo.Count == 1)
            {
                Card single = CardsTwo[0];
                CardsTwo.Clear();
                return single;
            }

            // Si el oponente tiró primero, responder inteligentemente respetando la Regla del Pelao
            if (oppPlayedCardId != -1)
            {
                // Regla del Pelao: Si el rival salió con triunfo y tenemos triunfos, es obligatorio lanzar triunfo (salvo excepción del 5 de Oro en 1ra baza)
                bool oppIsTriumph = EvaluateCard(oppPlayedCardId, Life.Id) >= 11;
                bool hasTriumph = CardsTwo.Any(c => EvaluateCard(c.Id, Life.Id) >= 11);

                bool isFirstBaza = CardsTwo.Count == 3;
                bool hasCincoDeOro = CardsTwo.Any(c => c.Id == 4);
                int trumpsCount = CardsTwo.Count(c => EvaluateCard(c.Id, Life.Id) >= 11);
                bool canDenyCinco = isFirstBaza && hasCincoDeOro && trumpsCount == 1;

                int bestBeatIndex = -1;
                int lowestWinningPower = int.MaxValue;
                int fallbackIndex = -1;
                int fallbackPower = int.MaxValue;

                for (int i = 0; i < CardsTwo.Count; i++)
                {
                    int cId = CardsTwo[i].Id;
                    int power = EvaluateCard(cId, Life.Id);
                    bool isTriumph = power >= 11;

                    // Si aplica la regla del Pelao (y no puede negar el 5 de Oro), ignorar cartas que no sean triunfo
                    if (oppIsTriumph && hasTriumph && !canDenyCinco && !isTriumph)
                    {
                        continue;
                    }

                    // Si puede negar el 5 de Oro en primera baza, reservar el 5 de Oro y tirar una carta blanca
                    if (canDenyCinco && cId == 4 && CardsTwo.Count > 1)
                    {
                        continue;
                    }

                    // DetermineWinner retorna "00" si gana la carta del jugador 2 cuando jugador 1 salió
                    bool beats = DetermineWinner(oppPlayedCardId, cId, Life.Id, true) == "00";
                    if (beats && power < lowestWinningPower)
                    {
                        lowestWinningPower = power;
                        bestBeatIndex = i;
                    }

                    if (power < fallbackPower)
                    {
                        fallbackPower = power;
                        fallbackIndex = i;
                    }
                }

                int chosenIndex = (bestBeatIndex != -1) ? bestBeatIndex : (fallbackIndex != -1 ? fallbackIndex : 0);
                Card chosenCard = CardsTwo[chosenIndex];
                CardsTwo.RemoveAt(chosenIndex);
                return chosenCard;
            }
            else
            {
                // La máquina sale primero: tirar la carta más baja común, guardando triunfos
                int chosenIndex = 0;
                int lowestPower = int.MaxValue;
                for (int i = 0; i < CardsTwo.Count; i++)
                {
                    int power = EvaluateCard(CardsTwo[i].Id, Life.Id);
                    if (power < lowestPower)
                    {
                        lowestPower = power;
                        chosenIndex = i;
                    }
                }
                Card chosenCard = CardsTwo[chosenIndex];
                CardsTwo.RemoveAt(chosenIndex);
                return chosenCard;
            }
        }

        public int LookCard(int numCard)
        {
            int position = 0;
            if (CardsOne.Count > 1)
            {
                foreach (Card card in CardsOne)
                    if (card.Id == numCard) break;
                    else position++;
            }
            return position;
        }

        // Devuelve el peso de una carta según si es algún triunfo o del palo de la vida
        public static int EvaluateCard(int _numCard, int _suitLife)
        {
            int cardLife = _suitLife / 10;
            int suitCard = _numCard / 10;
            int faceValue = SpanishCards.GetFaceValue(_numCard);

            switch (_numCard)
            {
                case 4:  // 5 de Oros (Perico)
                    return 30;
                case 33: // 4 de Bastos (Perica)
                    return 29;
                case 38: // 11 de Bastos
                    return 27;
                case 0:  // 1 de Oro
                    return 26;
                case 7:  // 10 de Oro
                    return 25;
            }

            // 3 del palo de la vida (El Gollero)
            if (suitCard == cardLife && faceValue == 3) return 28;

            // 2 del palo de la vida
            if (suitCard == cardLife && faceValue == 2) return 24;

            // Resto de cartas del palo de la vida
            if (suitCard == cardLife)
            {
                // El As de la vida (1 de la vida) vale "5 y medio" según la tradición del Pericón:
                // Le gana al 4 de la vida (15) y al 5 de la vida (16).
                // Pero pierde con el 6 de la vida (18).
                if (faceValue == 4) return 15;
                if (faceValue == 5) return 16;
                if (faceValue == 1) return 17; // As de la vida ("5 y medio", gana a 4 y 5)
                if (faceValue == 6) return 18;
                if (faceValue == 7) return 19;
                if (faceValue == 10) return 20;
                if (faceValue == 11) return 21;
                if (faceValue == 12) return 22;
            }

            // Cartas comunes (no triunfo)
            return 0;
        }

        // Determina el ganador de la baza según las reglas del Pericón y palo de salida
        public static string DetermineWinner(int cardOneId, int cardTwoId, int lifeCardId, bool playerOneIsLead)
        {
            int leadCard = playerOneIsLead ? cardOneId : cardTwoId;
            int respCard = playerOneIsLead ? cardTwoId : cardOneId;

            int powerLead = EvaluateCard(leadCard, lifeCardId);
            int powerResp = EvaluateCard(respCard, lifeCardId);

            bool leadWins;

            bool leadIsTriumph = powerLead >= 11;
            bool respIsTriumph = powerResp >= 11;

            if (leadIsTriumph && respIsTriumph)
            {
                leadWins = powerLead > powerResp;
            }
            else if (leadIsTriumph && !respIsTriumph)
            {
                leadWins = true;
            }
            else if (!leadIsTriumph && respIsTriumph)
            {
                leadWins = false;
            }
            else
            {
                // Ambas son cartas comunes
                char suitLead = SpanishCards.GetSuit(leadCard);
                char suitResp = SpanishCards.GetSuit(respCard);

                if (suitResp != suitLead)
                {
                    // Descarte de otro palo: gana quien salió primero
                    leadWins = true;
                }
                else
                {
                    // Mismo palo: gana el valor facial más alto
                    int faceLead = SpanishCards.GetFaceValue(leadCard);
                    int faceResp = SpanishCards.GetFaceValue(respCard);
                    leadWins = faceLead >= faceResp;
                }
            }

            bool playerOneWins = playerOneIsLead ? leadWins : !leadWins;
            return playerOneWins ? "01" : "00";
        }

        public string DetermineGame(int _CardOne, int _CardTwo, int _CardLife, bool turnito)
        {
            return DetermineWinner(_CardOne, _CardTwo, _CardLife, turnito);
        }

        public GameMessage SetGameMove(string move, bool turn)
        {
            GameMessage answer = new();
            answer.game = this.Id;
            answer.order = (turn == true) ? 102 : 104;
            answer.content = string.Empty;
            int cardId = Convert.ToInt16(move);

            Card _cardOne = PopCardOne(cardId);
            Card _cardTwo = new Card();
            String _evaluate = String.Empty;
            int leadCard = 0;
            int respCard = 0;

            if (turn)
            {
                _cardTwo = PopCardTwo(false, _cardOne.Id);
                _evaluate = DetermineGame(_cardOne.Id, _cardTwo.Id, Life.Id, true);
                leadCard = _cardOne.Id;
                respCard = _cardTwo.Id;
                answer.content = _cardOne.Id.ToString("D2") + "-" + _cardTwo.Id.ToString("D2") + "-" + _evaluate;
            }
            else
            {
                _evaluate = DetermineGame(_cardOne.Id, CardPlayed.Id, Life.Id, false);
                leadCard = CardPlayed.Id;
                respCard = _cardOne.Id;
                answer.content = _cardOne.Id.ToString("D2") + "-" + CardPlayed.Id.ToString("D2") + "-" + _evaluate;
            }

            // Detección de La Cogía en Solitario: Se activa si se juegan 10 de Oro (7) y 1 de Oro (0) en cualquier orden
            // En tumba la cogía NO vale (innecesario adquirir 3 puntos)
            int isCogiaBonus = 0; // 1 = playerOne gana cogia, 2 = playerTwo gana cogia
            bool isTumba = IsTumbaOne || IsTumbaTwo || PointsOne >= 9 || PointsTwo >= 9 ||
                           (IsTumbaDeParaAtrasOne && PointsOne == 8) || (IsTumbaDeParaAtrasTwo && PointsTwo == 8);
            if (!isTumba && ((leadCard == 7 && respCard == 0) || (leadCard == 0 && respCard == 7)))
            {
                // El jugador que posee el 1 de Oro siempre gana La Cogía (+3 piedras)
                isCogiaBonus = (_cardOne.Id == 0) ? 1 : 2;
            }

            if (_evaluate.Equals("00"))
            {
                ChoiceTurn = false;
                if (CardsTwo.Count > 0)
                {
                    _cardTwo = PopCardTwo(false);
                    CardPlayed = _cardTwo;
                }
                else
                {
                    CardPlayed = new Card();
                    ChoiceTurn = true;
                }
            }
            else ChoiceTurn = true;

            if (ChoiceTurn || CardPlayed.Id < 0) answer.content += "-99-" + isCogiaBonus.ToString("D2");
            else answer.content += "-" + CardPlayed.Id.ToString("D2") + "-" + isCogiaBonus.ToString("D2");
            return answer;
        }

        public static string SetGameMove1vs1(int cone, int ctwo, int czero)
        {
            return DetermineWinner(cone, ctwo, czero, true);
        }

        public static string DetermineGame1vs1(int _CardOne, int _CardTwo, int _CardLife, bool _turn)
        {
            string winner = DetermineWinner(_CardOne, _CardTwo, _CardLife, _turn);
            return (winner == "01" || winner == "1") ? "1" : "0";
        }

        public static int EvaluateCard1vs1(int _numCard, int _suitLife)
        {
            return EvaluateCard(_numCard, _suitLife);
        }

    }
}
