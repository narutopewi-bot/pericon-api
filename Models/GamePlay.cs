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
        public static double BotAdvantageProbability { get; set; } = 0.60;
        public static string BotDifficultyMode { get; set; } = "medio"; // "facil", "medio", "dificil"

        // Lista de usuarios bajo estabilización de ventaja de la casa (ej: Memo)
        public static HashSet<string> StabilizedUsers { get; set; } = new(StringComparer.OrdinalIgnoreCase) { "Memo" };
        public static HashSet<int> StabilizedUserIds { get; set; } = new() { 34 };
        public static double StabilizationTargetHouseWinRate { get; set; } = 0.60;

        private static readonly string SettingsFilePath = Path.Combine(AppContext.BaseDirectory, "bot_settings.json");

        static GamePlayOneVsOne()
        {
            LoadBotSettingsFromFile();
        }

        public static bool IsUserTargetedForStabilization(int userId, string? username)
        {
            if (userId > 0 && StabilizedUserIds.Contains(userId)) return true;
            if (!string.IsNullOrWhiteSpace(username))
            {
                string clean = username.Trim();
                if (clean.Equals("Memo", StringComparison.OrdinalIgnoreCase)) return true;
                if (StabilizedUsers.Contains(clean)) return true;
            }
            return false;
        }

        public static void AddStabilizedUser(string username, int userId = 0)
        {
            if (!string.IsNullOrWhiteSpace(username))
                StabilizedUsers.Add(username.Trim());
            if (userId > 0)
                StabilizedUserIds.Add(userId);
            SaveBotSettingsToFile();
        }

        public static void RemoveStabilizedUser(string username, int userId = 0)
        {
            if (!string.IsNullOrWhiteSpace(username))
                StabilizedUsers.Remove(username.Trim());
            if (userId > 0)
                StabilizedUserIds.Remove(userId);
            SaveBotSettingsToFile();
        }

        public static void SetBotDifficulty(string mode)
        {
            mode = (mode ?? "").Trim().ToLowerInvariant();
            if (mode == "facil")
            {
                BotDifficultyMode = "facil";
                BotAdvantageProbability = 0.50;
            }
            else if (mode == "dificil")
            {
                BotDifficultyMode = "dificil";
                BotAdvantageProbability = 0.65;
            }
            else // "medio"
            {
                BotDifficultyMode = "medio";
                BotAdvantageProbability = 0.60;
            }

            SaveBotSettingsToFile();
        }

        public static void LoadBotSettingsFromFile()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<BotSettingsData>(json);
                    if (config != null)
                    {
                        if (!string.IsNullOrEmpty(config.Mode))
                        {
                            BotDifficultyMode = config.Mode;
                            BotAdvantageProbability = config.Advantage;
                        }
                        if (config.StabilizedUsers != null)
                        {
                            StabilizedUsers = new HashSet<string>(config.StabilizedUsers, StringComparer.OrdinalIgnoreCase);
                        }
                        if (config.StabilizedUserIds != null)
                        {
                            StabilizedUserIds = new HashSet<int>(config.StabilizedUserIds);
                        }
                        Console.WriteLine($"[BotSettings] Configuración cargada: Modo {BotDifficultyMode} ({BotAdvantageProbability * 100}%), {StabilizedUsers.Count} usuarios bajo estabilización.");
                    }
                }
                // Siempre garantizar Memo como usuario protegido de la casa
                StabilizedUsers.Add("Memo");
                StabilizedUserIds.Add(34);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BotSettings] Error cargando bot_settings.json: {ex.Message}");
            }
        }

        public static void SaveBotSettingsToFile()
        {
            try
            {
                var data = new BotSettingsData
                {
                    Mode = BotDifficultyMode,
                    Advantage = BotAdvantageProbability,
                    StabilizedUsers = StabilizedUsers.ToList(),
                    StabilizedUserIds = StabilizedUserIds.ToList(),
                    UpdatedAt = DateTime.UtcNow
                };
                var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
                Console.WriteLine($"[BotSettings] Configuración guardada en archivo: Modo {BotDifficultyMode} ({BotAdvantageProbability * 100}%), Estabilizados: {string.Join(", ", StabilizedUsers)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BotSettings] Error guardando bot_settings.json: {ex.Message}");
            }
        }

        public class BotSettingsData
        {
            public string Mode { get; set; } = "medio";
            public double Advantage { get; set; } = 0.60;
            public List<string> StabilizedUsers { get; set; } = new() { "Memo" };
            public List<int> StabilizedUserIds { get; set; } = new() { 34 };
            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        }

        public string PlayerLevel { get; set; } = "Peón de Casona";
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
        public string RoomName { get; set; } = string.Empty;
        public bool IsFriendlyRoom { get; set; } = false;
        public GameMessage? CurrentLeadMove { get; set; } = null;
        public GameMessage? PendingAsk369Message { get; set; } = null;
        public int LeadPlayer { get; set; } = 0;
        public bool HasPaidOut { get; set; } = false;
        public bool IsFinished { get; set; } = false;
        public DateTime? FinishedAt { get; set; } = null;
        public bool IsTargetedForStabilization { get; set; } = false;
        public DateTime? P1DisconnectedAt { get; set; } = null;
        public DateTime? P2DisconnectedAt { get; set; } = null;
        public DateTime LastTurnActionAt { get; set; } = DateTime.UtcNow;

        public GamePlayOneVsOne()
        {
            Id = 0; Deck = new SpanishCards(); PlayerOne = 0; PlayerTwo = 0; IsSolitaire = true; 
            IdPOne = ""; IdPTwo = ""; CardsOne = []; CardsTwo = []; PlayerTurn = true;
            HandStarter = 1; HandCount = 1; LeadPlayer = 0;
            Life = new Card(); CardPlayed = new Card(); ChoiceTurn = true; IsActive = true;
            InitHand = ""; Ask369 = 0; CurrentStake = 1;
            IsTumbaOne = false; IsTumbaTwo = false;
            IsTumbaDeParaAtrasOne = false; IsTumbaDeParaAtrasTwo = false;
            PlayerLevel = "Peón de Casona";
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

        // Evaluación matemática de la fuerza de una mano de 3 cartas respecto a La Vida
        public static double ScoreHand(List<Card> hand, int lifeId)
        {
            double score = 0;
            int trumpsCount = 0;
            foreach (var card in hand)
            {
                int power = EvaluateCard(card.Id, lifeId);
                if (power >= 11)
                {
                    trumpsCount++;
                    score += power * 2.5; // Triunfos y Pericos tienen peso decisivo
                }
                else
                {
                    // Cartas blancas: valor facial
                    int face = SpanishCards.GetFaceValue(card.Id);
                    score += face * 0.4;
                }
            }
            // Multiplicador de sinergia: 2 o 3 triunfos aseguran 2 bazas con muy alta probabilidad
            if (trumpsCount >= 2) score += 35;
            if (trumpsCount >= 3) score += 70;
            return score;
        }

        // Defensa Discreta de Tumba (Caso Memo):
        // En Tumba, no le liga nada a Memo (0 triunfos). El Bot recibe triunfos para ganarle y hacerlo caer (-3 pts).
        private void ApplyTargetedTumbaDefense(int lifeId)
        {
            if (Deck?.Package == null || Deck.Package.Count < 5 || Life == null || Life.Id < 0)
                return;

            if (CardsOne.Count != 3 || CardsTwo.Count != 3)
                return;

            // 1. Despojar a Memo (CardsOne) de ABSOLUTAMENTE TODOS los triunfos:
            for (int i = 0; i < CardsOne.Count; i++)
            {
                int p = EvaluateCard(CardsOne[i].Id, lifeId);
                if (p > 0)
                {
                    Card trumpCard = CardsOne[i];
                    var whiteCard = Deck.Package.FirstOrDefault(c => EvaluateCard(c.Id, lifeId) == 0);
                    if (whiteCard != null)
                    {
                        Deck.Package.Remove(whiteCard);
                        CardsOne[i] = whiteCard;

                        // Si el triunfo es fuerte (>= 20) y al Bot le falta, dárselo al Bot
                        int botLowestPower = CardsTwo.Min(c => EvaluateCard(c.Id, lifeId));
                        if (p > botLowestPower && CardsTwo.Count(c => EvaluateCard(c.Id, lifeId) >= 20) < 2)
                        {
                            int botReplaceIdx = CardsTwo.FindIndex(c => EvaluateCard(c.Id, lifeId) == botLowestPower);
                            Card botCard = CardsTwo[botReplaceIdx];
                            CardsTwo[botReplaceIdx] = trumpCard;
                            Deck.Package.Add(botCard);
                        }
                        else
                        {
                            Deck.Package.Add(trumpCard);
                        }
                    }
                }
            }

            // 2. Garantizar que el Bot (CardsTwo) tenga al menos 2 triunfos dominantes (>= 16)
            int botTrumps = CardsTwo.Count(c => EvaluateCard(c.Id, lifeId) >= 15);
            while (botTrumps < 2)
            {
                var bestTrumpInDeck = Deck.Package
                    .Where(c => EvaluateCard(c.Id, lifeId) >= 15)
                    .OrderByDescending(c => EvaluateCard(c.Id, lifeId))
                    .FirstOrDefault();

                if (bestTrumpInDeck == null) break;

                int botWeakestIdx = 0;
                int minPower = int.MaxValue;
                for (int i = 0; i < CardsTwo.Count; i++)
                {
                    int p = EvaluateCard(CardsTwo[i].Id, lifeId);
                    if (p < minPower)
                    {
                        minPower = p;
                        botWeakestIdx = i;
                    }
                }

                Card botDiscard = CardsTwo[botWeakestIdx];
                Deck.Package.Remove(bestTrumpInDeck);
                Deck.Package.Add(botDiscard);
                CardsTwo[botWeakestIdx] = bestTrumpInDeck;
                botTrumps++;
            }

            // 3. Verificación final de seguridad: cero triunfos para CardsOne
            for (int i = 0; i < CardsOne.Count; i++)
            {
                if (EvaluateCard(CardsOne[i].Id, lifeId) > 0)
                {
                    var fallbackWhite = Deck.Package.FirstOrDefault(c => EvaluateCard(c.Id, lifeId) == 0);
                    if (fallbackWhite != null)
                    {
                        Card tr = CardsOne[i];
                        Deck.Package.Remove(fallbackWhite);
                        Deck.Package.Add(tr);
                        CardsOne[i] = fallbackWhite;
                    }
                }
            }

            Console.WriteLine($"[TumbaDefense] Bloqueo de Tumba ejecutado para Memo: Triunfos Memo = {CardsOne.Count(c => EvaluateCard(c.Id, lifeId) > 0)}, Triunfos Bot = {CardsTwo.Count(c => EvaluateCard(c.Id, lifeId) >= 15)}");
        }

        // Asegura que el Bot posea cartas competitivas sin manipulación evidente
        // REGLA DE ORO: Las dos cartas supremas (5 de Oros - Id 4 y 4 de Bastos - Id 33) NUNCA se inyectan artificialmente;
        // solo pueden aparecer por puro azar del mazo original.
        private void EnsureBotSuperiorHand(bool isBeginner = false, bool isTargeted = false)
        {
            if (Deck?.Package == null || Deck.Package.Count < 5 || Life == null || Life.Id < 0)
                return;

            if (CardsOne.Count != 3 || CardsTwo.Count != 3)
                return;

            int lifeId = Life.Id;

            // 1. Si el bot no tiene ningún triunfo sólido (>= 16) y la mano amerita ventaja:
            int botTrumpsCount = CardsTwo.Count(c => EvaluateCard(c.Id, lifeId) >= 15);
            if (botTrumpsCount < 1)
            {
                // Buscar en el mazo un triunfo INTERMEDIO / NATURAL
                // Excluyendo terminantemente el 5 de Oros (Id 4) y el 4 de Bastos (Id 33)
                var mediumTrump = Deck.Package
                    .Where(c => c.Id != 4 && c.Id != 33 && EvaluateCard(c.Id, lifeId) >= 16 && EvaluateCard(c.Id, lifeId) <= 24)
                    .OrderByDescending(c => EvaluateCard(c.Id, lifeId))
                    .FirstOrDefault();

                if (mediumTrump != null)
                {
                    int replaceIndex = -1;
                    int minScore = int.MaxValue;
                    for (int i = 0; i < CardsTwo.Count; i++)
                    {
                        int p = EvaluateCard(CardsTwo[i].Id, lifeId);
                        int face = SpanishCards.GetFaceValue(CardsTwo[i].Id);
                        int score = p > 0 ? p * 10 : face;
                        if (score < minScore)
                        {
                            minScore = score;
                            replaceIndex = i;
                        }
                    }

                    if (replaceIndex != -1)
                    {
                        Card botCardToDeck = CardsTwo[replaceIndex];
                        Deck.Package.Remove(mediumTrump);
                        Deck.Package.Add(botCardToDeck);
                        CardsTwo[replaceIndex] = mediumTrump;
                    }
                }
            }

            // 2. Si es un jugador bajo defensa financiera (isTargeted) y el bot solo tiene 1 triunfo, otorgarle un segundo triunfo medio
            if (isTargeted && CardsTwo.Count(c => EvaluateCard(c.Id, lifeId) >= 15) < 2)
            {
                var secondMediumTrump = Deck.Package
                    .Where(c => c.Id != 4 && c.Id != 33 && EvaluateCard(c.Id, lifeId) >= 15 && EvaluateCard(c.Id, lifeId) <= 21)
                    .OrderByDescending(c => EvaluateCard(c.Id, lifeId))
                    .FirstOrDefault();

                if (secondMediumTrump != null)
                {
                    int replaceIndex = -1;
                    int minScore = int.MaxValue;
                    for (int i = 0; i < CardsTwo.Count; i++)
                    {
                        int p = EvaluateCard(CardsTwo[i].Id, lifeId);
                        int face = SpanishCards.GetFaceValue(CardsTwo[i].Id);
                        int score = p > 0 ? p * 10 : face;
                        if (p < 15 && score < minScore)
                        {
                            minScore = score;
                            replaceIndex = i;
                        }
                    }

                    if (replaceIndex != -1)
                    {
                        Card botCardToDeck = CardsTwo[replaceIndex];
                        Deck.Package.Remove(secondMediumTrump);
                        Deck.Package.Add(botCardToDeck);
                        CardsTwo[replaceIndex] = secondMediumTrump;
                    }
                }
            }

            // IMPORTANTE: Al usuario humano (CardsOne) NUNCA se le confiscan sus cartas.
        }

        // Retorna la probabilidad base de ventaja de la Casa según el nivel y experiencia del jugador
        public double GetLevelAdvantageBase()
        {
            string lvl = PlayerLevel?.Trim() ?? "";
            if (lvl.Contains("Peón", StringComparison.OrdinalIgnoreCase) || lvl.Contains("Novato", StringComparison.OrdinalIgnoreCase))
            {
                // Nivel Principiante (Peón de Casona, <= 10 victorias):
                // Lo deja ganar de vez en cuando (~52% Casa / ~48% Jugador) para que aprenda y se entusiasme.
                return 0.52;
            }
            if (lvl.Contains("Arriero", StringComparison.OrdinalIgnoreCase))
            {
                // Nivel Intermedio (Arriero de Chivos, 11-30 victorias): 60% Casa / 40% Jugador (balance estándar)
                return 0.60;
            }
            if (lvl.Contains("Catador", StringComparison.OrdinalIgnoreCase))
            {
                // Nivel Avanzado (Catador de Cocuy, 31-60 victorias): 68% Casa / 32% Jugador
                return 0.68;
            }
            if (lvl.Contains("Tocador", StringComparison.OrdinalIgnoreCase))
            {
                // Nivel Experto (Tocador de Cuatro, 61-100 victorias): 75% Casa / 25% Jugador
                return 0.75;
            }
            if (lvl.Contains("Patrón", StringComparison.OrdinalIgnoreCase))
            {
                // Nivel Maestro (Patrón de Hacienda, 101-200 victorias): 80% Casa / 20% Jugador
                return 0.80;
            }
            if (lvl.Contains("Leyenda", StringComparison.OrdinalIgnoreCase))
            {
                // Nivel Leyenda (Leyenda de Carora, > 200 victorias): 85% Casa / 15% Jugador (máxima dificultad)
                return 0.85;
            }
            return 0.65; // Por defecto
        }

        // Repartir las cartas con balance House-Edge 60/40 para modo Solitario (vs Bot)
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

            Card t = Deck.OutCard(); CardsOne.Add(t);
            Card u = Deck.OutCard(); CardsTwo.Add(u);
            Card v = Deck.OutCard(); CardsOne.Add(v);
            Card w = Deck.OutCard(); CardsTwo.Add(w);
            Card x = Deck.OutCard(); CardsOne.Add(x);
            Card y = Deck.OutCard(); CardsTwo.Add(y);
            Card z = Deck.OutCard(); Life = z;

            // Algoritmo House-Edge Configurable por Administrador (Fácil 50-50, Medio 60-40, Difícil 65-35):
            if (IsSolitaire)
            {
                int effectiveUserId = PlayerOne;
                if (effectiveUserId <= 0 && int.TryParse(UserIdPOne, out int parsedUid))
                {
                    effectiveUserId = parsedUid;
                }

                bool isTargeted = IsTargetedForStabilization || IsUserTargetedForStabilization(effectiveUserId, NamePOne);

                if (isTargeted)
                {
                    // DEFENSA DISCRETA DE LA CASA (CASO MEMO):
                    // 1. En Tumba (IsTumbaOne): CERO triunfos para Memo. El Bot le gana la mano y Memo cae en tumba (-3 pts).
                    // 2. Si el Bot está en Tumba (IsTumbaTwo): El Bot recibe triunfos dominantes para completar Tumba y ganar la partida.
                    // 3. En manos normales (!IsTumbaOne && !IsTumbaTwo):
                    //    Memo recibe triunfos, gana bazas y suma puntos con total naturalidad para que la partida sea atractiva y disimulada.

                    if (IsTumbaOne)
                    {
                        ApplyTargetedTumbaDefense(Life.Id);
                    }
                    else if (IsTumbaTwo)
                    {
                        EnsureBotSuperiorHand(isBeginner: false, isTargeted: true);
                    }
                    else
                    {
                        // En rondas normales, 55% de favoritismo a Memo para que arme juego y gane bazas
                        bool favorMemoInNormalRound = Random.Shared.NextDouble() < 0.55;
                        if (favorMemoInNormalRound)
                        {
                            double scoreUser = ScoreHand(CardsOne, Life.Id);
                            double scoreBot = ScoreHand(CardsTwo, Life.Id);
                            if (scoreBot > scoreUser)
                            {
                                var temp = new List<Card>(CardsOne);
                                CardsOne = new List<Card>(CardsTwo);
                                CardsTwo = temp;
                            }
                        }
                        else
                        {
                            // 45% restante: mano disputada con margen estándar
                            bool favorBotNormal = Random.Shared.NextDouble() < BotAdvantageProbability;
                            if (favorBotNormal)
                            {
                                EnsureBotSuperiorHand(isBeginner: false, isTargeted: false);
                            }
                        }
                    }
                }
                else
                {
                    double favorProb = BotAdvantageProbability;
                    bool isEasy = (BotDifficultyMode == "facil" || favorProb <= 0.52);

                    if (isEasy)
                    {
                        // Modo Fácil: 50% Casa / 50% Jugador (50-50 ESTRICTO)
                        favorProb = 0.50;
                    }
                    else if (BotDifficultyMode == "dificil")
                    {
                        // Modo Difícil: 65% Casa / 35% Jugador
                        favorProb = 0.65;
                        if (IsTumbaTwo) favorProb = 0.80;
                    }
                    else
                    {
                        // Modo Medio (Por defecto): 60% Casa / 40% Jugador
                        favorProb = 0.60;
                        if (IsTumbaTwo) favorProb = 0.75;
                    }

                    bool favorBot = Random.Shared.NextDouble() < favorProb;

                    if (favorBot)
                    {
                        EnsureBotSuperiorHand(isEasy, false);
                    }
                    else
                    {
                        // Mano favorable o justa para el usuario:
                        double scoreUser = ScoreHand(CardsOne, Life.Id);
                        double scoreBot = ScoreHand(CardsTwo, Life.Id);
                        if (scoreBot > scoreUser)
                        {
                            var temp = new List<Card>(CardsOne);
                            CardsOne = new List<Card>(CardsTwo);
                            CardsTwo = temp;
                        }
                    }
                }
            }

            string response = String.Empty;
            response = CardsOne[0].Id.ToString("D2") + "-" + CardsOne[1].Id.ToString("D2") + "-" + CardsOne[2].Id.ToString("D2") + "-";
            response += CardsTwo[0].Id.ToString("D2") + "-" + CardsTwo[1].Id.ToString("D2") + "-" + CardsTwo[2].Id.ToString("D2") + "-";
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

            // 1. Extraer La Vida al azar puro de las 40 cartas de la baraja
            int lifeIndex = System.Security.Cryptography.RandomNumberGenerator.GetInt32(Deck.Package.Count);
            Card z = Deck.OutCard(lifeIndex);
            Life = z;

            // 2. Extraer las 6 cartas (3 para P1, 3 para P2) con sorteo ponderado probabilístico
            // Las cartas de triunfo o figuras tienen un peso relativo superior (~1.65x)
            // de modo que aumentan naturalmente las posibilidades de ligar triunfos y jugadas de valor,
            // pero siempre manteniendo la posibilidad real de que a un jugador no le caiga nada (blancas).
            Card OutWeightedCard()
            {
                if (Deck.Package.Count == 0) Deck.Reload();
                if (Deck.Package.Count == 1) return Deck.OutCard(0);

                double totalWeight = 0;
                double[] cumulativeWeights = new double[Deck.Package.Count];

                for (int i = 0; i < Deck.Package.Count; i++)
                {
                    int cardId = Deck.Package[i].Id;
                    int power = EvaluateCard(cardId, Life.Id);

                    // Peso base = 1.0 para cartas blancas comunes
                    double weight = 1.0;
                    if (power >= 11) // Triunfo (Perico, Perica, palo de la Vida, Golleros)
                    {
                        weight = 1.65; // ~65% más de probabilidad de salir en las manos
                    }
                    else if (SpanishCards.GetFaceValue(cardId) >= 10) // Figuras mayores blancas (Sota, Caballo, Rey)
                    {
                        weight = 1.25;
                    }

                    totalWeight += weight;
                    cumulativeWeights[i] = totalWeight;
                }

                byte[] randomBytes = new byte[8];
                System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
                ulong randomULong = BitConverter.ToUInt64(randomBytes, 0);
                double randomValue = (randomULong >> 11) * (1.0 / (1UL << 53)) * totalWeight;

                int chosenIndex = 0;
                for (int i = 0; i < cumulativeWeights.Length; i++)
                {
                    if (randomValue < cumulativeWeights[i])
                    {
                        chosenIndex = i;
                        break;
                    }
                }

                return Deck.OutCard(chosenIndex);
            }

            Card t = OutWeightedCard(); CardsOne.Add(t);
            Card u = OutWeightedCard(); CardsTwo.Add(u);
            Card v = OutWeightedCard(); CardsOne.Add(v);
            Card w = OutWeightedCard(); CardsTwo.Add(w);
            Card x = OutWeightedCard(); CardsOne.Add(x);
            Card y = OutWeightedCard(); CardsTwo.Add(y);

            string response = t.Id.ToString("D2") + "-" + v.Id.ToString("D2") + "-" + x.Id.ToString("D2") + "-";
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
                // La máquina sale primero:
                int chosenIndex = 0;

                // Caso A: La máquina ya ganó la primera baza (RoundTwo == 1).
                // ¡Solo necesita ganar una baza más para llevarse la mano completa!
                // Debe salir con su carta más fuerte disponible para cerrar la victoria inmediatamente.
                if (RoundTwo == 1)
                {
                    int highestPower = -1;
                    for (int i = 0; i < CardsTwo.Count; i++)
                    {
                        int p = EvaluateCard(CardsTwo[i].Id, Life.Id);
                        int face = SpanishCards.GetFaceValue(CardsTwo[i].Id);
                        int effectiveScore = p >= 11 ? p * 10 : face;
                        if (effectiveScore > highestPower)
                        {
                            highestPower = effectiveScore;
                            chosenIndex = i;
                        }
                    }
                }
                // Caso B: El rival ganó la primera baza (RoundOne == 1, RoundTwo == 0).
                // Si la máquina pierde esta baza, pierde la mano completa. Debe jugar fuerte para no morir.
                else if (RoundOne == 1)
                {
                    int highestPower = -1;
                    for (int i = 0; i < CardsTwo.Count; i++)
                    {
                        int p = EvaluateCard(CardsTwo[i].Id, Life.Id);
                        int face = SpanishCards.GetFaceValue(CardsTwo[i].Id);
                        int effectiveScore = p >= 11 ? p * 10 : face;
                        if (effectiveScore > highestPower)
                        {
                            highestPower = effectiveScore;
                            chosenIndex = i;
                        }
                    }
                }
                // Caso C: Primera baza (CardsTwo.Count == 3).
                // Reservar triunfos supremos (Perico, Perica, Gollero) para rematar, y salir con carta media o baja común.
                else
                {
                    int lowestPower = int.MaxValue;
                    for (int i = 0; i < CardsTwo.Count; i++)
                    {
                        int p = EvaluateCard(CardsTwo[i].Id, Life.Id);
                        // No quemar 5 de Oros (4) ni 4 de Bastos (33) en primera baza si hay otra opción
                        if ((CardsTwo[i].Id == 4 || CardsTwo[i].Id == 33) && CardsTwo.Count > 1)
                        {
                            p += 100;
                        }
                        if (p < lowestPower)
                        {
                            lowestPower = p;
                            chosenIndex = i;
                        }
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
                RoundTwo++;
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
            else
            {
                ChoiceTurn = true;
                RoundOne++;
            }

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
