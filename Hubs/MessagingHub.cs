using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using PericonAPI.Classes;
using PericonAPI.Data;
using PericonAPI.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PericonAPI.Hubs
{
    public class GamePlayer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int Coins { get; set; }
        public bool Active { get; set; }

        public GamePlayer() 
        {
            Id = ""; Name = ""; Email = ""; Coins = 0; Active = false;
        }

        public GamePlayer(string Id, string Name, string Email)
        {
            this.Id = Id; this.Name = Name; this.Email = Email; this.Coins = 0; Active = true;
        }

        public override string ToString()
        {
            return this.Id + " " + this.Name + " " + this.Email + " " + this.Coins.ToString();
        }

    }

    public class Player
    {
        public string Name { get; set; }
        public string Id { get; set; }

        public Player(string name, string id)
        {
            Name = name; Id = id;
        }
    }

    public class GameMessage
    {
        public int game { get; set; }
        public int order { get; set; }
        public string content { get; set; } = string.Empty;

        public override string ToString()
        {
            string output = game.ToString() + ":" + order.ToString() + ":" + content;
            return output;
        }
    }

    public static class GameLogger
    {
        private static readonly object _fileLock = new object();
        private static readonly string _logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        private static readonly string _logPath = Path.Combine(AppContext.BaseDirectory, "logs", "game_events.log");

        public static void Log(int gameId, string action, string details)
        {
            string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [Game:{gameId}] [{action}] {details}";
            Console.WriteLine(line);
            try
            {
                lock (_fileLock)
                {
                    if (!Directory.Exists(_logDir)) Directory.CreateDirectory(_logDir);
                    File.AppendAllText(_logPath, line + Environment.NewLine);
                }
            }
            catch { }
        }
    }

    public class MessagingHub : Hub
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<MessagingHub> _hubContext;
        private static IHubContext<MessagingHub>? _staticHubContext;

        public static void SetHubContext(IHubContext<MessagingHub> context)
        {
            _staticHubContext = context;
        }

        public MessagingHub(IServiceScopeFactory scopeFactory, IHubContext<MessagingHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _staticHubContext = hubContext;
        }

        private static List<GamePlayOneVsOne> games = new List<GamePlayOneVsOne>(); 
        private static List<GamePlayTwoVsTwo> games2vs2 = new List<GamePlayTwoVsTwo>(); 

        private GamePlayOneVsOne example = new GamePlayOneVsOne(1);

        private static List<GamePlayer> users = new List<GamePlayer>();

        public class MatchQueueItem
        {
            public string ConnectionId { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string PlayerName { get; set; } = string.Empty;
            public string AvatarUrl { get; set; } = string.Empty;
            public string Mode { get; set; } = "1 vs 1";
            public int Bet { get; set; } = 10;
            public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
        }

        public class Seat2v2
        {
            public int SeatIndex { get; set; } // 0 = P1 (Azul), 1 = P2 (Rojo), 2 = P3 (Azul), 3 = P4 (Rojo)
            public string ConnectionId { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string AvatarUrl { get; set; } = string.Empty;
            public int Team { get; set; } // 1 or 2
            public string Role { get; set; } = string.Empty;
            public bool IsReady { get; set; } = true;
            public bool IsConnected { get; set; } = true;
            public DateTime? DisconnectedAt { get; set; }
        }

        public class PlayedCard2v2Dto
        {
            public int SeatIndex { get; set; }
            public int CardId { get; set; }
        }

        public class Room2v2Session
        {
            public string RoomName { get; set; } = string.Empty;
            public int Bet { get; set; } = 100;
            public int GameId { get; set; } = 0;
            public List<Seat2v2> Seats { get; set; } = new List<Seat2v2>();
            public bool GameStarted { get; set; } = false;
            public bool IsStarting { get; set; } = false;
            public DateTime LastHandDealtAt { get; set; } = DateTime.MinValue;
            public string CurrentInitHand { get; set; } = string.Empty;
            public int PointsTeam1 { get; set; } = 0;
            public int PointsTeam2 { get; set; } = 0;
            public int TricksTeam1 { get; set; } = 0;
            public int TricksTeam2 { get; set; } = 0;
            public int CurrentStake { get; set; } = 1;
            public int PendingStake { get; set; } = 0;
            public int StakeAskerSeat { get; set; } = -1;
            public int StakeAskerTeam { get; set; } = 0;
            public int LastStakeTeam { get; set; } = 0;
            public int CurrentTurn { get; set; } = 0;
            public int LeadPlayer { get; set; } = 0;
            public List<PlayedCard2v2Dto> CurrentTrick { get; set; } = new List<PlayedCard2v2Dto>();
            public List<PlayedCard2v2Dto> HandHistoryCards { get; set; } = new List<PlayedCard2v2Dto>();
            public int HandCount { get; set; } = 0;
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

            // Tumba y Tumba de para atrás oficial 2 vs 2
            public bool IsTumbaTeam1 { get; set; } = false;
            public bool IsTumbaTeam2 { get; set; } = false;
            public bool IsTumbaDeParaAtrasTeam1 { get; set; } = false;
            public bool IsTumbaDeParaAtrasTeam2 { get; set; } = false;
            public bool IsTumbaDecisionPending { get; set; } = false;
            public bool IsHandResolving { get; set; } = false;
            public int LastHandStarter { get; set; } = 0;
            public bool IsGameOver { get; set; } = false;
            public int WinningTeam { get; set; } = 0;

            public void UpdateTumbaStatus(int oldT1 = -1, int oldT2 = -1)
            {
                if (oldT1 != -1)
                {
                    if (oldT1 >= 9 && PointsTeam1 == 8) IsTumbaDeParaAtrasTeam1 = true;
                    else if (PointsTeam1 != 8) IsTumbaDeParaAtrasTeam1 = false;
                }
                IsTumbaTeam1 = PointsTeam1 >= 9 || (IsTumbaDeParaAtrasTeam1 && PointsTeam1 == 8);

                if (oldT2 != -1)
                {
                    if (oldT2 >= 9 && PointsTeam2 == 8) IsTumbaDeParaAtrasTeam2 = true;
                    else if (PointsTeam2 != 8) IsTumbaDeParaAtrasTeam2 = false;
                }
                IsTumbaTeam2 = PointsTeam2 >= 9 || (IsTumbaDeParaAtrasTeam2 && PointsTeam2 == 8);
            }
        }

        private static Dictionary<string, Room2v2Session> rooms2v2 = new Dictionary<string, Room2v2Session>();
        private static readonly object rooms2v2Lock = new object();

        public class Room1v1Session
        {
            public string RoomName { get; set; } = string.Empty;
            public int Bet { get; set; } = 10;
            public List<Seat2v2> Seats { get; set; } = new List<Seat2v2>();
            public bool GameStarted { get; set; } = false;
            public int GameId { get; set; } = 0;
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        }

        private static Dictionary<string, Room1v1Session> rooms1v1 = new Dictionary<string, Room1v1Session>();
        private static readonly object rooms1v1Lock = new object();

        private static Dictionary<string, GamePlayOneVsOne> solitaireSessions = new Dictionary<string, GamePlayOneVsOne>();
        private static readonly object solitaireLock = new object();

        private static List<MatchQueueItem> matchmakingQueue = new List<MatchQueueItem>();
        private static readonly object queueLock = new object();

        private static Dictionary<int, DateTime> _lastHandChangeTime1vs1 = new Dictionary<int, DateTime>();
        private static readonly object _handChangeLock1vs1 = new object();

        private GameOrder sentence = new GameOrder();

        private static int counter = 0;

        public override async Task OnConnectedAsync()
        {
            var id = Context.ConnectionId;
            counter++;
            GamePlayer q = new GamePlayer(id.ToString(), "Jugador-" + counter.ToString(), "buzon@correo.com");
            users.Add(q);
            Console.WriteLine($"Cliente: {q} y Cantidad de elementos en Users: {users.Count}");
            await base.OnConnectedAsync();
        }

        // 
        public GamePlayer SearchPlayer(string id)
        {
            GamePlayer q = new GamePlayer();
            foreach(GamePlayer player in users) 
            {
                if (player.Id.Equals(id))
                {
                    q = player;
                    break;
                }
            }
            return q;
        }

        public async Task SetPlayer()
        {
            Console.WriteLine($"SetPlayer. Cliente: {Context.ConnectionId}");
            GamePlayer user = new GamePlayer();
            user.Name = "nulo";
            foreach(GamePlayer x in users)
            {
                if (x.Id.Equals(Context.ConnectionId))
                {
                    Console.WriteLine("Está en la lista!");
                    user = x;
                    break;
                }
            }
            await Clients.Client(user.Id).SendAsync("GetPlayer", user);
        }

        public async Task IdentifyPlayer(string playerName, string email, int coins)
        {
            Console.WriteLine($"IdentifyPlayer. Cliente: {Context.ConnectionId}, Nombre: {playerName}");
            int realCoins = coins;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var dbUser = db.Users.FirstOrDefault(u =>
                        (!string.IsNullOrEmpty(playerName) && u.Username.ToLower() == playerName.ToLower()) ||
                        (!string.IsNullOrEmpty(email) && u.Email.ToLower() == email.ToLower()));
                    if (dbUser != null)
                    {
                        realCoins = dbUser.Coins;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IdentifyPlayer DB check error] {ex.Message}");
            }

            GamePlayer? toNotify = null;
            lock (users)
            {
                foreach (var user in users)
                {
                    if (user.Id.Equals(Context.ConnectionId))
                    {
                        if (!string.IsNullOrWhiteSpace(playerName)) user.Name = playerName;
                        if (!string.IsNullOrWhiteSpace(email)) user.Email = email;
                        user.Coins = Math.Max(0, realCoins);
                        toNotify = user;
                        break;
                    }
                }

                if (toNotify == null)
                {
                    toNotify = new GamePlayer(Context.ConnectionId, playerName ?? "Jugador", email ?? "")
                    {
                        Coins = Math.Max(0, realCoins),
                        Active = true
                    };
                    users.Add(toNotify);
                }
            }

            if (toNotify != null)
            {
                await Clients.Client(toNotify.Id).SendAsync("GetPlayer", toNotify);
            }
        }

        public async Task GetListPlayers()
        {
            Console.WriteLine($"GetListPlayers. Cliente: {Context.ConnectionId}");
            List<Player> listuser = new List<Player>();
            foreach (GamePlayer x in users)
            {
                if (x.Id.Equals(Context.ConnectionId) == false)
                {
                    Player w = new Player(x.Name, x.Id);
                    listuser.Add(w);
                }
            }
            await Clients.Client(Context.ConnectionId).SendAsync("RetListPlayers", listuser);
        }

        public async Task AskInvitePlayer(GameMessage move)
        {
            Console.WriteLine($"AskInvitePlayer. Cliente: {Context.ConnectionId}, Orden: {move}");
            GamePlayer dataplay = SearchPlayer(Context.ConnectionId);
            await Clients.Client(move.content).SendAsync("InvitedGame", dataplay);
        }

        public async Task Ask369Game(GameMessage move)
        {
            Console.WriteLine($"Ask369Game. Cliente: {Context.ConnectionId}, Orden: {move}");
            GamePlayer dataplay = SearchPlayer(Context.ConnectionId);
            int numg = FindGame1vs1(move.game);
            if (numg < 0 || numg >= games.Count) return;

            // En Tumba no está permitido pedir
            if (games[numg].IsTumbaOne || games[numg].IsTumbaTwo || games[numg].PointsOne >= 9 || games[numg].PointsTwo >= 9 ||
                (games[numg].IsTumbaDeParaAtrasOne && games[numg].PointsOne == 8) || (games[numg].IsTumbaDeParaAtrasTwo && games[numg].PointsTwo == 8))
            {
                Console.WriteLine("[Ask369Game] Pedir bloqueado porque un jugador está en Tumba.");
                return;
            }

            bool callerIsP1 = (Context.ConnectionId == games[numg].IdPOne);
            int callerNum = callerIsP1 ? 1 : 2;

            if (games[numg].LastStakeAsker == callerNum)
            {
                Console.WriteLine($"[Ask369Game] Pedir bloqueado: el jugador {callerNum} ya pidió previamente sin revire.");
                return;
            }
            games[numg].LastStakeAsker = callerNum;
            string sentto = callerIsP1 ? games[numg].IdPTwo : games[numg].IdPOne;

            GameMessage data = new GameMessage();
            switch (move.order)
            {
                case 70:    // 70 .. 72 POne, 73 .. 75 PTwo
                case 73:
                    games[numg].Ask369 = 1;
                    data.content = "1";
                    break;
                case 71:
                case 74:
                    games[numg].Ask369 = 4;
                    data.content = "4";
                    break;
                case 72:
                case 75:
                    games[numg].Ask369 = 7;
                    data.content = "7";
                    break;
            }
            data.game = move.game;
            data.order = 76;
            if (!string.IsNullOrEmpty(sentto))
            {
                await Clients.Client(sentto).SendAsync("Asked369Game", data);
            }
            else
            {
                await Clients.OthersInGroup($"game1vs1_{move.game}").SendAsync("Asked369Game", data);
            }
        }

        public async Task Answer369Game(GameMessage move)
        {
            Console.WriteLine($"Answer369Game. Cliente: {Context.ConnectionId}, Orden: {move}");
            int numg = FindGame1vs1(move.game);
            if (numg < 0 || numg >= games.Count) return;

            string[] daticos = move.content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (daticos.Length < 2) return;
            int chosen = int.Parse(daticos[daticos.Length - 1]);

            bool callerIsP1 = (Context.ConnectionId == games[numg].IdPOne);
            string ownto = Context.ConnectionId;
            string sentto = callerIsP1 ? games[numg].IdPTwo : games[numg].IdPOne;

            GameMessage data = new GameMessage();
            data.game = move.game;
            data.order = 77;

            int oldP1 = games[numg].PointsOne;
            int oldP2 = games[numg].PointsTwo;

            switch (chosen)
            {
                case 2: // Acepta 3
                    games[numg].CurrentStake = 3;
                    games[numg].Ask369 = 3;
                    games[numg].LastStakeAsker = callerIsP1 ? 2 : 1;
                    data.content = $"2 {games[numg].PointsOne} {games[numg].PointsTwo}";
                    await Clients.Client(ownto).SendAsync("EndAsk369Round", data);
                    if (!string.IsNullOrEmpty(sentto)) await Clients.Client(sentto).SendAsync("Answered369Game", data);
                    else await Clients.OthersInGroup($"game1vs1_{move.game}").SendAsync("Answered369Game", data);
                    break;
                case 3: // Rechaza 3 -> Quien pidió 3 (rival de caller) gana 1 punto
                    games[numg].Ask369 = -1;
                    games[numg].LastStakeAsker = 0;
                    games[numg].RoundOne = 0;
                    games[numg].RoundTwo = 0;
                    if (callerIsP1) games[numg].PointsTwo += 1;
                    else games[numg].PointsOne += 1;
                    games[numg].UpdateTumbaStatus(oldP1, oldP2);
                    data.content = $"3 {games[numg].PointsOne} {games[numg].PointsTwo}";
                    await Clients.Client(ownto).SendAsync("EndAsk369Round", data);
                    if (!string.IsNullOrEmpty(sentto)) await Clients.Client(sentto).SendAsync("Answered369Game", data);
                    else await Clients.OthersInGroup($"game1vs1_{move.game}").SendAsync("Answered369Game", data);
                    break;
                case 4: // Revira a 6 (propone 6 a sentto)
                    games[numg].LastStakeAsker = callerIsP1 ? 1 : 2;
                    data.order = 76;
                    data.content = "4";
                    data.game = move.game;
                    if (!string.IsNullOrEmpty(sentto)) await Clients.Client(sentto).SendAsync("Asked369Game", data);
                    else await Clients.OthersInGroup($"game1vs1_{move.game}").SendAsync("Asked369Game", data);
                    break;
                case 5: // Acepta 6
                    games[numg].CurrentStake = 6;
                    games[numg].Ask369 = 6;
                    games[numg].LastStakeAsker = callerIsP1 ? 2 : 1;
                    data.content = $"5 {games[numg].PointsOne} {games[numg].PointsTwo}";
                    await Clients.Client(ownto).SendAsync("EndAsk369Round", data);
                    if (!string.IsNullOrEmpty(sentto)) await Clients.Client(sentto).SendAsync("Answered369Game", data);
                    else await Clients.OthersInGroup($"game1vs1_{move.game}").SendAsync("Answered369Game", data);
                    break;
                case 6: // Rechaza 6 -> Quien propuso 6 gana las 3 piedras ya pactadas
                    games[numg].Ask369 = -1;
                    games[numg].LastStakeAsker = 0;
                    games[numg].RoundOne = 0;
                    games[numg].RoundTwo = 0;
                    if (callerIsP1) games[numg].PointsTwo += 3;
                    else games[numg].PointsOne += 3;
                    games[numg].UpdateTumbaStatus(oldP1, oldP2);
                    data.content = $"6 {games[numg].PointsOne} {games[numg].PointsTwo}";
                    await Clients.Client(ownto).SendAsync("EndAsk369Round", data);
                    if (!string.IsNullOrEmpty(sentto)) await Clients.Client(sentto).SendAsync("Answered369Game", data);
                    else await Clients.OthersInGroup($"game1vs1_{move.game}").SendAsync("Answered369Game", data);
                    break;
                case 7: // Revira a 9 (propone 9 a sentto)
                    games[numg].LastStakeAsker = callerIsP1 ? 1 : 2;
                    data.order = 76;
                    data.content = "7";
                    data.game = move.game;
                    if (!string.IsNullOrEmpty(sentto)) await Clients.Client(sentto).SendAsync("Asked369Game", data);
                    else await Clients.OthersInGroup($"game1vs1_{move.game}").SendAsync("Asked369Game", data);
                    break;
                case 8: // Acepta 9
                    games[numg].CurrentStake = 9;
                    games[numg].Ask369 = 9;
                    games[numg].LastStakeAsker = callerIsP1 ? 2 : 1;
                    data.content = $"8 {games[numg].PointsOne} {games[numg].PointsTwo}";
                    await Clients.Client(ownto).SendAsync("EndAsk369Round", data);
                    if (!string.IsNullOrEmpty(sentto)) await Clients.Client(sentto).SendAsync("Answered369Game", data);
                    else await Clients.OthersInGroup($"game1vs1_{move.game}").SendAsync("Answered369Game", data);
                    break;
                case 9: // Rechaza 9 -> Quien propuso 9 gana las 6 piedras ya pactadas
                    games[numg].Ask369 = -1;
                    games[numg].LastStakeAsker = 0;
                    games[numg].RoundOne = 0;
                    games[numg].RoundTwo = 0;
                    if (callerIsP1) games[numg].PointsTwo += 6;
                    else games[numg].PointsOne += 6;
                    games[numg].UpdateTumbaStatus(oldP1, oldP2);
                    data.content = $"9 {games[numg].PointsOne} {games[numg].PointsTwo}";
                    await Clients.Client(ownto).SendAsync("EndAsk369Round", data);
                    if (!string.IsNullOrEmpty(sentto)) await Clients.Client(sentto).SendAsync("Answered369Game", data);
                    else await Clients.OthersInGroup($"game1vs1_{move.game}").SendAsync("Answered369Game", data);
                    break;
            }
        }

        public async Task AnswerInvitePlayer(GameMessage move)
        {
            Console.WriteLine($"AnswerInvitePlayer. Cliente: {Context.ConnectionId}, Orden: {move}");
            GamePlayer dataplay = SearchPlayer(Context.ConnectionId);
            GameMessage data = new GameMessage();
            if (move.order == 95)
            {
                data.content = dataplay.Id;
                data.game = 0;
                data.order = 97;
                await Clients.Client(move.content).SendAsync("InviteGame", data);
            } else
            {
                data.content = dataplay.Id;
                data.game = 0;
                data.order = 98;
                await Clients.Client(move.content).SendAsync("InviteGame", data);
            }
        }

        public async Task SendQuickPhrase(string roomName, string playerName, string phrase, int seatIndex)
        {
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(phrase)) return;
            Console.WriteLine($"[SendQuickPhrase] Sala: {roomName}, Jugador: {playerName}, Frase: {phrase}");
            await Clients.Group(roomName).SendAsync("ReceiveQuickPhrase", new
            {
                playerName,
                phrase,
                seatIndex,
                senderConnectionId = Context.ConnectionId
            });
        }

        // Métodos de desarrollo de los juegos a modo 1 vs 1

        public GamePlayer GetPlayerData(string _id)
        {
            GamePlayer x = new GamePlayer();
            foreach(GamePlayer w in users)
                if (w.Id == _id)
                {
                    x = w;
                    break;
                }
            return x;
        }

        // Se define el juego

        public async Task SetGame1vs1(GameMessage move) // order = 90
        {
            Console.WriteLine($"SetGame1vs1. Cliente: {Context.ConnectionId}, Orden: {move}");
            string POne = Context.ConnectionId;
            string PTwo = move.content;
            GamePlayOneVsOne newgame = new GamePlayOneVsOne(POne, PTwo);
            GamePlayer QOne = GetPlayerData(POne);
            GamePlayer QTwo = GetPlayerData(PTwo);
            string name1 = !string.IsNullOrEmpty(QOne.Name) && !QOne.Name.StartsWith("Jugador-") ? QOne.Name : (QOne.Name ?? "Jugador 1");
            string name2 = !string.IsNullOrEmpty(QTwo.Name) && !QTwo.Name.StartsWith("Jugador-") ? QTwo.Name : (QTwo.Name ?? "Jugador 2");
            newgame.NamePOne = name1;
            newgame.NamePTwo = name2;
            newgame.RoomName = $"match-{newgame.Id}";
            newgame.IsFriendlyRoom = false;

            // Sorteo de mano inicial 50% / 50%
            Random rng = new Random();
            int startingPlayer = rng.Next(2) == 0 ? 1 : 2;
            newgame.HandStarter = startingPlayer;
            newgame.PlayerTurn = (startingPlayer == 1);
            newgame.HandCount = 1;

            newgame.Deck.RandomCards();
            newgame.Id = newgame.GenerateSeed(games);
            newgame.ShuffleCards_1vs1();
            games.Add(newgame);
            GameMessage sentence = new GameMessage();
            sentence.game = newgame.Id;
            sentence.order = 99;
            string previewcontent = $"{POne}|{name1}|{PTwo}|{name2}|";
            sentence.content = previewcontent + "1";
            Console.WriteLine($"Juego creado: {newgame.Id}, Mano inicial: {newgame.InitHand}, Inicia P{startingPlayer}");
            await Clients.Client(POne).SendAsync("ReadyToGame1vs1", sentence);
            sentence.content = previewcontent + "0";
            await Clients.Client(PTwo).SendAsync("ReadyToGame1vs1", sentence);
        }


        public Task LogClientEvent(GameMessage move)
        {
            GameLogger.Log(move.game, $"ClientReport:{Context.ConnectionId}", $"Order:{move.order} - {move.content}");
            return Task.CompletedTask;
        }

        public async Task ChangeGame1vs1(GameMessage move) // order = 90
        {
            GameLogger.Log(move.game, "ChangeGame1vs1", $"Cliente: {Context.ConnectionId}, Orden: {move.order}");
            int numg = FindGame1vs1(move.game);
            if (numg < 0 || numg >= games.Count)
            {
                GameLogger.Log(move.game, "ChangeGame1vs1", $"WARNING: Juego {move.game} no encontrado o inactivo.");
                return;
            }

            // Debounce para evitar ejecuciones dobles si ambos clientes llaman ChangeGame1vs1 simultáneamente
            lock (_handChangeLock1vs1)
            {
                if (_lastHandChangeTime1vs1.TryGetValue(move.game, out DateTime lastChange) &&
                    (DateTime.UtcNow - lastChange).TotalMilliseconds < 2500)
                {
                    GameLogger.Log(move.game, "ChangeGame1vs1", "Ignorando llamada duplicada a ChangeGame1vs1 por debounce.");
                    return;
                }
                _lastHandChangeTime1vs1[move.game] = DateTime.UtcNow;
            }

            // Alternancia estricta de la salida ("una y una")
            games[numg].HandStarter = (games[numg].HandStarter == 1) ? 2 : 1;
            games[numg].PlayerTurn = (games[numg].HandStarter == 1);
            games[numg].HandCount++;

            games[numg].Deck.RandomCards();
            games[numg].ShuffleCards_1vs1();
            games[numg].CurrentStake = 1;
            games[numg].LastStakeAsker = 0;
            games[numg].Ask369 = 0;
            games[numg].RoundOne = 0;
            games[numg].RoundTwo = 0;
            string POne = games[numg].IdPOne;
            string PTwo = games[numg].IdPTwo;
            string PThree = games[numg].InitHand;
            GameMessage sentence = new GameMessage();
            sentence.game = move.game;
            sentence.order = 87;
            string previewcontent = PThree + "-";
            string PFour = (games[numg].HandStarter == 1 ? "1" : "0");
            string PFive = (games[numg].HandStarter == 2 ? "1" : "0");
            string PScore = $"-{games[numg].PointsOne}-{games[numg].PointsTwo}";
            sentence.content = previewcontent + PFour + PScore;
            Console.WriteLine($"[ChangeGame1vs1] Mano {games[numg].HandCount}: Salida corresponde a P{games[numg].HandStarter}. Enviando a POne ({POne})");
            if (!string.IsNullOrEmpty(POne)) await Clients.Client(POne).SendAsync("setChangeHand", sentence);

            sentence.content = previewcontent + PFive + PScore;
            Console.WriteLine($"[ChangeGame1vs1] Mano {games[numg].HandCount}: Salida corresponde a P{games[numg].HandStarter}. Enviando a PTwo ({PTwo})");
            if (!string.IsNullOrEmpty(PTwo)) await Clients.Client(PTwo).SendAsync("setChangeHand", sentence);

            // Respaldo a la sala SignalR
            await Clients.Group($"game1vs1_{move.game}").SendAsync("GameHandUpdated1vs1", new
            {
                game = move.game,
                handCards = PThree,
                handStarter = games[numg].HandStarter,
                pointsOne = games[numg].PointsOne,
                pointsTwo = games[numg].PointsTwo
            });
        }

        public async Task RequestNewHand1vs1(int gameId)
        {
            GameLogger.Log(gameId, "RequestNewHand1vs1", $"Invocado por Cliente: {Context.ConnectionId}");
            int numg = FindGame1vs1(gameId);
            if (numg < 0 || numg >= games.Count) return;

            var move = new GameMessage { game = gameId, order = 90, content = "" };
            await ChangeGame1vs1(move);
        }

        public async Task RequestRevancha1vs1(int gameId, string requesterName)
        {
            GameLogger.Log(gameId, "RequestRevancha1vs1", $"Invocado por Cliente: {Context.ConnectionId}, Nombre: {requesterName}");
            int numg = FindGame1vs1(gameId);
            if (numg < 0 || numg >= games.Count) return;

            string caller = Context.ConnectionId;
            bool callerIsP1 = (caller == games[numg].IdPOne);
            string targetOpp = callerIsP1 ? games[numg].IdPTwo : games[numg].IdPOne;
            string reqName = !string.IsNullOrWhiteSpace(requesterName)
                ? requesterName
                : (callerIsP1 ? (!string.IsNullOrEmpty(games[numg].NamePOne) ? games[numg].NamePOne : "Tu rival")
                              : (!string.IsNullOrEmpty(games[numg].NamePTwo) ? games[numg].NamePTwo : "Tu rival"));

            if (string.IsNullOrEmpty(targetOpp))
            {
                await Clients.Caller.SendAsync("RevanchaRejected1vs1", new { responderName = "El contrincante (desconectado)" });
                return;
            }

            await Clients.Client(targetOpp).SendAsync("RevanchaRequested1vs1", new
            {
                gameId = gameId,
                requesterName = reqName
            });
        }

        public async Task AnswerRevancha1vs1(int gameId, bool accepted, string responderName)
        {
            GameLogger.Log(gameId, "AnswerRevancha1vs1", $"Invocado por Cliente: {Context.ConnectionId}, Aceptado: {accepted}, Nombre: {responderName}");
            int numg = FindGame1vs1(gameId);
            if (numg < 0 || numg >= games.Count) return;

            string caller = Context.ConnectionId;
            bool callerIsP1 = (caller == games[numg].IdPOne);
            string targetOpp = callerIsP1 ? games[numg].IdPTwo : games[numg].IdPOne;
            string respName = !string.IsNullOrWhiteSpace(responderName)
                ? responderName
                : (callerIsP1 ? (!string.IsNullOrEmpty(games[numg].NamePOne) ? games[numg].NamePOne : "Tu rival")
                              : (!string.IsNullOrEmpty(games[numg].NamePTwo) ? games[numg].NamePTwo : "Tu rival"));

            if (!accepted)
            {
                if (!string.IsNullOrEmpty(targetOpp))
                {
                    await Clients.Client(targetOpp).SendAsync("RevanchaRejected1vs1", new { responderName = respName });
                }
                await Clients.Caller.SendAsync("RevanchaRejected1vs1", new { responderName = respName });
                return;
            }

            // Reiniciar estado completo para la revancha 1vs1
            games[numg].PointsOne = 0;
            games[numg].PointsTwo = 0;
            games[numg].RoundOne = 0;
            games[numg].RoundTwo = 0;
            games[numg].CurrentStake = 1;
            games[numg].LastStakeAsker = 0;
            games[numg].Ask369 = 0;
            games[numg].IsTumbaOne = false;
            games[numg].IsTumbaTwo = false;
            games[numg].IsTumbaDeParaAtrasOne = false;
            games[numg].IsTumbaDeParaAtrasTwo = false;
            games[numg].HandStarter = 1;
            games[numg].HandCount = 1;
            games[numg].PlayerTurn = true;
            games[numg].IsActive = true;
            games[numg].Deck.RandomCards();
            games[numg].ShuffleCards_1vs1();

            // Notificar aceptación
            await Clients.Group($"game1vs1_{gameId}").SendAsync("RevanchaAccepted1vs1", new
            {
                gameId = gameId,
                responderName = respName
            });

            // Enviar reparto de mano inicial de la revancha a ambos jugadores
            string POne = games[numg].IdPOne;
            string PTwo = games[numg].IdPTwo;
            string PThree = games[numg].InitHand;
            GameMessage sentence = new GameMessage();
            sentence.game = gameId;
            sentence.order = 87;
            string previewcontent = PThree + "-";
            string PFour = "1";
            string PFive = "0";
            string PScore = "-0-0";

            sentence.content = previewcontent + PFour + PScore;
            if (!string.IsNullOrEmpty(POne)) await Clients.Client(POne).SendAsync("setChangeHand", sentence);

            sentence.content = previewcontent + PFive + PScore;
            if (!string.IsNullOrEmpty(PTwo)) await Clients.Client(PTwo).SendAsync("setChangeHand", sentence);

            await Clients.Group($"game1vs1_{gameId}").SendAsync("GameHandUpdated1vs1", new
            {
                game = gameId,
                handCards = PThree,
                handStarter = 1,
                pointsOne = 0,
                pointsTwo = 0
            });
        }

        public async Task RejoinGame1vs1(int gameId, bool isPlayerOne)
        {
            Console.WriteLine($"[RejoinGame1vs1] Cliente: {Context.ConnectionId}, Juego: {gameId}, EsP1: {isPlayerOne}");
            int numg = FindGame1vs1(gameId);
            if (numg < 0 || numg >= games.Count) return;

            if (isPlayerOne) games[numg].IdPOne = Context.ConnectionId;
            else games[numg].IdPTwo = Context.ConnectionId;

            await Groups.AddToGroupAsync(Context.ConnectionId, $"game1vs1_{gameId}");

            string targetOpp = isPlayerOne ? games[numg].IdPTwo : games[numg].IdPOne;
            if (!string.IsNullOrEmpty(targetOpp))
            {
                await Clients.Client(targetOpp).SendAsync("OpponentReconnected1vs1", new
                {
                    opponentConnectionId = Context.ConnectionId,
                    isPlayerOne = isPlayerOne
                });
            }
        }

        public async Task PassTumba1vs1(GameMessage move)
        {
            GameLogger.Log(move.game, "PassTumba1vs1", $"Cliente: {Context.ConnectionId}");
            int numg = FindGame1vs1(move.game);
            if (numg < 0 || numg >= games.Count) return;

            string caller = Context.ConnectionId;
            bool callerIsP1 = (caller == games[numg].IdPOne);

            int oldP1 = games[numg].PointsOne;
            int oldP2 = games[numg].PointsTwo;

            if (callerIsP1)
            {
                games[numg].PointsOne = Math.Max(0, games[numg].PointsOne - 1);
                games[numg].PointsTwo += 1;
            }
            else
            {
                games[numg].PointsTwo = Math.Max(0, games[numg].PointsTwo - 1);
                games[numg].PointsOne += 1;
            }
            games[numg].UpdateTumbaStatus(oldP1, oldP2);

            string p1Score = games[numg].PointsOne.ToString();
            string p2Score = games[numg].PointsTwo.ToString();

            await Clients.Client(games[numg].IdPOne).SendAsync("TumbaPassedNotice", new
            {
                passedByMe = callerIsP1,
                pointsOne = p1Score,
                pointsTwo = p2Score,
                message = callerIsP1 ? "Pasaste en Tumba (-1 piedra para ti, +1 para el rival)" : "¡El rival pasó en Tumba! (+1 piedra para ti, -1 para él)"
            });

            await Clients.Client(games[numg].IdPTwo).SendAsync("TumbaPassedNotice", new
            {
                passedByMe = !callerIsP1,
                pointsOne = p1Score,
                pointsTwo = p2Score,
                message = !callerIsP1 ? "Pasaste en Tumba (-1 piedra para ti, +1 para el rival)" : "¡El rival pasó en Tumba! (+1 piedra para ti, -1 para él)"
            });

            // Repartir la nueva mano
            games[numg].Deck.RandomCards();
            games[numg].ShuffleCards_1vs1();
            games[numg].HandStarter = (games[numg].HandStarter == 1) ? 2 : 1;
            games[numg].PlayerTurn = (games[numg].HandStarter == 1);
            games[numg].HandCount++;
            games[numg].CurrentStake = 1;
            games[numg].LastStakeAsker = 0;
            games[numg].Ask369 = 0;
            games[numg].RoundOne = 0;
            games[numg].RoundTwo = 0;

            string POne = games[numg].IdPOne;
            string PTwo = games[numg].IdPTwo;
            string PThree = games[numg].InitHand;
            GameMessage sentence = new GameMessage();
            sentence.game = move.game;
            sentence.order = 87;
            string previewcontent = PThree + "-";
            string PFour = (games[numg].HandStarter == 1 ? "1" : "0");
            string PFive = (games[numg].HandStarter == 2 ? "1" : "0");
            string PScore = $"-{games[numg].PointsOne}-{games[numg].PointsTwo}";

            sentence.content = previewcontent + PFour + PScore;
            await Clients.Client(POne).SendAsync("setChangeHand", sentence);
            sentence.content = previewcontent + PFive + PScore;
            await Clients.Client(PTwo).SendAsync("setChangeHand", sentence);
        }

        public async Task AcceptTumba1vs1(GameMessage move)
        {
            GameLogger.Log(move.game, "AcceptTumba1vs1", $"Cliente: {Context.ConnectionId}");
            int numg = FindGame1vs1(move.game);
            if (numg < 0 || numg >= games.Count) return;

            string caller = Context.ConnectionId;
            bool callerIsP1 = (caller == games[numg].IdPOne);
            string otherPlayer = callerIsP1 ? games[numg].IdPTwo : games[numg].IdPOne;

            await Clients.Client(otherPlayer).SendAsync("TumbaAcceptedNotice", new
            {
                message = "El rival aceptó jugar la mano de Tumba."
            });
        }

        public async Task GetGame1vs1(GameMessage move)
        {
            Console.WriteLine($"GetGame1vs1. Cliente: {Context.ConnectionId}, Orden: {move}");
            string PZero = move.content;
            bool PFlag = move.order == 91 ? true : false;
            int Place = FindGame1vs1(PZero, PFlag);
            GameMessage sentence = new GameMessage();
            sentence.game = games[Place].Id;
            sentence.order = PFlag ? 93 : 94;
            sentence.content = games[Place].InitHand;
            await Clients.Client(Context.ConnectionId).SendAsync("DealGame1vs1", sentence);
        }

        public async Task GetInitHand(int id, bool flag)
        {
            Console.WriteLine($"GetInitHandGame1vs1. Cliente: {Context.ConnectionId}, Juego: {id}, Flag: {flag}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"game1vs1_{id}");
            string PZero = FindInitHand(id);
            int numg = FindGame1vs1(id);
            if (numg >= 0 && numg < games.Count)
            {
                // Actualizar ConnectionId activo del cliente en la partida
                if (flag) games[numg].IdPOne = Context.ConnectionId;
                else games[numg].IdPTwo = Context.ConnectionId;

                string targetOpp = flag ? games[numg].IdPTwo : games[numg].IdPOne;
                if (!string.IsNullOrEmpty(targetOpp))
                {
                    await Clients.Client(targetOpp).SendAsync("OpponentConnectionUpdated", new
                    {
                        newConnectionId = Context.ConnectionId,
                        isPlayerOne = flag
                    });
                }
            }

            int p1 = (numg >= 0 && numg < games.Count) ? games[numg].PointsOne : 0;
            int p2 = (numg >= 0 && numg < games.Count) ? games[numg].PointsTwo : 0;
            bool isMyTurn = (numg >= 0 && numg < games.Count)
                ? (flag ? (games[numg].HandStarter == 1) : (games[numg].HandStarter == 2))
                : flag;
            GameMessage sentence = new GameMessage();
            sentence.game = id;
            sentence.order = 81;
            sentence.content = PZero + "-" + (isMyTurn ? "1" : "0") + $"-{p1}-{p2}";
            await Clients.Client(Context.ConnectionId).SendAsync("SetInitHand", sentence);
        }

        private int FindGame1vs1(string player, bool position)
        {
            int result = -1;
            int contador = -1;
            foreach(GamePlayOneVsOne game in games)
            {
                contador++;
                if (position && game.IsActive)
                {
                    if (game.IdPOne == player)
                    {
                        result = contador;
                        break;
                    }
                } else
                {
                    if (game.IdPTwo == player)
                    {
                        result = contador;
                        break;
                    }
                }
            }
            return result;
        }

        private int FindGame1vs1(int index)
        {
            int result = -1;
            int contador = -1;
            foreach (GamePlayOneVsOne game in games)
            {
                contador++;
                if (game.Id == index)
                {
                   result = contador;
                   break;
                }
            }
            return result;
        }

        private string FindInitHand(int id)
        {
            string mano = "";
            foreach (GamePlayOneVsOne game in games)
            {
                if (game.Id == id)
                {
                    mano = game.InitHand;
                    break;
                }
            }
            return mano;
        }

        public async Task RequestCard1vs1(GameMessage move)
        {
            GameLogger.Log(move.game, "RequestCard1vs1", $"Order:{move.order}, Cliente:{Context.ConnectionId}, Move:{move.content}");
            int numg = FindGame1vs1(move.game);
            if (numg < 0 || numg >= games.Count) return;

            string[] daticos = move.content.Split(" ");
            bool isPlayerOne = (Context.ConnectionId == games[numg].IdPOne);
            if (!isPlayerOne && Context.ConnectionId != games[numg].IdPTwo)
            {
                // Auto-reparar socket si reconectó:
                if (daticos.Length > 0 && daticos[0] == games[numg].IdPOne)
                {
                    games[numg].IdPOne = Context.ConnectionId;
                    isPlayerOne = true;
                }
                else if (daticos.Length > 0 && daticos[0] == games[numg].IdPTwo)
                {
                    games[numg].IdPTwo = Context.ConnectionId;
                    isPlayerOne = false;
                }
            }

            string targetOpp = isPlayerOne ? games[numg].IdPTwo : games[numg].IdPOne;
            GameMessage sentence = new GameMessage();

            if (move.order == 82) // Juego del que lleva la mano
            {
                sentence.game = move.game;
                sentence.order = 84;
                sentence.content = move.content;
                Console.WriteLine($"[RequestCard1vs1 82] Enviando a rival ({targetOpp}): 84 {sentence.content}");
                if (!string.IsNullOrEmpty(targetOpp))
                {
                    await Clients.Client(targetOpp).SendAsync("ResponseCard1vs1", sentence);
                }
                else
                {
                    await Clients.OthersInGroup($"game1vs1_{move.game}").SendAsync("ResponseCard1vs1", sentence);
                }
            }
            else if (move.order == 83) // Juego del que responde
            {
                int cardone = Convert.ToInt16(daticos[1]);
                int cardtwo = Convert.ToInt16(daticos[3]);
                int cardzero = Convert.ToInt16(daticos[4]);

                string rone = games[numg].RoundOne.ToString();
                string rtwo = games[numg].RoundTwo.ToString();
                string pone = games[numg].PointsOne.ToString();
                string ptwo = games[numg].PointsTwo.ToString();
                string mdef = "";
                string rdef = "";
                int leadCard = cardone;
                int respCard = cardtwo;

                // El llamador de la orden 83 es quien responde. Por tanto, el jugador que salió (lead) es el contrincante:
                bool leadIsPlayerOne = !isPlayerOne;

                // Verificación de "La Cogía": Si se juega el 10 de Oro (7) y el rival responde con el 1 de Oro (0)
                // En tumba la cogía NO vale (innecesario adquirir 3 puntos)
                bool isTumbaMulti = games[numg].IsTumbaOne || games[numg].IsTumbaTwo ||
                                   games[numg].PointsOne >= 9 || games[numg].PointsTwo >= 9 ||
                                   (games[numg].IsTumbaDeParaAtrasOne && games[numg].PointsOne == 8) ||
                                   (games[numg].IsTumbaDeParaAtrasTwo && games[numg].PointsTwo == 8);

                bool isCogida = false;
                int cogidaWinner = 0; // 1 = playerOne, 2 = playerTwo
                if (!isTumbaMulti && ((leadCard == 7 && respCard == 0) || (leadCard == 0 && respCard == 7)))
                {
                    isCogida = true;
                    // El que tiene el 1 de Oro (0) siempre gana la cogía (+3 piedras), sea que salió o respondió
                    if (leadCard == 0)
                    {
                        cogidaWinner = leadIsPlayerOne ? 1 : 2;
                    }
                    else
                    {
                        cogidaWinner = leadIsPlayerOne ? 2 : 1;
                    }
                }

                if (isCogida)
                {
                    int oldP1 = games[numg].PointsOne;
                    int oldP2 = games[numg].PointsTwo;
                    if (cogidaWinner == 1) games[numg].PointsOne += 3;
                    else games[numg].PointsTwo += 3;
                    games[numg].UpdateTumbaStatus(oldP1, oldP2);
                    Console.WriteLine($"[La Cogia] ¡Jugador {cogidaWinner} se acredita +3 piedras!");
                }

                // DetermineGame1vs1 retorna "1" si la carta de salida (lead) gana, o "0" si la de respuesta gana
                string cardwin = GamePlayOneVsOne.DetermineGame1vs1(cardone, cardtwo, cardzero, true);
                mdef = cardwin;

                bool trickWinnerIsPlayerOne = (cardwin == "1") ? leadIsPlayerOne : !leadIsPlayerOne;

                int stake = games[numg].CurrentStake > 0 ? games[numg].CurrentStake : 1;

                if (trickWinnerIsPlayerOne)
                {
                    games[numg].RoundOne++; 
                    rone = games[numg].RoundOne.ToString();
                    if (games[numg].RoundOne == 2)
                    {
                        mdef = leadIsPlayerOne ? "3" : "2"; 
                        games[numg].RoundOne = 0; 
                        games[numg].RoundTwo = 0;

                        // Reglas oficiales de Tumba y Obligado:
                        bool wasInTumbaOne = games[numg].IsTumbaOne;
                        bool wasInTumbaTwo = games[numg].IsTumbaTwo;

                        bool isP1Winner = false;
                        if (wasInTumbaOne && wasInTumbaTwo)
                        {
                            // En Obligado: Quien gane 2 de 3 bazas gana el juego
                            mdef = leadIsPlayerOne ? "5" : "4";
                            isP1Winner = true;
                        }
                        else if (wasInTumbaOne)
                        {
                            // Jugador 1 estaba en Tumba y ganó la mano -> Gana la partida (Tumba completada)
                            mdef = leadIsPlayerOne ? "5" : "4";
                            isP1Winner = true;
                        }
                        else if (wasInTumbaTwo)
                        {
                            // Jugador 2 estaba en Tumba y perdió la mano -> Cae en Tumba (-3 pts para él, +3 para J1)
                            int oldP1 = games[numg].PointsOne;
                            int oldP2 = games[numg].PointsTwo;
                            games[numg].PointsTwo = Math.Max(0, games[numg].PointsTwo - 3);
                            games[numg].PointsOne += 3;
                            games[numg].UpdateTumbaStatus(oldP1, oldP2);
                        }
                        else
                        {
                            // Ninguno en Tumba: se suma el valor de la apuesta (stake).
                            // Si llega a >= 9, ENTRA en Tumba para la siguiente mano, pero NO gana la partida aún.
                            int oldP1 = games[numg].PointsOne;
                            int oldP2 = games[numg].PointsTwo;
                            games[numg].PointsOne += stake;
                            games[numg].UpdateTumbaStatus(oldP1, oldP2);
                        }

                        if (isP1Winner)
                        {
                            await ProcessMatchPayout(numg, games[numg].IdPOne, games[numg].IdPTwo, "VictoriaPorPuntos");
                        }

                        pone = games[numg].PointsOne.ToString();
                        ptwo = games[numg].PointsTwo.ToString();
                    }
                }
                else
                {
                    games[numg].RoundTwo++;
                    rtwo = games[numg].RoundTwo.ToString();
                    if (games[numg].RoundTwo == 2)
                    {
                        mdef = (!leadIsPlayerOne) ? "3" : "2"; 
                        games[numg].RoundOne = 0; 
                        games[numg].RoundTwo = 0;

                        bool wasInTumbaOne = games[numg].IsTumbaOne;
                        bool wasInTumbaTwo = games[numg].IsTumbaTwo;

                        bool isP2Winner = false;
                        if (wasInTumbaOne && wasInTumbaTwo)
                        {
                            // En Obligado: Quien gane 2 de 3 bazas gana el juego
                            mdef = (!leadIsPlayerOne) ? "5" : "4";
                            isP2Winner = true;
                        }
                        else if (wasInTumbaTwo)
                        {
                            // Jugador 2 estaba en Tumba y ganó la mano -> Gana la partida (Tumba completada)
                            mdef = (!leadIsPlayerOne) ? "5" : "4";
                            isP2Winner = true;
                        }
                        else if (wasInTumbaOne)
                        {
                            // Jugador 1 estaba en Tumba y perdió la mano -> Cae en Tumba (-3 pts para él, +3 para J2)
                            int oldP1 = games[numg].PointsOne;
                            int oldP2 = games[numg].PointsTwo;
                            games[numg].PointsOne = Math.Max(0, games[numg].PointsOne - 3);
                            games[numg].PointsTwo += 3;
                            games[numg].UpdateTumbaStatus(oldP1, oldP2);
                        }
                        else
                        {
                            // Ninguno en Tumba: se suma el valor de la apuesta (stake).
                            // Si llega a >= 9, ENTRA en Tumba para la siguiente mano, pero NO gana la partida aún.
                            int oldP1 = games[numg].PointsOne;
                            int oldP2 = games[numg].PointsTwo;
                            games[numg].PointsTwo += stake;
                            games[numg].UpdateTumbaStatus(oldP1, oldP2);
                        }

                        if (isP2Winner)
                        {
                            await ProcessMatchPayout(numg, games[numg].IdPTwo, games[numg].IdPOne, "VictoriaPorPuntos");
                        }

                        pone = games[numg].PointsOne.ToString();
                        ptwo = games[numg].PointsTwo.ToString();
                    }
                }
                pone = games[numg].PointsOne.ToString();
                ptwo = games[numg].PointsTwo.ToString();
                sentence.game = move.game;
                sentence.order = 85;
                sentence.content = daticos[1] + " " + daticos[3] + " " + daticos[4] + " ";
                rdef = cardwin + " " + mdef + " " + rone + " " + rtwo + " " + pone + " " + ptwo;
                sentence.content += rdef;
                Console.WriteLine($"[RequestCard1vs1 83] Enviando a rival ({targetOpp}): 85 {sentence.content}");
                if (!string.IsNullOrEmpty(targetOpp))
                {
                    await Clients.Client(targetOpp).SendAsync("ResponseCard1vs1", sentence);
                }
                else
                {
                    await Clients.OthersInGroup($"game1vs1_{move.game}").SendAsync("ResponseCard1vs1", sentence);
                }
                await Clients.Client(Context.ConnectionId).SendAsync("ReasonRound1vs1", rdef);
            }
        }

        public async Task TimeoutRound1vs1(GameMessage move)
        {
            GameLogger.Log(move.game, "TimeoutRound1vs1", $"Cliente: {Context.ConnectionId}");
            int numg = FindGame1vs1(move.game);
            if (numg < 0 || numg >= games.Count) return;

            string caller = Context.ConnectionId;
            bool callerIsP1 = (caller == games[numg].IdPOne);
            string winnerId = callerIsP1 ? games[numg].IdPTwo : games[numg].IdPOne;
            string loserId = caller;

            int stake = games[numg].CurrentStake > 0 ? games[numg].CurrentStake : 1;
            bool wasInTumbaOne = games[numg].IsTumbaOne;
            bool wasInTumbaTwo = games[numg].IsTumbaTwo;
            bool isGameOver = false;

            if (callerIsP1)
            {
                // P1 agotó el tiempo -> P2 gana la ronda
                if (wasInTumbaTwo)
                {
                    // P2 estaba en Tumba y gana la partida
                    isGameOver = true;
                }
                else if (wasInTumbaOne)
                {
                    // P1 estaba en Tumba y agotó el tiempo -> cae en Tumba (-3 pts)
                    int oldP1 = games[numg].PointsOne;
                    int oldP2 = games[numg].PointsTwo;
                    games[numg].PointsOne = Math.Max(0, games[numg].PointsOne - 3);
                    games[numg].PointsTwo += 3;
                    games[numg].UpdateTumbaStatus(oldP1, oldP2);
                }
                else
                {
                    int oldP1 = games[numg].PointsOne;
                    int oldP2 = games[numg].PointsTwo;
                    games[numg].PointsTwo += stake;
                    games[numg].UpdateTumbaStatus(oldP1, oldP2);
                }
            }
            else
            {
                // P2 agotó el tiempo -> P1 gana la ronda
                if (wasInTumbaOne)
                {
                    // P1 estaba en Tumba y gana la partida
                    isGameOver = true;
                }
                else if (wasInTumbaTwo)
                {
                    // P2 estaba en Tumba y agotó el tiempo -> cae en Tumba (-3 pts)
                    int oldP1 = games[numg].PointsOne;
                    int oldP2 = games[numg].PointsTwo;
                    games[numg].PointsTwo = Math.Max(0, games[numg].PointsTwo - 3);
                    games[numg].PointsOne += 3;
                    games[numg].UpdateTumbaStatus(oldP1, oldP2);
                }
                else
                {
                    int oldP1 = games[numg].PointsOne;
                    int oldP2 = games[numg].PointsTwo;
                    games[numg].PointsOne += stake;
                    games[numg].UpdateTumbaStatus(oldP1, oldP2);
                }
            }

            games[numg].RoundOne = 0;
            games[numg].RoundTwo = 0;

            string p1Score = games[numg].PointsOne.ToString();
            string p2Score = games[numg].PointsTwo.ToString();

            if (isGameOver)
            {
                await ProcessMatchPayout(numg, winnerId, loserId, "TiempoAgotado");
            }

            // Notificar a ambos clientes
            await Clients.Client(winnerId).SendAsync("RoundTimeoutNotice", new
            {
                won = true,
                gameOver = isGameOver,
                pointsOne = p1Score,
                pointsTwo = p2Score,
                message = "⏳ ¡El contrincante agotó sus 30 segundos! Ganaste la ronda."
            });

            await Clients.Client(loserId).SendAsync("RoundTimeoutNotice", new
            {
                won = false,
                gameOver = isGameOver,
                pointsOne = p1Score,
                pointsTwo = p2Score,
                message = "⏳ Se agotaron tus 30 segundos para jugar. Perdiste la ronda."
            });
        }

        public async Task SurrenderGame1vs1(GameMessage move)
        {
            Console.WriteLine($"[SurrenderGame1vs1] Cliente: {Context.ConnectionId}, Juego: {move.game}");
            int numg = FindGame1vs1(move.game);
            if (numg < 0 || numg >= games.Count) return;

            string caller = Context.ConnectionId;
            bool callerIsP1 = (caller == games[numg].IdPOne);
            string winnerId = callerIsP1 ? games[numg].IdPTwo : games[numg].IdPOne;
            string loserId = caller;

            // Procesar liquidación de monedas (80% ganador, 20% administrador)
            await ProcessMatchPayout(numg, winnerId, loserId, "Rendicion");

            // Notificar al ganador que el oponente se rindió
            await Clients.Client(winnerId).SendAsync("OpponentSurrendered", new
            {
                message = "🏆 ¡Tu contrincante ha abandonado la partida! Has ganado automáticamente el premio."
            });

            // Notificar al que se rindió
            await Clients.Client(loserId).SendAsync("YouSurrendered", new
            {
                message = "Has abandonado la partida. Tu contrincante fue declarado ganador."
            });
        }

        /// <summary>
        /// El jugador activo reclama la victoria porque el rival agotó sus 30 segundos de turno o se desconectó.
        /// </summary>
        public async Task ClaimOpponentTimeout1vs1(int gameId)
        {
            Console.WriteLine($"[ClaimOpponentTimeout1vs1] Cliente: {Context.ConnectionId}, Juego: {gameId}");
            int numg = FindGame1vs1(gameId);
            if (numg < 0 || numg >= games.Count) return;
            var game = games[numg];
            if (!game.IsActive) return;

            string caller = Context.ConnectionId;
            bool callerIsP1 = (caller == game.IdPOne);
            bool callerIsP2 = (caller == game.IdPTwo);
            if (!callerIsP1 && !callerIsP2) return;

            string winnerId = caller;
            string loserId = callerIsP1 ? game.IdPTwo : game.IdPOne;

            // Procesar liquidación otorgando la victoria al jugador activo
            await ProcessMatchPayout(numg, winnerId, loserId, "AbandonoRival");

            await Clients.Client(winnerId).SendAsync("OpponentSurrendered", new
            {
                message = "🏆 ¡Tu contrincante no respondió a tiempo y abandonó la partida! Has ganado la partida."
            });

            if (!string.IsNullOrEmpty(loserId))
            {
                await Clients.Client(loserId).SendAsync("YouSurrendered", new
                {
                    message = "Partida perdida por tiempo agotado o desconexión."
                });
            }
        }

        /// <summary>
        /// Liquida las apuestas de la partida 1 vs 1:
        /// - Descuenta la apuesta al perdedor.
        /// - Retiene el 20% de comisión para la casa/administrador.
        /// - Entrega el 80% del pozo total al ganador.
        /// - Registra la partida en MatchBetRecords para auditoría y reportes del administrador.
        /// - Notifica a ambos jugadores con su saldo actualizado.
        /// </summary>
        private async Task ProcessMatchPayout(int gameIndex, string winnerConnectionId, string loserConnectionId, string reason)
        {
            if (gameIndex < 0 || gameIndex >= games.Count) return;
            var game = games[gameIndex];
            if (!game.IsActive) return; // Evitar doble liquidación si ya se procesó
            game.IsActive = false;

            bool isSala = game.IsFriendlyRoom 
                || (!string.IsNullOrEmpty(game.RoomName) && game.RoomName.StartsWith("sala-", StringComparison.OrdinalIgnoreCase));
            if (!isSala)
            {
                lock (rooms1v1Lock)
                {
                    isSala = rooms1v1.Values.Any(s => s.GameId == game.Id);
                }
            }

            int bet = game.Coins > 0 ? game.Coins : 10;
            int totalPot = bet * 2;
            int houseCommission;
            int winnerPrize;

            if (isSala)
            {
                // En salas privadas (amistosas): Todo el dinero recaudado de la tarifa de sala va para la casa (Administrador)
                houseCommission = totalPot;
                winnerPrize = 0;
            }
            else
            {
                // En duelos de emparejamiento público: 20% para la casa, 80% para el ganador
                houseCommission = (int)Math.Round(totalPot * 0.20);
                winnerPrize = totalPot - houseCommission;
            }

            int winnerNewCoins = 0;
            int loserNewCoins = 0;
            int winnerWins = 0;
            int winnerLosses = 0;
            string winnerLevel = "Peón de Casona";
            int loserWins = 0;
            int loserLosses = 0;
            string loserLevel = "Peón de Casona";

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var winnerPlayer = SearchPlayer(winnerConnectionId);
                    var loserPlayer = SearchPlayer(loserConnectionId);

                    string winnerName = !string.IsNullOrEmpty(winnerPlayer.Name) && winnerPlayer.Name != "nulo"
                        ? winnerPlayer.Name
                        : ((winnerConnectionId == game.IdPOne) ? game.NamePOne : game.NamePTwo);

                    string loserName = !string.IsNullOrEmpty(loserPlayer.Name) && loserPlayer.Name != "nulo"
                        ? loserPlayer.Name
                        : ((loserConnectionId == game.IdPOne) ? game.NamePOne : game.NamePTwo);

                    string winnerEmail = winnerPlayer.Email ?? "";
                    string loserEmail = loserPlayer.Email ?? "";

                    string winnerUserId = (winnerConnectionId == game.IdPOne) ? game.UserIdPOne : game.UserIdPTwo;
                    string loserUserId = (loserConnectionId == game.IdPOne) ? game.UserIdPOne : game.UserIdPTwo;

                    var dbWinner = db.Users.FirstOrDefault(u => 
                        (!string.IsNullOrEmpty(winnerUserId) && u.Id.ToString() == winnerUserId) ||
                        (!string.IsNullOrEmpty(winnerName) && u.Username.ToLower() == winnerName.ToLower()) || 
                        (!string.IsNullOrEmpty(winnerEmail) && u.Email.ToLower() == winnerEmail.ToLower()));

                    var dbLoser = db.Users.FirstOrDefault(u => 
                        (!string.IsNullOrEmpty(loserUserId) && u.Id.ToString() == loserUserId) ||
                        (!string.IsNullOrEmpty(loserName) && u.Username.ToLower() == loserName.ToLower()) || 
                        (!string.IsNullOrEmpty(loserEmail) && u.Email.ToLower() == loserEmail.ToLower()));

                    if (dbLoser != null)
                    {
                        int loserDeduction = Math.Min(dbLoser.Coins, bet);
                        dbLoser.Coins -= loserDeduction;
                        dbLoser.Losses += 1;
                        dbLoser.Level = dbLoser.GetCalculatedLevel();
                        loserNewCoins = dbLoser.Coins;
                        loserPlayer.Coins = loserNewCoins;
                        loserWins = dbLoser.Wins;
                        loserLosses = dbLoser.Losses;
                        loserLevel = dbLoser.Level;
                    }
                    else
                    {
                        loserPlayer.Coins = Math.Max(0, loserPlayer.Coins - bet);
                        loserNewCoins = loserPlayer.Coins;
                        loserWins = 0;
                        loserLosses = 1;
                    }

                    if (dbWinner != null)
                    {
                        if (isSala)
                        {
                            // En sala, ambos jugadores pagan la tarifa de sala de entrada (10 monedas) para la casa
                            int winnerDeduction = Math.Min(dbWinner.Coins, bet);
                            dbWinner.Coins -= winnerDeduction;
                        }
                        else
                        {
                            int netWinnerGain = Math.Max(0, winnerPrize - bet);
                            dbWinner.Coins += netWinnerGain;
                        }

                        dbWinner.Wins += 1;
                        dbWinner.Level = dbWinner.GetCalculatedLevel();
                        winnerNewCoins = dbWinner.Coins;
                        winnerPlayer.Coins = winnerNewCoins;
                        winnerWins = dbWinner.Wins;
                        winnerLosses = dbWinner.Losses;
                        winnerLevel = dbWinner.Level;
                    }
                    else
                    {
                        if (isSala)
                        {
                            winnerPlayer.Coins = Math.Max(0, winnerPlayer.Coins - bet);
                        }
                        else
                        {
                            winnerPlayer.Coins = Math.Max(0, winnerPlayer.Coins + (winnerPrize - bet));
                        }
                        winnerNewCoins = winnerPlayer.Coins;
                        winnerWins = 1;
                        winnerLosses = 0;
                    }

                    if (dbWinner != null || dbLoser != null)
                    {
                        var betRecord = new MatchBetRecord
                        {
                            GameId = game.Id,
                            PlayerOneName = !string.IsNullOrEmpty(game.NamePOne) ? game.NamePOne : SearchPlayer(game.IdPOne).Name,
                            PlayerTwoName = !string.IsNullOrEmpty(game.NamePTwo) ? game.NamePTwo : SearchPlayer(game.IdPTwo).Name,
                            BetPerPlayer = bet,
                            TotalPot = totalPot,
                            HouseCommission = houseCommission,
                            WinnerPrize = winnerPrize,
                            WinnerUsername = dbWinner?.Username ?? winnerName,
                            LoserUsername = dbLoser?.Username ?? loserName,
                            EndReason = isSala ? $"[SALA 100%] {reason}" : reason,
                            CreatedAt = DateTime.UtcNow
                        };
                        db.MatchBetRecords.Add(betRecord);
                        await db.SaveChangesAsync();

                        GameLogger.Log(game.Id, "ProcessMatchPayout", $"[{(isSala ? "SALA 100%" : "DUELO 20%")}] Ganador={dbWinner?.Username ?? winnerName} (Saldo={winnerNewCoins}), Perdedor={dbLoser?.Username ?? loserName} (Saldo={loserNewCoins}), Premio={winnerPrize}, Casa={houseCommission}, Razon={reason}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessMatchPayout Error] {ex.Message}");
            }

            // Notificar SIEMPRE a ambos jugadores para que vean su pantalla final y monedas
            try
            {
                string winnerMessage = isSala
                    ? $"🏆 ¡Ganaste la partida en sala privada! Tarifa de sala ({bet} monedas) abonada a la plataforma."
                    : $"🏆 ¡Ganaste la partida! Te llevas {winnerPrize} monedas (80% del pozo de {totalPot}). Comisión de sala (20%): {houseCommission} monedas.";

                string loserMessage = isSala
                    ? $"Partida en sala privada finalizada. Tarifa de sala ({bet} monedas) abonada a la plataforma."
                    : $"Partida finalizada. Se descontaron {bet} monedas de tu monedero.";

                await Clients.Client(winnerConnectionId).SendAsync("MatchFinishedPayout", new
                {
                    isWinner = true,
                    isSala = isSala,
                    bet = bet,
                    totalPot = totalPot,
                    houseCommission = houseCommission,
                    winnerPrize = winnerPrize,
                    netGain = isSala ? -bet : (winnerPrize - bet),
                    newBalance = winnerNewCoins,
                    newWins = winnerWins,
                    newLosses = winnerLosses,
                    level = winnerLevel,
                    message = winnerMessage
                });

                await Clients.Client(loserConnectionId).SendAsync("MatchFinishedPayout", new
                {
                    isWinner = false,
                    isSala = isSala,
                    bet = bet,
                    totalPot = totalPot,
                    houseCommission = houseCommission,
                    winnerPrize = winnerPrize,
                    netGain = -bet,
                    newBalance = loserNewCoins,
                    newWins = loserWins,
                    newLosses = loserLosses,
                    level = loserLevel,
                    message = loserMessage
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessMatchPayout Send Error] {ex.Message}");
            }
        }

        /// <summary>
        /// Procesa la liquidación de apuestas y actualización de victorias/derrotas para partidas 2 vs 2.
        /// </summary>
        private async Task ProcessMatchPayout2v2(string roomKey, Room2v2Session session, int winningTeamOfMatch, string reason)
        {
            if (session == null || session.Seats.Count < 4) return;

            bool isSala2v2 = (session.RoomName != null && session.RoomName.StartsWith("sala-", StringComparison.OrdinalIgnoreCase))
                          || (roomKey != null && roomKey.StartsWith("sala-", StringComparison.OrdinalIgnoreCase));

            int betPerPlayer = Math.Max(10, session.Bet);
            int totalPot = betPerPlayer * 4;
            int houseCommission;
            int totalPrize;
            int winnerPrizePerPlayer;

            if (isSala2v2)
            {
                // En salas 2v2: 100% de la tarifa recaudada va para la casa (4 jugadores * betPerPlayer = totalPot)
                houseCommission = totalPot;
                totalPrize = 0;
                winnerPrizePerPlayer = 0;
            }
            else
            {
                houseCommission = (int)Math.Round(totalPot * 0.20);
                totalPrize = totalPot - houseCommission;
                winnerPrizePerPlayer = totalPrize / 2;
            }

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    foreach (var seat in session.Seats)
                    {
                        bool isWinner = (seat.Team == winningTeamOfMatch);

                        var dbUser = db.Users.FirstOrDefault(u =>
                            (!string.IsNullOrEmpty(seat.UserId) && u.Id.ToString() == seat.UserId) ||
                            (!string.IsNullOrEmpty(seat.Name) && u.Username.ToLower() == seat.Name.ToLower()));

                        int newBalance = 0;
                        int newWins = 0;
                        int newLosses = 0;
                        string calculatedLevel = "Peón de Casona";

                        if (dbUser != null)
                        {
                            if (isWinner)
                            {
                                if (isSala2v2)
                                {
                                    int deduction = Math.Min(dbUser.Coins, betPerPlayer);
                                    dbUser.Coins -= deduction;
                                }
                                else
                                {
                                    int netGain = Math.Max(0, winnerPrizePerPlayer - betPerPlayer);
                                    dbUser.Coins += netGain;
                                }
                                dbUser.Wins += 1;
                            }
                            else
                            {
                                int deduction = Math.Min(dbUser.Coins, betPerPlayer);
                                dbUser.Coins -= deduction;
                                dbUser.Losses += 1;
                            }

                            dbUser.Level = dbUser.GetCalculatedLevel();
                            newBalance = dbUser.Coins;
                            newWins = dbUser.Wins;
                            newLosses = dbUser.Losses;
                            calculatedLevel = dbUser.Level;
                        }

                        if (!string.IsNullOrEmpty(seat.ConnectionId))
                        {
                            try
                            {
                                string seatMsg = isWinner
                                    ? (isSala2v2
                                        ? $"🏆 ¡Tu equipo ganó la partida en sala privada! Tarifa de sala ({betPerPlayer} monedas) abonada a la plataforma."
                                        : $"🏆 ¡Tu equipo ganó la partida 2 vs 2! Te llevas {winnerPrizePerPlayer} monedas. Comisión de sala: {houseCommission / 2} monedas.")
                                    : (isSala2v2
                                        ? $"Partida en sala privada 2 vs 2 finalizada. Tarifa de sala ({betPerPlayer} monedas) abonada a la plataforma."
                                        : $"Partida 2 vs 2 finalizada. Se descontaron {betPerPlayer} monedas de tu monedero.");

                                await Clients.Client(seat.ConnectionId).SendAsync("MatchFinishedPayout", new
                                {
                                    isWinner = isWinner,
                                    isSala = isSala2v2,
                                    bet = betPerPlayer,
                                    totalPot = totalPot,
                                    houseCommission = houseCommission,
                                    winnerPrize = winnerPrizePerPlayer,
                                    netGain = isWinner ? (isSala2v2 ? -betPerPlayer : (winnerPrizePerPlayer - betPerPlayer)) : -betPerPlayer,
                                    newBalance = newBalance,
                                    newWins = newWins,
                                    newLosses = newLosses,
                                    level = calculatedLevel,
                                    message = seatMsg
                                });
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ProcessMatchPayout2v2 Send Error] {ex.Message}");
                            }
                        }
                    }

                    await db.SaveChangesAsync();

                    var betRecord = new MatchBetRecord
                    {
                        GameId = session.GameId,
                        PlayerOneName = "Equipo 1 (Azul)",
                        PlayerTwoName = "Equipo 2 (Rojo)",
                        BetPerPlayer = betPerPlayer,
                        TotalPot = totalPot,
                        HouseCommission = houseCommission,
                        WinnerPrize = totalPrize,
                        WinnerUsername = winningTeamOfMatch == 1 ? "Equipo Azul" : "Equipo Rojo",
                        LoserUsername = winningTeamOfMatch == 1 ? "Equipo Rojo" : "Equipo Azul",
                        EndReason = isSala2v2 ? $"[SALA 100%] {reason}" : reason,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.MatchBetRecords.Add(betRecord);
                    await db.SaveChangesAsync();

                    GameLogger.Log(session.GameId, "ProcessMatchPayout2v2", $"[{(isSala2v2 ? "SALA 2v2 100%" : "DUELO 2v2 20%")}] Equipo Ganador={winningTeamOfMatch}, Pozo={totalPot}, Casa={houseCommission}, Razon={reason}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessMatchPayout2v2 Error] {ex.Message}");
            }
        }

        // Métodos de desarrollo de los juegos a modo Solitario

        // Comienzo del juego. Turno del Jugador base

        public async Task StartGameSolitaire()
        {
            GamePlayOneVsOne newgame = new GamePlayOneVsOne(1);
            newgame.PlayerTurn = true;
            GameMessage sentence = new GameMessage();
            newgame.Deck.RandomCards();
            newgame.Id = newgame.GenerateSeed(games);
            sentence.game = newgame.Id;
            sentence.order = 100;
            sentence.content = newgame.StartGame();
            games.Add(newgame);
            await Clients.Caller.SendAsync("SetGameSolitaire", sentence);
        }


        public async Task KeepGameSolitaire(GameMessage move)
        {
            int IdGame = move.game;
            GamePlayOneVsOne actual = GamePlayOneVsOne.SearchPlay(games, IdGame);
            actual.Deck.RandomCards();
            actual.PlayerTurn = !actual.PlayerTurn;
            GameMessage sentence = new GameMessage();
            sentence.game = IdGame;
            sentence.order = 101;
            sentence.content = actual.StartRound();
            await Clients.Caller.SendAsync("PutGameSolitaire", sentence);
        }

        //
        // Movimientos nuevos
        // 

        // Comienzo del juego: el usuario comienza como mano del juego

        public async Task InitGameSol()
        {
            string callerId = Context.ConnectionId;
            GamePlayOneVsOne newgame = new GamePlayOneVsOne(1);
            newgame.IdPOne = callerId;
            newgame.PlayerTurn = true;
            newgame.ChoiceTurn = newgame.PlayerTurn;
            newgame.Deck.RandomCards();
            newgame.Id = newgame.GenerateSeed(games);

            lock (solitaireLock)
            {
                solitaireSessions[callerId] = newgame;
            }

            lock (games)
            {
                games.Add(newgame);
            }

            GameMessage sentence = new GameMessage();
            sentence.game = newgame.Id;
            sentence.order = 100;
            sentence.content = newgame.StartGame();
            Console.WriteLine($"InitGameSol ejecutado. Cliente: {callerId}, Juego: {newgame.Id}, Mano: {sentence.content}");
            await Clients.Caller.SendAsync("InitiatedGameSol", sentence);
        }

        public async Task ChangeTurnSol(GameMessage move)
        {
            try
            {
                string callerId = Context.ConnectionId;
                GamePlayOneVsOne? actual = null;
                lock (solitaireLock)
                {
                    solitaireSessions.TryGetValue(callerId, out actual);
                }
                if (actual == null)
                {
                    actual = GamePlayOneVsOne.SearchPlay(games, move.game);
                    lock (solitaireLock)
                    {
                        solitaireSessions[callerId] = actual;
                    }
                }

                actual.Deck.RandomCards();
                actual.PlayerTurn = !actual.PlayerTurn;
                actual.ChoiceTurn = actual.PlayerTurn;
                GameMessage sentence = new GameMessage();
                sentence.game = actual.Id;
                sentence.order = 106;
                sentence.content = actual.StartRound();
                Console.WriteLine($"ChangeTurnSol ejecutado. Cliente: {callerId}, Juego: {actual.Id}, Turno: {actual.PlayerTurn}, Mano: {sentence.content}");
                await Clients.Caller.SendAsync("ChangedTurnSol", sentence);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ChangeTurnSol: {ex.Message}");
            }
        }

        // Proceso de jugada de tirada 
        // (según quién lleva la mano, interpreta la jugada)

        public async Task ProcessGameMove(GameMessage move)
        {
            try
            {
                string callerId = Context.ConnectionId;
                GamePlayOneVsOne? actual = null;
                lock (solitaireLock)
                {
                    solitaireSessions.TryGetValue(callerId, out actual);
                }
                if (actual == null)
                {
                    actual = GamePlayOneVsOne.SearchPlay(games, move.game);
                    lock (solitaireLock)
                    {
                        solitaireSessions[callerId] = actual;
                    }
                }

                bool kindTurn = (move.order == 101);  
                GameMessage sentence = actual.SetGameMove(move.content, kindTurn);
                sentence.game = actual.Id;
                Console.WriteLine($"ProcessGameMove ejecutado. Cliente: {callerId}, Juego: {actual.Id}, Carta: {move.content}, Res: {sentence.content}");
                await Clients.Caller.SendAsync("ProcessedGameMove", sentence);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ProcessGameMove: {ex.Message} \n {ex.StackTrace}");
            }
        }

        public async Task AskInvitation(int One)
        {
            GameMessage sentence = new GameMessage();
            sentence.game = One;
            sentence.order = 110;
            sentence.content = "10";
            await Clients.Others.SendAsync("AskOnevsOne", sentence);
        }

        public async Task AnswerInvitation(GameMessage Zero)
        {
            GameMessage sentence = new GameMessage();
         //   sentence.game = One;
            sentence.order = 111;
            sentence.content = "10";
            await Clients.Others.SendAsync("AnswerOnevsOne", sentence);
        }

        public async Task StartGameOnevsOne(int One, int Two)
        {
            GamePlayOneVsOne newgame = new GamePlayOneVsOne(One, Two);
            newgame.PlayerTurn = true;
            GameMessage sentence = new GameMessage();
            newgame.Deck.RandomCards();
            newgame.Id = newgame.GenerateSeed(games);
            sentence.game = newgame.Id;
            sentence.order = 100;
            sentence.content = newgame.StartGame();
            games.Add(newgame);
            await Clients.Caller.SendAsync("SetGameSolitaire", sentence);
        }

        // ==========================================
        // MATCHMAKING AUTOMÁTICO 1 VS 1
        // ==========================================

        public async Task JoinMatchmaking(string mode, int bet, string playerName = "", string userId = "", string avatarUrl = "")
        {
            string callerId = Context.ConnectionId;
            Console.WriteLine($"JoinMatchmaking recibido. Cliente: {callerId}, Modo: {mode}, Apuesta: {bet}, User: {playerName} ({userId})");

            // GESTIÓN DE MATCHMAKING ALEATORIO PARA 2 CONTRA 2 (4 JUGADORES)
            if (mode == "2 vs 2")
            {
                List<MatchQueueItem> matchedFour = new List<MatchQueueItem>();
                lock (queueLock)
                {
                    matchmakingQueue.RemoveAll(q => q.ConnectionId == callerId);
                    matchmakingQueue.Add(new MatchQueueItem
                    {
                        ConnectionId = callerId,
                        UserId = userId ?? "",
                        PlayerName = !string.IsNullOrEmpty(playerName) ? playerName : (GetPlayerData(callerId)?.Name ?? "Jugador"),
                        AvatarUrl = avatarUrl ?? "",
                        Mode = mode,
                        Bet = bet,
                        EnqueuedAt = DateTime.UtcNow
                    });

                    var waitingFor2v2 = matchmakingQueue.Where(q => q.Mode == "2 vs 2" && q.Bet == bet).ToList();
                    if (waitingFor2v2.Count >= 4)
                    {
                        matchedFour = waitingFor2v2.Take(4).ToList();
                        foreach (var item in matchedFour)
                        {
                            matchmakingQueue.Remove(item);
                        }
                    }
                }

                if (matchedFour.Count < 4)
                {
                    int currentWaiting = 0;
                    List<string> waitingIds = new List<string>();
                    lock (queueLock)
                    {
                        var list = matchmakingQueue.Where(q => q.Mode == "2 vs 2" && q.Bet == bet).ToList();
                        currentWaiting = list.Count;
                        waitingIds = list.Select(q => q.ConnectionId).ToList();
                    }

                    // Notificar a todos los que esperan en la cola de 2 vs 2 cuántos van (1/4, 2/4, 3/4)
                    foreach (var connId in waitingIds)
                    {
                        await Clients.Client(connId).SendAsync("MatchmakingStatus", new
                        {
                            status = "waiting",
                            message = $"Buscando jugadores para 2 vs 2 ({currentWaiting}/4)...",
                            count = currentWaiting,
                            mode = "2 vs 2"
                        });
                    }
                    return;
                }

                if (matchedFour.Count == 4)
                {
                    // Mezclar aleatoriamente las 4 personas para que nadie sepa de antemano contra quién juega
                    Random rng = new Random();
                    var shuffled = matchedFour.OrderBy(_ => rng.Next()).ToList();
                    string p1 = shuffled[0].ConnectionId;
                    string p2 = shuffled[1].ConnectionId;
                    string p3 = shuffled[2].ConnectionId;
                    string p4 = shuffled[3].ConnectionId;

                    string n1 = !string.IsNullOrEmpty(shuffled[0].PlayerName) ? shuffled[0].PlayerName : (GetPlayerData(p1)?.Name ?? "Jugador");
                    string n2 = !string.IsNullOrEmpty(shuffled[1].PlayerName) ? shuffled[1].PlayerName : (GetPlayerData(p2)?.Name ?? "Jugador");
                    string n3 = !string.IsNullOrEmpty(shuffled[2].PlayerName) ? shuffled[2].PlayerName : (GetPlayerData(p3)?.Name ?? "Jugador");
                    string n4 = !string.IsNullOrEmpty(shuffled[3].PlayerName) ? shuffled[3].PlayerName : (GetPlayerData(p4)?.Name ?? "Jugador");

                    GamePlayTwoVsTwo newGame2v2 = new GamePlayTwoVsTwo(p1, p2, p3, p4);
                    newGame2v2.Coins = bet;
                    newGame2v2.Name1 = n1;
                    newGame2v2.Name2 = n2;
                    newGame2v2.Name3 = n3;
                    newGame2v2.Name4 = n4;

                    newGame2v2.Id = newGame2v2.GenerateSeed(games2vs2);
                    newGame2v2.ShuffleCards_2vs2();
                    games2vs2.Add(newGame2v2);

                    string matchRoomKey = $"match-{newGame2v2.Id}";
                    Room2v2Session matchSession = new Room2v2Session
                    {
                        RoomName = matchRoomKey,
                        Bet = bet,
                        GameStarted = true,
                        CurrentInitHand = newGame2v2.InitHand
                    };
                    matchSession.Seats.Add(new Seat2v2 { SeatIndex = 0, ConnectionId = p1, UserId = shuffled[0].UserId, Name = n1, AvatarUrl = shuffled[0].AvatarUrl, Team = 1, Role = "Anfitrión" });
                    matchSession.Seats.Add(new Seat2v2 { SeatIndex = 1, ConnectionId = p2, UserId = shuffled[1].UserId, Name = n2, AvatarUrl = shuffled[1].AvatarUrl, Team = 2, Role = "Rival 1" });
                    matchSession.Seats.Add(new Seat2v2 { SeatIndex = 2, ConnectionId = p3, UserId = shuffled[2].UserId, Name = n3, AvatarUrl = shuffled[2].AvatarUrl, Team = 1, Role = "Compañero" });
                    matchSession.Seats.Add(new Seat2v2 { SeatIndex = 3, ConnectionId = p4, UserId = shuffled[3].UserId, Name = n4, AvatarUrl = shuffled[3].AvatarUrl, Team = 2, Role = "Rival 2" });

                    lock (rooms2v2Lock)
                    {
                        rooms2v2[matchRoomKey] = matchSession;
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        await Groups.AddToGroupAsync(shuffled[i].ConnectionId, matchRoomKey);
                    }

                    Console.WriteLine($"[Matchmaking 2vs2] 4 Jugadores emparejados: {n1} & {n3} vs {n2} & {n4}. Sala: {matchRoomKey}");

                    for (int i = 0; i < 4; i++)
                    {
                        string targetConn = shuffled[i].ConnectionId;
                        GameMessage msg = new GameMessage
                        {
                            game = newGame2v2.Id,
                            order = 220, // MatchFound2v2
                            content = $"{newGame2v2.Id} {bet} {p1} {n1} {p2} {n2} {p3} {n3} {p4} {n4} {i} {newGame2v2.InitHand} {matchRoomKey}"
                        };
                        await Clients.Client(targetConn).SendAsync("MatchFound2v2", msg);
                    }
                }
                return;
            }

            MatchQueueItem? matchedPlayer = null;

            lock (queueLock)
            {
                // Limpiar entradas previas del mismo jugador
                matchmakingQueue.RemoveAll(q => q.ConnectionId == callerId);

                // Buscar un contrincante que espere el mismo modo y apuesta
                matchedPlayer = matchmakingQueue.FirstOrDefault(q => q.Mode == mode && q.Bet == bet && q.ConnectionId != callerId);

                if (matchedPlayer != null)
                {
                    matchmakingQueue.Remove(matchedPlayer);
                }
                else
                {
                    matchmakingQueue.Add(new MatchQueueItem
                    {
                        ConnectionId = callerId,
                        UserId = userId ?? "",
                        PlayerName = !string.IsNullOrEmpty(playerName) ? playerName : (GetPlayerData(callerId)?.Name ?? "Jugador"),
                        AvatarUrl = avatarUrl ?? "",
                        Mode = mode,
                        Bet = bet,
                        EnqueuedAt = DateTime.UtcNow
                    });
                }
            }

            if (matchedPlayer == null)
            {
                // Esperando en cola
                await Clients.Caller.SendAsync("MatchmakingStatus", new { status = "waiting", message = "Buscando oponente..." });
            }
            else
            {
                // Se encontró emparejamiento inmediato
                string p1 = matchedPlayer.ConnectionId;
                string p2 = callerId;

                GamePlayOneVsOne newGame = new GamePlayOneVsOne(p1, p2);
                newGame.Coins = matchedPlayer.Bet;
                GamePlayer q1 = GetPlayerData(p1);
                GamePlayer q2 = GetPlayerData(p2);

                string name1 = !string.IsNullOrEmpty(matchedPlayer.PlayerName) && !matchedPlayer.PlayerName.StartsWith("Jugador-")
                    ? matchedPlayer.PlayerName
                    : (!string.IsNullOrEmpty(q1.Name) && !q1.Name.StartsWith("Jugador-") ? q1.Name : "Jugador 1");

                string name2 = !string.IsNullOrEmpty(playerName) && !playerName.StartsWith("Jugador-")
                    ? playerName
                    : (!string.IsNullOrEmpty(q2.Name) && !q2.Name.StartsWith("Jugador-") ? q2.Name : "Jugador 2");

                newGame.NamePOne = name1;
                newGame.NamePTwo = name2;
                newGame.UserIdPOne = matchedPlayer.UserId ?? "";
                newGame.UserIdPTwo = userId ?? "";
                newGame.RoomName = $"match-{newGame.Id}";
                newGame.IsFriendlyRoom = false;

                lock (users)
                {
                    var u1 = users.FirstOrDefault(u => u.Id == p1);
                    if (u1 != null && !string.IsNullOrEmpty(name1) && !name1.StartsWith("Jugador-")) u1.Name = name1;
                    var u2 = users.FirstOrDefault(u => u.Id == p2);
                    if (u2 != null && !string.IsNullOrEmpty(name2) && !name2.StartsWith("Jugador-")) u2.Name = name2;
                }

                // Sorteo de mano inicial 50% / 50%
                Random rng = new Random();
                int startP = rng.Next(2) == 0 ? 1 : 2;
                newGame.HandStarter = startP;
                newGame.PlayerTurn = (startP == 1);
                newGame.HandCount = 1;

                newGame.Deck.RandomCards();
                newGame.Id = newGame.GenerateSeed(games);
                newGame.ShuffleCards_1vs1();
                games.Add(newGame);

                await Groups.AddToGroupAsync(p1, $"game1vs1_{newGame.Id}");
                await Groups.AddToGroupAsync(p2, $"game1vs1_{newGame.Id}");

                Console.WriteLine($"[Matchmaking] Emparejados {p1} ({name1}) vs {p2} ({name2}). Juego: {newGame.Id}");

                GameMessage msgP1 = new GameMessage
                {
                    game = newGame.Id,
                    order = 99,
                    content = $"{p1}|{name1}|{p2}|{name2}|1"
                };

                GameMessage msgP2 = new GameMessage
                {
                    game = newGame.Id,
                    order = 99,
                    content = $"{p1}|{name1}|{p2}|{name2}|0"
                };

                await Clients.Client(p1).SendAsync("MatchFound", msgP1);
                await Clients.Client(p2).SendAsync("MatchFound", msgP2);
            }
        }

        public async Task CancelMatchmaking()
        {
            string callerId = Context.ConnectionId;
            lock (queueLock)
            {
                matchmakingQueue.RemoveAll(q => q.ConnectionId == callerId);
            }
            Console.WriteLine($"[Matchmaking] Cancelado por cliente: {callerId}");
            await Clients.Caller.SendAsync("MatchmakingStatus", new { status = "canceled", message = "Búsqueda cancelada" });
        }

        // ==========================================
        // GESTIÓN DE DECISIÓN DE TUMBA ("¿Deseas jugar esta ronda?")
        // ==========================================

        public async Task AnswerTumbaDecision(GameMessage move)
        {
            // move.order: 201 = Acepta jugar (-3 si pierde, gana partida si gana)
            //             202 = No acepta jugar (-1 a quien tumba, +1 al rival y nueva mano)
            Console.WriteLine($"AnswerTumbaDecision. Cliente: {Context.ConnectionId}, Orden: {move.order}, Juego: {move.game}");
            int numg = FindGame1vs1(move.game);
            if (numg == -1) return;

            var game = games[numg];
            bool isPlayerOne = (Context.ConnectionId == game.IdPOne);

            if (move.order == 202) // "No juego" -> -1 al que tumba, +1 al rival
            {
                if (isPlayerOne)
                {
                    game.PointsOne = Math.Max(0, game.PointsOne - 1);
                    game.PointsTwo += 1;
                }
                else
                {
                    game.PointsTwo = Math.Max(0, game.PointsTwo - 1);
                    game.PointsOne += 1;
                }

                game.UpdateTumbaStatus();

                // Barajar nueva mano directamente
                game.Deck.RandomCards();
                game.ShuffleCards_1vs1();
                game.PlayerTurn = !game.PlayerTurn;

                GameMessage notify = new GameMessage
                {
                    game = game.Id,
                    order = 203,
                    content = $"{game.PointsOne} {game.PointsTwo} {game.InitHand}"
                };

                await Clients.Client(game.IdPOne).SendAsync("TumbaRoundSkipped", notify);
                await Clients.Client(game.IdPTwo).SendAsync("TumbaRoundSkipped", notify);
            }
            else
            {
                // Acepta jugar la ronda de tumba
                GameMessage notify = new GameMessage
                {
                    game = game.Id,
                    order = 204,
                    content = "accepted"
                };
                await Clients.Client(game.IdPOne).SendAsync("TumbaRoundAccepted", notify);
                await Clients.Client(game.IdPTwo).SendAsync("TumbaRoundAccepted", notify);
            }
        }

        // ==========================================
        // SALAS PRIVADAS AMISTOSAS 1 VS 1
        // ==========================================

        public async Task JoinRoom1v1(string roomName, string playerName, int bet, string userId = "", string avatarUrl = "")
        {
            string callerId = Context.ConnectionId;
            string roomKey = (roomName ?? "sala-1v1").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(playerName) || playerName == "nulo")
            {
                playerName = $"Jugador-{callerId.Substring(0, Math.Min(4, callerId.Length))}";
            }

            Room1v1Session session;
            Seat2v2? assignedSeat = null;
            bool shouldStart = false;

            lock (rooms1v1Lock)
            {
                if (!rooms1v1.TryGetValue(roomKey, out session!))
                {
                    session = new Room1v1Session
                    {
                        RoomName = roomName ?? "sala-1v1",
                        Bet = bet > 0 ? bet : 10
                    };
                    rooms1v1[roomKey] = session;
                }

                // Limpiar desconectados si no ha iniciado la partida (después de 120s)
                if (!session.GameStarted)
                {
                    session.Seats.RemoveAll(s => !s.IsConnected && s.DisconnectedAt.HasValue && (DateTime.UtcNow - s.DisconnectedAt.Value).TotalSeconds > 120);
                }

                // Buscar si ya existe por ConnectionId o UserId
                Seat2v2? existing = session.Seats.FirstOrDefault(s => s.ConnectionId == callerId || (!string.IsNullOrEmpty(userId) && s.UserId == userId));
                if (existing != null)
                {
                    existing.ConnectionId = callerId;
                    existing.IsConnected = true;
                    existing.Name = playerName;
                    existing.DisconnectedAt = null;
                    assignedSeat = existing;
                }
                else if (session.Seats.Count < 2)
                {
                    int nextSeatIdx = session.Seats.Count == 0 ? 0 : (session.Seats[0].SeatIndex == 0 ? 1 : 0);
                    assignedSeat = new Seat2v2
                    {
                        SeatIndex = nextSeatIdx,
                        ConnectionId = callerId,
                        UserId = userId ?? "",
                        Name = playerName,
                        AvatarUrl = avatarUrl ?? "",
                        IsConnected = true
                    };
                    session.Seats.Add(assignedSeat);
                }

                if (session.Seats.Count == 2 && !session.GameStarted)
                {
                    session.GameStarted = true;
                    shouldStart = true;
                }
            }

            await Groups.AddToGroupAsync(callerId, roomKey);

            if (assignedSeat == null)
            {
                await Clients.Caller.SendAsync("RoomFull1v1", new { message = "La sala 1 vs 1 ya cuenta con 2 jugadores completos." });
                return;
            }

            var roomState = new
            {
                roomName = session.RoomName,
                bet = session.Bet,
                seats = session.Seats.OrderBy(s => s.SeatIndex).ToList(),
                mySeatIndex = assignedSeat.SeatIndex,
                isFull = session.Seats.Count >= 2,
                gameStarted = session.GameStarted
            };

            await Clients.Group(roomKey).SendAsync("RoomUpdate1v1", roomState);

            if (shouldStart)
            {
                var seat0 = session.Seats.First(s => s.SeatIndex == 0);
                var seat1 = session.Seats.First(s => s.SeatIndex == 1);

                string p1 = seat0.ConnectionId;
                string p2 = seat1.ConnectionId;
                string name1 = seat0.Name;
                string name2 = seat1.Name;

                GamePlayOneVsOne newGame = new GamePlayOneVsOne(p1, p2);
                newGame.Coins = session.Bet;
                newGame.NamePOne = name1;
                newGame.NamePTwo = name2;
                newGame.UserIdPOne = seat0.UserId;
                newGame.UserIdPTwo = seat1.UserId;
                newGame.RoomName = session.RoomName;
                newGame.IsFriendlyRoom = session.RoomName.StartsWith("sala-", StringComparison.OrdinalIgnoreCase);

                Random rng = new Random();
                int startP = rng.Next(2) == 0 ? 1 : 2;
                newGame.HandStarter = startP;
                newGame.PlayerTurn = (startP == 1);
                newGame.HandCount = 1;

                newGame.Deck.RandomCards();
                newGame.Id = newGame.GenerateSeed(games);
                newGame.ShuffleCards_1vs1();
                games.Add(newGame);
                session.GameId = newGame.Id;

                await Groups.AddToGroupAsync(p1, $"game1vs1_{newGame.Id}");
                await Groups.AddToGroupAsync(p2, $"game1vs1_{newGame.Id}");

                Console.WriteLine($"[Room1v1] Partida iniciada en {roomKey}. Juego #{newGame.Id} ({name1} vs {name2})");

                GameMessage msgP1 = new GameMessage
                {
                    game = newGame.Id,
                    order = 99,
                    content = $"{p1}|{name1}|{p2}|{name2}|1"
                };
                GameMessage msgP2 = new GameMessage
                {
                    game = newGame.Id,
                    order = 99,
                    content = $"{p1}|{name1}|{p2}|{name2}|0"
                };

                await Clients.Client(p1).SendAsync("MatchFound", msgP1);
                await Clients.Client(p2).SendAsync("MatchFound", msgP2);
            }
        }

        public Task<object> GetRoomInfo(string roomName)
        {
            string key = (roomName ?? "").Trim().ToLowerInvariant();
            if (!key.StartsWith("sala-") && !key.StartsWith("match-"))
            {
                key = $"sala-{key}";
            }

            lock (rooms1v1Lock)
            {
                if (rooms1v1.TryGetValue(key, out var s1))
                {
                    return Task.FromResult<object>(new { exists = true, mode = "1v1", roomName = s1.RoomName, count = s1.Seats.Count, isFull = s1.Seats.Count >= 2, gameStarted = s1.GameStarted });
                }
            }

            lock (rooms2v2Lock)
            {
                if (rooms2v2.TryGetValue(key, out var s2))
                {
                    return Task.FromResult<object>(new { exists = true, mode = "2v2", roomName = s2.RoomName, count = s2.Seats.Count, isFull = s2.Seats.Count >= 4, gameStarted = s2.GameStarted });
                }
            }

            return Task.FromResult<object>(new { exists = false });
        }

        public async Task JoinVoice1vs1(int gameId, int seatIndex)
        {
            string roomKey = $"game1vs1_{gameId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);
            Console.WriteLine($"[Voice1vs1] Cliente {Context.ConnectionId} unido al canal de voz {roomKey} en asiento {seatIndex}");
        }

        // ==========================================
        // SALAS MULTIJUGADOR 2 VS 2 (USUARIO VS USUARIO)
        // ==========================================

        public async Task JoinRoom2v2(string roomName, string playerName, int bet, int preferredSlot = -1, string userId = "", string avatarUrl = "")
        {
            string callerId = Context.ConnectionId;
            string roomKey = (roomName ?? "sala-pericon").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(playerName) || playerName == "nulo")
            {
                playerName = $"Jugador-{callerId.Substring(0, Math.Min(4, callerId.Length))}";
            }

            Room2v2Session session;
            bool isReconnecting = false;
            Seat2v2? assignedSeat = null;
            bool shouldStartGame = false;
            bool isRoomFull = false;

            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session!))
                {
                    session = new Room2v2Session
                    {
                        RoomName = roomName ?? "sala-pericon",
                        Bet = bet > 0 ? bet : 100
                    };
                    rooms2v2[roomKey] = session;
                }

                // Limpiar asientos abandonados en el lobby (más de 120s desconectados si la partida no ha iniciado)
                if (!session.GameStarted)
                {
                    session.Seats.RemoveAll(s => !s.IsConnected && s.DisconnectedAt.HasValue && (DateTime.UtcNow - s.DisconnectedAt.Value).TotalSeconds > 120);
                }

                // 1. Buscar si ya existe por ConnectionId
                Seat2v2? existingSeat = session.Seats.FirstOrDefault(s => s.ConnectionId == callerId);

                // 2. Si no, buscar por UserId si viene provisto
                if (existingSeat == null && !string.IsNullOrEmpty(userId))
                {
                    existingSeat = session.Seats.FirstOrDefault(s => !string.IsNullOrEmpty(s.UserId) && s.UserId == userId);
                }

                // 3. Si no, buscar por nombre (si no es genérico)
                if (existingSeat == null && !string.IsNullOrEmpty(playerName) && playerName != "Jugador" && !playerName.StartsWith("Jugador-"))
                {
                    existingSeat = session.Seats.FirstOrDefault(s => s.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
                }

                // 4. Si sigue sin encontrar y hay preferredSlot, verificar si ese asiento está libre o desconectado
                if (existingSeat == null && preferredSlot >= 0 && preferredSlot <= 3)
                {
                    var slotSeat = session.Seats.FirstOrDefault(s => s.SeatIndex == preferredSlot);
                    if (slotSeat != null && (!slotSeat.IsConnected || slotSeat.ConnectionId == callerId))
                    {
                        // Solo reclamar si no pertenece a otro usuario registrado diferente
                        if (string.IsNullOrEmpty(slotSeat.UserId) || slotSeat.UserId == userId)
                        {
                            existingSeat = slotSeat;
                        }
                    }
                }

                // 5. Si la partida ya inició y hay un asiento desconectado, verificar si coincide con el usuario
                if (existingSeat == null && session.GameStarted)
                {
                    existingSeat = session.Seats.FirstOrDefault(s => !s.IsConnected &&
                        ((!string.IsNullOrEmpty(userId) && s.UserId == userId) ||
                         (!string.IsNullOrEmpty(playerName) && playerName != "Jugador" && !playerName.StartsWith("Jugador-") && s.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase))));

                    // Si no hubo coincidencia exacta, ocupar un asiento desconectado anónimo si no tiene dueño registrado
                    if (existingSeat == null)
                    {
                        existingSeat = session.Seats.FirstOrDefault(s => !s.IsConnected && (string.IsNullOrEmpty(s.UserId) || s.UserId == userId));
                    }
                }

                if (existingSeat != null)
                {
                    existingSeat.ConnectionId = callerId;
                    if (!string.IsNullOrEmpty(userId)) existingSeat.UserId = userId;
                    if (!string.IsNullOrEmpty(playerName) && playerName != "Jugador" && !playerName.StartsWith("Jugador-"))
                    {
                        existingSeat.Name = playerName;
                    }
                    if (!string.IsNullOrEmpty(avatarUrl))
                    {
                        existingSeat.AvatarUrl = avatarUrl;
                    }
                    existingSeat.IsConnected = true;
                    existingSeat.DisconnectedAt = null;
                    assignedSeat = existingSeat;
                    isReconnecting = session.GameStarted;
                }
                else
                {
                    var takenIndices = session.Seats.Select(s => s.SeatIndex).ToHashSet();
                    int freeSeat = -1;

                    // Si el usuario solicita un preferredSlot (0 = Anfitrión, 1 = Rival 1, 2 = Compañero, 3 = Rival 2)
                    if (preferredSlot >= 0 && preferredSlot <= 3 && !takenIndices.Contains(preferredSlot))
                    {
                        freeSeat = preferredSlot;
                    }
                    else
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (!takenIndices.Contains(i))
                            {
                                freeSeat = i;
                                break;
                            }
                        }
                    }

                    if (freeSeat != -1)
                    {
                        int team = (freeSeat == 0 || freeSeat == 2) ? 1 : 2;
                        string role = freeSeat switch
                        {
                            0 => "Anfitrión",
                            1 => "Rival 1",
                            2 => "Compañero",
                            3 => "Rival 2",
                            _ => "Jugador"
                        };

                        assignedSeat = new Seat2v2
                        {
                            SeatIndex = freeSeat,
                            ConnectionId = callerId,
                            UserId = userId ?? "",
                            Name = playerName,
                            AvatarUrl = avatarUrl ?? "",
                            Team = team,
                            Role = role,
                            IsReady = true,
                            IsConnected = true
                        };
                        session.Seats.Add(assignedSeat);
                    }
                }

                // Si la sala ya tiene 4 jugadores y el cliente no pudo ser asignado a ningún asiento
                if (assignedSeat == null && session.Seats.Count >= 4)
                {
                    isRoomFull = true;
                    return;
                }

                // Verificar si se completaron los 4 jugadores conectados para iniciar la partida
                if (session.Seats.Count >= 4 && !session.GameStarted && !session.IsStarting && session.Seats.All(s => s.IsConnected))
                {
                    session.IsStarting = true;
                    session.GameStarted = true;
                    shouldStartGame = true;
                }
            }

            if (isRoomFull)
            {
                await Clients.Caller.SendAsync("RoomFull2v2", new { message = "La sala ya está completa con 4 jugadores." });
                return;
            }

            await Groups.AddToGroupAsync(callerId, roomKey);

            var roomState = new
            {
                roomName = session.RoomName,
                bet = session.Bet,
                seats = session.Seats.OrderBy(s => s.SeatIndex).ToList(),
                isFull = session.Seats.Count >= 4,
                gameStarted = session.GameStarted
            };

            await Clients.Group(roomKey).SendAsync("RoomUpdate2v2", roomState);

            // Si es reconexión durante partida en curso
            if (isReconnecting && assignedSeat != null)
            {
                await Clients.Group(roomKey).SendAsync("PlayerReconnectedNotice2v2", new
                {
                    seatIndex = assignedSeat.SeatIndex,
                    name = assignedSeat.Name,
                    message = $"✅ {assignedSeat.Name} se ha reconectado. ¡La partida continúa!"
                });

                var reconnectState = new
                {
                    roomName = session.RoomName,
                    mySeatIndex = assignedSeat.SeatIndex,
                    myRole = assignedSeat.Role,
                    myTeam = assignedSeat.Team,
                    initHand = session.CurrentInitHand,
                    bet = session.Bet,
                    starterPlayer = session.LeadPlayer,
                    seats = session.Seats.OrderBy(s => s.SeatIndex).ToList(),
                    pointsTeam1 = session.PointsTeam1,
                    pointsTeam2 = session.PointsTeam2,
                    tricksTeam1 = session.TricksTeam1,
                    tricksTeam2 = session.TricksTeam2,
                    currentStake = session.CurrentStake,
                    pendingStake = session.PendingStake,
                    stakeAskerSeat = session.StakeAskerSeat,
                    stakeAskerTeam = session.StakeAskerTeam,
                    lastStakeTeam = session.LastStakeTeam,
                    currentTurn = session.CurrentTurn,
                    currentTrick = session.CurrentTrick,
                    handPlayedCards = session.HandHistoryCards
                };
                await Clients.Caller.SendAsync("GameReconnectedState2v2", reconnectState);
                return;
            }

            // Si la partida ya fue iniciada previamente y no es reconexión
            if (session.GameStarted && !string.IsNullOrEmpty(session.CurrentInitHand))
            {
                var startedPayload = new
                {
                    roomName = session.RoomName,
                    initHand = session.CurrentInitHand,
                    starterPlayer = session.LeadPlayer,
                    bet = session.Bet,
                    seats = session.Seats.OrderBy(s => s.SeatIndex).ToList()
                };
                await Clients.Caller.SendAsync("GameStarted2v2", startedPayload);
            }
            else if (shouldStartGame)
            {
                await StartGame2v2Internal(roomKey);
            }
        }

        public async Task SwitchSeat2v2(string roomName, int targetSeat)
        {
            string callerId = Context.ConnectionId;
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();

            Room2v2Session? session;
            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session) || session.GameStarted) return;

                var mySeat = session.Seats.FirstOrDefault(s => s.ConnectionId == callerId);
                if (mySeat == null) return;

                if (targetSeat >= 0 && targetSeat <= 3 && !session.Seats.Any(s => s.SeatIndex == targetSeat))
                {
                    mySeat.SeatIndex = targetSeat;
                    mySeat.Team = (targetSeat == 0 || targetSeat == 2) ? 1 : 2;
                    mySeat.Role = targetSeat switch
                    {
                        0 => "Anfitrión",
                        1 => "Rival 1",
                        2 => "Compañero",
                        3 => "Rival 2",
                        _ => "Jugador"
                    };
                }
            }

            var roomState = new
            {
                roomName = session.RoomName,
                bet = session.Bet,
                seats = session.Seats.OrderBy(s => s.SeatIndex).ToList(),
                isFull = session.Seats.Count >= 4,
                gameStarted = session.GameStarted
            };
            await Clients.Group(roomKey).SendAsync("RoomUpdate2v2", roomState);
        }

        public async Task StartGame2v2(string roomName)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            await StartGame2v2Internal(roomKey);
        }

        public async Task RequestRevancha2v2(string roomName, int requesterSeat, string requesterName)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            GameLogger.Log(0, "RequestRevancha2v2", $"Sala: {roomKey}, Seat: {requesterSeat}, Nombre: {requesterName}");

            Room2v2Session? session;
            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session)) return;
            }

            int requesterTeam = (requesterSeat == 0 || requesterSeat == 2) ? 1 : 2;
            string reqName = !string.IsNullOrWhiteSpace(requesterName)
                ? requesterName
                : (session.Seats.FirstOrDefault(s => s.SeatIndex == requesterSeat)?.Name ?? "Un rival");

            await Clients.Group(roomKey).SendAsync("RevanchaRequested2v2", new
            {
                roomName = session.RoomName,
                requesterSeat = requesterSeat,
                requesterName = reqName,
                requesterTeam = requesterTeam
            });
        }

        public async Task AnswerRevancha2v2(string roomName, int responderSeat, string responderName, bool accepted)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            GameLogger.Log(0, "AnswerRevancha2v2", $"Sala: {roomKey}, Seat: {responderSeat}, Aceptado: {accepted}");

            Room2v2Session? session;
            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session)) return;
            }

            string respName = !string.IsNullOrWhiteSpace(responderName)
                ? responderName
                : (session.Seats.FirstOrDefault(s => s.SeatIndex == responderSeat)?.Name ?? "Un jugador");

            if (!accepted)
            {
                await Clients.Group(roomKey).SendAsync("RevanchaRejected2v2", new
                {
                    roomName = session.RoomName,
                    responderSeat = responderSeat,
                    responderName = respName
                });
                return;
            }

            await Clients.Group(roomKey).SendAsync("RevanchaAccepted2v2", new
            {
                roomName = session.RoomName,
                responderSeat = responderSeat,
                responderName = respName
            });

            await StartGame2v2Internal(roomKey);
        }

        private async Task StartGame2v2Internal(string roomKey)
        {
            Room2v2Session? session;
            string initHand = "";
            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session) || session.Seats.Count < 4) return;
                session.GameStarted = true;
                session.IsStarting = false;
                session.PointsTeam1 = 0;
                session.PointsTeam2 = 0;
                session.TricksTeam1 = 0;
                session.TricksTeam2 = 0;
                session.CurrentStake = 1;
                session.PendingStake = 0;
                session.StakeAskerSeat = -1;
                session.StakeAskerTeam = 0;
                session.LastStakeTeam = 0;
                session.CurrentTurn = 0;
                session.LeadPlayer = 0;
                session.CurrentTrick.Clear();
                session.HandHistoryCards.Clear();
                session.HandCount = 1;
                session.LastHandDealtAt = DateTime.UtcNow;
                session.LastHandStarter = 0;
                session.IsHandResolving = false;
                session.IsTumbaTeam1 = false;
                session.IsTumbaTeam2 = false;
                session.IsTumbaDeParaAtrasTeam1 = false;
                session.IsTumbaDeParaAtrasTeam2 = false;
                session.IsTumbaDecisionPending = false;

                // Barajar 12 cartas para los 4 jugadores + 1 Vida
                SpanishCards deck = new SpanishCards();
                deck.RandomCards();
                List<Card> c0 = new List<Card>();
                List<Card> c1 = new List<Card>();
                List<Card> c2 = new List<Card>();
                List<Card> c3 = new List<Card>();
                for (int r = 0; r < 3; r++)
                {
                    c0.Add(deck.OutCard());
                    c1.Add(deck.OutCard());
                    c2.Add(deck.OutCard());
                    c3.Add(deck.OutCard());
                }
                Card life = deck.OutCard();

                List<string> parts = new List<string>();
                foreach (var c in c0) parts.Add(c.Id.ToString("D2"));
                foreach (var c in c1) parts.Add(c.Id.ToString("D2"));
                foreach (var c in c2) parts.Add(c.Id.ToString("D2"));
                foreach (var c in c3) parts.Add(c.Id.ToString("D2"));
                parts.Add(life.Id.ToString("D2"));

                initHand = string.Join("-", parts);
                session.CurrentInitHand = initHand;
            }

            var payload = new
            {
                roomName = session.RoomName,
                initHand = initHand,
                starterPlayer = 0, // Inicia Seat 0 (Anfitrión)
                bet = session.Bet,
                seats = session.Seats.OrderBy(s => s.SeatIndex).ToList(),
                pointsTeam1 = session.PointsTeam1,
                pointsTeam2 = session.PointsTeam2,
                isTumbaTeam1 = false,
                isTumbaTeam2 = false,
                isTumbaDeParaAtrasTeam1 = false,
                isTumbaDeParaAtrasTeam2 = false
            };

            if (_staticHubContext != null)
            {
                await _staticHubContext.Clients.Group(roomKey).SendAsync("GameStarted2v2", payload);
            }
            else
            {
                await Clients.Group(roomKey).SendAsync("GameStarted2v2", payload);
            }
        }

        public static async Task DealNewHand2v2Static(string roomName, int starterPlayer)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            Room2v2Session? session;
            string initHand = "";

            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session)) return;
                // Evitar repartir duplicado si múltiples llamadas ocurren en menos de 2s
                if ((DateTime.UtcNow - session.LastHandDealtAt).TotalMilliseconds < 2000)
                {
                    return;
                }
                session.LastHandDealtAt = DateTime.UtcNow;

                session.TricksTeam1 = 0;
                session.TricksTeam2 = 0;
                session.CurrentStake = 1;
                session.PendingStake = 0;
                session.StakeAskerSeat = -1;
                session.StakeAskerTeam = 0;
                session.LastStakeTeam = 0;
                session.LeadPlayer = starterPlayer;
                session.CurrentTurn = starterPlayer;
                session.LastHandStarter = starterPlayer;
                session.IsHandResolving = false;
                session.CurrentTrick.Clear();
                session.HandHistoryCards.Clear();
                session.HandCount++;

                // Actualizar estado oficial de Tumba antes de iniciar la mano
                session.UpdateTumbaStatus();
                session.IsTumbaDecisionPending = session.IsTumbaTeam1 || session.IsTumbaTeam2;

                SpanishCards deck = new SpanishCards();
                deck.RandomCards();
                List<Card> c0 = new List<Card>();
                List<Card> c1 = new List<Card>();
                List<Card> c2 = new List<Card>();
                List<Card> c3 = new List<Card>();
                for (int r = 0; r < 3; r++)
                {
                    c0.Add(deck.OutCard());
                    c1.Add(deck.OutCard());
                    c2.Add(deck.OutCard());
                    c3.Add(deck.OutCard());
                }
                Card life = deck.OutCard();

                List<string> parts = new List<string>();
                foreach (var c in c0) parts.Add(c.Id.ToString("D2"));
                foreach (var c in c1) parts.Add(c.Id.ToString("D2"));
                foreach (var c in c2) parts.Add(c.Id.ToString("D2"));
                foreach (var c in c3) parts.Add(c.Id.ToString("D2"));
                parts.Add(life.Id.ToString("D2"));

                initHand = string.Join("-", parts);
                session.CurrentInitHand = initHand;
            }

            var payload = new
            {
                roomName = session.RoomName,
                initHand = initHand,
                starterPlayer = starterPlayer,
                pointsTeam1 = session.PointsTeam1,
                pointsTeam2 = session.PointsTeam2,
                isTumbaTeam1 = session.IsTumbaTeam1,
                isTumbaTeam2 = session.IsTumbaTeam2,
                isTumbaDeParaAtrasTeam1 = session.IsTumbaDeParaAtrasTeam1,
                isTumbaDeParaAtrasTeam2 = session.IsTumbaDeParaAtrasTeam2
            };

            if (_staticHubContext != null)
            {
                Console.WriteLine($"[DealNewHand2v2] Enviando NewHandDealt2v2 a sala '{roomKey}', starter={starterPlayer}, mano={session.HandCount}");
                await _staticHubContext.Clients.Group(roomKey).SendAsync("NewHandDealt2v2", payload);
            }
            else
            {
                Console.WriteLine($"[DealNewHand2v2 WARN] _staticHubContext es null para sala '{roomKey}'");
            }
        }

        public async Task DealNewHand2v2(string roomName, int starterPlayer)
        {
            await DealNewHand2v2Static(roomName, starterPlayer);
        }

        public async Task RequestNewHand2v2(string roomName)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            Room2v2Session? session;
            int nextStarter = 0;
            bool shouldDeal = false;
            bool shouldRebroadcast = false;

            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session)) return;
                if (!session.GameStarted) return;
                if (session.IsGameOver) return;

                // Si la mano concluyó y está en resolución, o si todos jugaron sus cartas y no hay bazas activas
                if (session.IsHandResolving || (session.CurrentTrick.Count == 0 && session.HandHistoryCards.Count >= 8))
                {
                    nextStarter = (session.LastHandStarter + 1) % 4;
                    shouldDeal = true;
                }
                else if (session.HandHistoryCards.Count == 0 && !string.IsNullOrEmpty(session.CurrentInitHand))
                {
                    // La mano ya fue repartida recientemente, retransmitir al cliente que pudo perderla
                    shouldRebroadcast = true;
                }
            }

            if (shouldDeal)
            {
                Console.WriteLine($"[RequestNewHand2v2] Forzando reparto de nueva mano para sala '{roomKey}', starter={nextStarter}");
                await DealNewHand2v2Static(roomKey, nextStarter);
            }
            else if (shouldRebroadcast && session != null)
            {
                Console.WriteLine($"[RequestNewHand2v2] Reenviando NewHandDealt2v2 a sala '{roomKey}'");
                var payload = new
                {
                    roomName = session.RoomName,
                    initHand = session.CurrentInitHand,
                    starterPlayer = session.LastHandStarter,
                    pointsTeam1 = session.PointsTeam1,
                    pointsTeam2 = session.PointsTeam2,
                    isTumbaTeam1 = session.IsTumbaTeam1,
                    isTumbaTeam2 = session.IsTumbaTeam2,
                    isTumbaDeParaAtrasTeam1 = session.IsTumbaDeParaAtrasTeam1,
                    isTumbaDeParaAtrasTeam2 = session.IsTumbaDeParaAtrasTeam2
                };
                if (_staticHubContext != null)
                {
                    await _staticHubContext.Clients.Group(roomKey).SendAsync("NewHandDealt2v2", payload);
                }
            }
        }

        public async Task PlayCard2v2(string roomName, int seatIndex, int cardId)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            Room2v2Session? session;
            bool trickCompleted = false;
            int bestCardId = 0;
            int bestSeat = 0;
            int winningTeam = 0;
            int t1Tricks = 0;
            int t2Tricks = 0;
            int pT1 = 0;
            int pT2 = 0;
            bool handCompleted = false;
            int handWinningTeam = 0;
            bool isGameOver = false;
            int winningTeamOfMatch = 0;
            int fallenInTumbaTeam = 0;
            bool isCogida2v2 = false;
            int cogidaTeam = 0;

            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session)) return;
                if (!session.GameStarted) return;
                if (seatIndex < 0 || seatIndex > 3) return;
                // Bloquear jugar cartas mientras hay un cante pendiente de respuesta, la mano está resolviendo o la partida terminó
                if (session.PendingStake > 0 || session.IsGameOver || session.IsHandResolving) return;
                // Validar que sea el turno de este asiento
                if (session.CurrentTurn != seatIndex) return;
                // Evitar que el mismo jugador juegue dos cartas en la misma baza
                if (session.CurrentTrick.Any(p => p.SeatIndex == seatIndex)) return;
                // Evitar que se juegue una carta ya utilizada en esta mano
                if (session.HandHistoryCards.Any(p => p.CardId == cardId)) return;

                var playedDto = new PlayedCard2v2Dto { SeatIndex = seatIndex, CardId = cardId };
                session.CurrentTrick.Add(playedDto);
                session.HandHistoryCards.Add(playedDto);
                session.CurrentTurn = (seatIndex + 1) % 4;

                // Si se completaron las 4 cartas de la baza: resolución oficial de la baza en el servidor
                if (session.CurrentTrick.Count == 4)
                {
                    trickCompleted = true;
                    int leadCardId = session.CurrentTrick[0].CardId;
                    bestCardId = session.CurrentTrick[0].CardId;
                    bestSeat = session.CurrentTrick[0].SeatIndex;

                    // Extraer la carta de La Vida (último token de CurrentInitHand)
                    int lifeCardId = 0;
                    var tokens = session.CurrentInitHand.Split('-');
                    if (tokens.Length > 0)
                    {
                        int.TryParse(tokens[tokens.Length - 1], out lifeCardId);
                    }

                    for (int i = 1; i < session.CurrentTrick.Count; i++)
                    {
                        int candCardId = session.CurrentTrick[i].CardId;
                        int candSeat = session.CurrentTrick[i].SeatIndex;

                        if (GamePlayTwoVsTwo.DoesCandidateBeatBest(bestCardId, candCardId, leadCardId, lifeCardId))
                        {
                            bestCardId = candCardId;
                            bestSeat = candSeat;
                        }
                    }

                    winningTeam = (bestSeat == 0 || bestSeat == 2) ? 1 : 2;
                    if (winningTeam == 1) session.TricksTeam1++;
                    else session.TricksTeam2++;

                    t1Tricks = session.TricksTeam1;
                    t2Tricks = session.TricksTeam2;

                    // Verificación de La Cogía (10 de Oro matado con 1 de Oro de equipo rival)
                    // En tumba la cogía NO vale (innecesario adquirir 3 puntos)
                    bool isTumba2v2 = session.IsTumbaTeam1 || session.IsTumbaTeam2 ||
                                      session.PointsTeam1 >= 9 || session.PointsTeam2 >= 9 ||
                                      (session.IsTumbaDeParaAtrasTeam1 && session.PointsTeam1 == 8) ||
                                      (session.IsTumbaDeParaAtrasTeam2 && session.PointsTeam2 == 8);

                    int tenGoldIdx = session.CurrentTrick.FindIndex(p => p.CardId == 7);
                    int oneGoldIdx = session.CurrentTrick.FindIndex(p => p.CardId == 0);
                    if (!isTumba2v2 && tenGoldIdx != -1 && oneGoldIdx != -1)
                    {
                        int tenSeat = session.CurrentTrick[tenGoldIdx].SeatIndex;
                        int oneSeat = session.CurrentTrick[oneGoldIdx].SeatIndex;
                        int tenTeam = (tenSeat == 0 || tenSeat == 2) ? 1 : 2;
                        int oneTeam = (oneSeat == 0 || oneSeat == 2) ? 1 : 2;
                        if (tenTeam != oneTeam)
                        {
                            isCogida2v2 = true;
                            cogidaTeam = oneTeam; // El equipo que tiene el 1 de Oro siempre gana La Cogía
                            int oldT1C = session.PointsTeam1;
                            int oldT2C = session.PointsTeam2;
                            if (cogidaTeam == 1) session.PointsTeam1 += 3;
                            else session.PointsTeam2 += 3;
                            session.UpdateTumbaStatus(oldT1C, oldT2C);
                            Console.WriteLine($"[La Cogia 2v2] ¡Equipo {cogidaTeam} se acredita +3 piedras por La Cogía!");
                        }
                    }

                    session.LeadPlayer = bestSeat;
                    session.CurrentTurn = bestSeat;
                    session.CurrentTrick.Clear();

                    // Comprobar si concluyó la mano (2 bazas ganadas o 3 jugadas)
                    if (session.TricksTeam1 >= 2 || session.TricksTeam2 >= 2 || (session.TricksTeam1 + session.TricksTeam2 >= 3))
                    {
                        handCompleted = true;
                        session.IsHandResolving = true;
                        handWinningTeam = session.TricksTeam1 > session.TricksTeam2 ? 1 : 2;

                        int oldT1 = session.PointsTeam1;
                        int oldT2 = session.PointsTeam2;

                        bool wasInTumbaT1 = session.IsTumbaTeam1;
                        bool wasInTumbaT2 = session.IsTumbaTeam2;
                        bool isObligado = wasInTumbaT1 && wasInTumbaT2;

                        if (isObligado)
                        {
                            isGameOver = true;
                            winningTeamOfMatch = handWinningTeam;
                            if (handWinningTeam == 1) session.PointsTeam1 = 10;
                            else session.PointsTeam2 = 10;
                        }
                        else if (wasInTumbaT1)
                        {
                            if (handWinningTeam == 1)
                            {
                                isGameOver = true;
                                winningTeamOfMatch = 1;
                                session.PointsTeam1 = 10;
                            }
                            else
                            {
                                // Equipo 1 estaba en Tumba y perdió la mano: Cae en Tumba (-3 pts para él, +3 para Equipo 2)
                                fallenInTumbaTeam = 1;
                                session.PointsTeam1 = Math.Max(0, session.PointsTeam1 - 3);
                                session.PointsTeam2 += 3;
                                session.UpdateTumbaStatus(oldT1, oldT2);
                            }
                        }
                        else if (wasInTumbaT2)
                        {
                            if (handWinningTeam == 2)
                            {
                                isGameOver = true;
                                winningTeamOfMatch = 2;
                                session.PointsTeam2 = 10;
                            }
                            else
                            {
                                // Equipo 2 estaba en Tumba y perdió la mano: Cae en Tumba (-3 pts para él, +3 para Equipo 1)
                                fallenInTumbaTeam = 2;
                                session.PointsTeam2 = Math.Max(0, session.PointsTeam2 - 3);
                                session.PointsTeam1 += 3;
                                session.UpdateTumbaStatus(oldT1, oldT2);
                            }
                        }
                        else
                        {
                            // Mano normal: suma valor de la apuesta stake. Si llega a >= 9, entra en Tumba para la siguiente mano pero NO gana la partida aún.
                            int stake = session.CurrentStake > 0 ? session.CurrentStake : 1;
                            if (handWinningTeam == 1)
                            {
                                session.PointsTeam1 += stake;
                            }
                            else
                            {
                                session.PointsTeam2 += stake;
                            }
                            session.UpdateTumbaStatus(oldT1, oldT2);
                        }

                        if (isGameOver)
                        {
                            session.IsGameOver = true;
                            session.WinningTeam = winningTeamOfMatch;
                        }
                    }

                    pT1 = session.PointsTeam1;
                    pT2 = session.PointsTeam2;
                }
            }

            // Notificar a todos que se jugó la carta
            await Clients.Group(roomKey).SendAsync("CardPlayed2v2", new
            {
                seatIndex,
                cardId
            });

            if (trickCompleted)
            {
                await Clients.Group(roomKey).SendAsync("TrickFinished2v2", new
                {
                    winningSeat = bestSeat,
                    winningTeam = winningTeam,
                    winningCardId = bestCardId,
                    tricksTeam1 = t1Tricks,
                    tricksTeam2 = t2Tricks,
                    pointsTeam1 = pT1,
                    pointsTeam2 = pT2,
                    isCogida = isCogida2v2,
                    cogidaTeam = cogidaTeam,
                    isGameOver = isGameOver,
                    winningTeamOfMatch = winningTeamOfMatch
                });

                if (handCompleted && session != null)
                {
                    await Clients.Group(roomKey).SendAsync("HandFinished2v2", new
                    {
                        handWinningTeam,
                        pointsTeam1 = pT1,
                        pointsTeam2 = pT2,
                        isTumbaTeam1 = session.IsTumbaTeam1,
                        isTumbaTeam2 = session.IsTumbaTeam2,
                        isTumbaDeParaAtrasTeam1 = session.IsTumbaDeParaAtrasTeam1,
                        isTumbaDeParaAtrasTeam2 = session.IsTumbaDeParaAtrasTeam2,
                        fallenInTumbaTeam,
                        isGameOver,
                        winningTeamOfMatch,
                        message = fallenInTumbaTeam > 0
                            ? $"¡El Equipo {(fallenInTumbaTeam == 1 ? "Azul" : "Rojo")} cayó en Tumba (-3 piedras para ellos, +3 para el rival)!"
                            : (isGameOver ? $"¡El Equipo {(winningTeamOfMatch == 1 ? "Azul" : "Rojo")} ha ganado la partida!" : "")
                    });

                    if (isGameOver)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ProcessMatchPayout2v2(roomKey, session, winningTeamOfMatch, "Victoria 2 vs 2 por límite de puntos");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ProcessMatchPayout2v2 PlayCard Error]: {ex}");
                            }
                        });
                    }
                    else
                    {
                        int nextStarter = (session.LastHandStarter + 1) % 4;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(4000);
                                await DealNewHand2v2Static(roomKey, nextStarter);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[DealNewHand2v2 PlayCard Error]: {ex}");
                            }
                        });
                    }
                }
            }
        }

        public async Task PedirStake2v2(string roomName, int seatIndex, int nextStake)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            Room2v2Session? session;
            int askerTeam = (seatIndex == 0 || seatIndex == 2) ? 1 : 2;

            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session)) return;
                if (!session.GameStarted) return;
                // Si ya hay un cante pendiente de respuesta, ignorar para evitar solapamientos
                if (session.PendingStake > 0) return;
                // En Tumba no se permite pedir (si algún equipo tiene 9 o más piedras o está en tumba de para atrás)
                if (session.PointsTeam1 >= 9 || session.PointsTeam2 >= 9 ||
                    session.IsTumbaTeam1 || session.IsTumbaTeam2 ||
                    (session.IsTumbaDeParaAtrasTeam1 && session.PointsTeam1 == 8) ||
                    (session.IsTumbaDeParaAtrasTeam2 && session.PointsTeam2 == 8)) return;
                // No se puede pedir más allá de 9
                if (session.CurrentStake >= 9) return;
                // El equipo que cantó el último aumento no puede auto-aumentar
                if (session.LastStakeTeam == askerTeam && session.CurrentStake > 1) return;

                // Validar secuencia obligatoria de apuestas de Pericón: 1 -> 3 -> 6 -> 9
                int expectedNextStake = session.CurrentStake == 1 ? 3 : (session.CurrentStake == 3 ? 6 : 9);
                if (nextStake != expectedNextStake) return;

                session.PendingStake = nextStake;
                session.StakeAskerSeat = seatIndex;
                session.StakeAskerTeam = askerTeam;
            }

            await Clients.Group(roomKey).SendAsync("StakeAsked2v2", new
            {
                seatIndex,
                nextStake
            });
        }

        public async Task AnswerStake2v2(string roomName, int seatIndex, bool accepted)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            Room2v2Session? session;
            int askerTeam = 0;
            int reward = 0;
            int finalStake = 1;
            bool wasPending = false;
            bool isGameOver = false;
            int winningTeamOfMatch = 0;

            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session)) return;
                // Si ya no está pendiente (porque el compañero ya respondió), ignorar
                if (session.PendingStake == 0) return;

                int responderTeam = (seatIndex == 0 || seatIndex == 2) ? 1 : 2;
                // Solo el equipo rival al que pidió puede responder
                if (responderTeam == session.StakeAskerTeam) return;

                wasPending = true;
                askerTeam = session.StakeAskerTeam;

                if (accepted)
                {
                    session.CurrentStake = session.PendingStake;
                    session.LastStakeTeam = askerTeam;
                    finalStake = session.CurrentStake;
                    session.PendingStake = 0;
                    session.StakeAskerSeat = -1;
                }
                else
                {
                    // "NO QUIERO": El equipo retador gana la mano inmediatamente
                    // El valor ganado es la apuesta previa que ya estaba aceptada
                    reward = session.CurrentStake == 1 ? 1 : (session.CurrentStake == 3 ? 3 : 6);
                    int oldT1 = session.PointsTeam1;
                    int oldT2 = session.PointsTeam2;
                    if (askerTeam == 1)
                    {
                        session.PointsTeam1 += reward;
                    }
                    else
                    {
                        session.PointsTeam2 += reward;
                    }
                    session.UpdateTumbaStatus(oldT1, oldT2);

                    finalStake = session.CurrentStake;
                    session.PendingStake = 0;
                    session.StakeAskerSeat = -1;
                    session.LastStakeTeam = 0;

                    // Limpiar bazas de la mano actual en el servidor
                    session.CurrentTrick.Clear();
                    session.TricksTeam1 = 0;
                    session.TricksTeam2 = 0;
                    session.CurrentStake = 1;
                }
            }

            if (!wasPending) return;

            await Clients.Group(roomKey).SendAsync("StakeAnswered2v2", new
            {
                seatIndex,
                accepted,
                currentStake = finalStake,
                challengerTeam = askerTeam,
                reward,
                pointsTeam1 = session.PointsTeam1,
                pointsTeam2 = session.PointsTeam2,
                isTumbaTeam1 = session.IsTumbaTeam1,
                isTumbaTeam2 = session.IsTumbaTeam2,
                isTumbaDeParaAtrasTeam1 = session.IsTumbaDeParaAtrasTeam1,
                isTumbaDeParaAtrasTeam2 = session.IsTumbaDeParaAtrasTeam2,
                isGameOver,
                winningTeamOfMatch
            });

            if (reward > 0 && isGameOver && session != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessMatchPayout2v2(roomKey, session, winningTeamOfMatch, "Victoria 2 vs 2 por rechazo de cante");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ProcessMatchPayout2v2 AnswerStake Error]: {ex}");
                    }
                });
            }
            else if (reward > 0 && !isGameOver && session != null)
            {
                int nextStarter = (session.LastHandStarter + 1) % 4;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(3000);
                        await DealNewHand2v2Static(roomKey, nextStarter);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DealNewHand2v2 AnswerStake Error]: {ex}");
                    }
                });
            }
        }

        public async Task PassTumba2v2(string roomName, int seatIndex)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            Room2v2Session? session;
            int passingTeam = (seatIndex == 0 || seatIndex == 2) ? 1 : 2;
            int oldT1 = 0, oldT2 = 0;

            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session)) return;
                // Descartar llamadas dobles si ambos compañeros presionan pasar
                if (!session.IsTumbaDecisionPending) return;
                session.IsTumbaDecisionPending = false;

                oldT1 = session.PointsTeam1;
                oldT2 = session.PointsTeam2;

                if (passingTeam == 1)
                {
                    session.PointsTeam1 = Math.Max(0, session.PointsTeam1 - 1);
                    session.PointsTeam2 += 1;
                }
                else
                {
                    session.PointsTeam2 = Math.Max(0, session.PointsTeam2 - 1);
                    session.PointsTeam1 += 1;
                }

                session.UpdateTumbaStatus(oldT1, oldT2);
            }

            bool isGameOver = false;
            int winningTeam = 0;

            if (session.PointsTeam1 >= 12)
            {
                isGameOver = true;
                winningTeam = 1;
                session.IsGameOver = true;
                session.WinningTeam = 1;
            }
            else if (session.PointsTeam2 >= 12)
            {
                isGameOver = true;
                winningTeam = 2;
                session.IsGameOver = true;
                session.WinningTeam = 2;
            }

            await Clients.Group(roomKey).SendAsync("TumbaPassedNotice2v2", new
            {
                seatIndex,
                passingTeam,
                pointsTeam1 = session.PointsTeam1,
                pointsTeam2 = session.PointsTeam2,
                isTumbaTeam1 = session.IsTumbaTeam1,
                isTumbaTeam2 = session.IsTumbaTeam2,
                isTumbaDeParaAtrasTeam1 = session.IsTumbaDeParaAtrasTeam1,
                isTumbaDeParaAtrasTeam2 = session.IsTumbaDeParaAtrasTeam2,
                isGameOver,
                winningTeam,
                message = $"El Equipo {(passingTeam == 1 ? "Azul" : "Rojo")} pasó en Tumba (-1 piedra para ellos, +1 para el rival)."
            });

            if (isGameOver)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessMatchPayout2v2(roomKey, session, winningTeam, "Victoria 2 vs 2 por pase en Tumba");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ProcessMatchPayout2v2 PassTumba Error]: {ex}");
                    }
                });
            }
            else
            {
                int nextStarter = (session.LastHandStarter + 1) % 4;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(2500);
                        await DealNewHand2v2Static(roomKey, nextStarter);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DealNewHand2v2 PassTumba Error]: {ex}");
                    }
                });
            }
        }

        public async Task AcceptTumba2v2(string roomName, int seatIndex)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            int acceptingTeam = (seatIndex == 0 || seatIndex == 2) ? 1 : 2;

            lock (rooms2v2Lock)
            {
                if (rooms2v2.TryGetValue(roomKey, out var session))
                {
                    session.IsTumbaDecisionPending = false;
                }
            }

            await Clients.Group(roomKey).SendAsync("TumbaAcceptedNotice2v2", new
            {
                seatIndex,
                acceptingTeam,
                message = $"El Equipo {(acceptingTeam == 1 ? "Azul" : "Rojo")} aceptó jugar la mano en Tumba."
            });
        }

        public Task UpdatePoints2v2(string roomName, int pointsT1, int pointsT2)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            lock (rooms2v2Lock)
            {
                if (rooms2v2.TryGetValue(roomKey, out var session))
                {
                    session.PointsTeam1 = pointsT1;
                    session.PointsTeam2 = pointsT2;
                }
            }
            return Task.CompletedTask;
        }

        public async Task LeaveRoom2v2(string roomName)
        {
            string callerId = Context.ConnectionId;
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();

            Room2v2Session? session = null;
            lock (rooms2v2Lock)
            {
                if (rooms2v2.TryGetValue(roomKey, out session))
                {
                    if (session.GameStarted)
                    {
                        var seat = session.Seats.FirstOrDefault(s => s.ConnectionId == callerId);
                        if (seat != null)
                        {
                            seat.IsConnected = false;
                            seat.DisconnectedAt = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        session.Seats.RemoveAll(s => s.ConnectionId == callerId);
                        if (session.Seats.Count == 0)
                        {
                            rooms2v2.Remove(roomKey);
                        }
                    }
                }
            }

            await Groups.RemoveFromGroupAsync(callerId, roomKey);

            if (session != null && session.Seats.Count > 0)
            {
                var roomState = new
                {
                    roomName = session.RoomName,
                    bet = session.Bet,
                    seats = session.Seats.OrderBy(s => s.SeatIndex).ToList(),
                    isFull = session.Seats.Count >= 4,
                    gameStarted = session.GameStarted
                };
            }
        }

        public async Task SendVoiceSignal2v2(string roomName, int fromSeat, int toSeat, string signalData)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(roomKey)) return;

            if (toSeat >= 0 && toSeat <= 3)
            {
                string? targetConnId = null;
                lock (rooms2v2Lock)
                {
                    if (rooms2v2.TryGetValue(roomKey, out var session))
                    {
                        var seat = session.Seats.FirstOrDefault(s => s.SeatIndex == toSeat && s.IsConnected);
                        targetConnId = seat?.ConnectionId;
                    }
                }

                if (string.IsNullOrEmpty(targetConnId))
                {
                    lock (rooms1v1Lock)
                    {
                        if (rooms1v1.TryGetValue(roomKey, out var session1))
                        {
                            var seat = session1.Seats.FirstOrDefault(s => s.SeatIndex == toSeat && s.IsConnected);
                            targetConnId = seat?.ConnectionId;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(targetConnId))
                {
                    await Clients.Client(targetConnId).SendAsync("VoiceSignalReceived2v2", fromSeat, toSeat, signalData);
                    return;
                }
            }

            await Clients.OthersInGroup(roomKey).SendAsync("VoiceSignalReceived2v2", fromSeat, toSeat, signalData);
        }

        public async Task BroadcastVoiceState2v2(string roomName, int seatIndex, bool isSpeaking, bool isMuted)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(roomKey)) return;
            await Clients.OthersInGroup(roomKey).SendAsync("VoiceStateUpdated2v2", seatIndex, isSpeaking, isMuted);
        }

        public async Task SendVoiceChunk2v2(string roomName, int fromSeat, int toSeat, string base64Data)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(roomKey) || string.IsNullOrEmpty(base64Data)) return;

            if (toSeat >= 0 && toSeat <= 3)
            {
                string? targetConnId = null;
                lock (rooms2v2Lock)
                {
                    if (rooms2v2.TryGetValue(roomKey, out var session))
                    {
                        var seat = session.Seats.FirstOrDefault(s => s.SeatIndex == toSeat && s.IsConnected);
                        targetConnId = seat?.ConnectionId;
                    }
                }

                if (string.IsNullOrEmpty(targetConnId))
                {
                    lock (rooms1v1Lock)
                    {
                        if (rooms1v1.TryGetValue(roomKey, out var session1))
                        {
                            var seat = session1.Seats.FirstOrDefault(s => s.SeatIndex == toSeat && s.IsConnected);
                            targetConnId = seat?.ConnectionId;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(targetConnId))
                {
                    await Clients.Client(targetConnId).SendAsync("VoiceChunkReceived2v2", fromSeat, toSeat, base64Data);
                    return;
                }
            }

            await Clients.OthersInGroup(roomKey).SendAsync("VoiceChunkReceived2v2", fromSeat, toSeat, base64Data);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            string callerId = Context.ConnectionId;
            lock (queueLock)
            {
                matchmakingQueue.RemoveAll(q => q.ConnectionId == callerId);
            }

            lock (solitaireLock)
            {
                solitaireSessions.Remove(callerId);
            }

            // Notificar desconexión inmediata al oponente en partidas 1 vs 1 activas
            lock (games)
            {
                for (int i = 0; i < games.Count; i++)
                {
                    var g = games[i];
                    if (g.IsActive && (g.IdPOne == callerId || g.IdPTwo == callerId))
                    {
                        string oppId = (g.IdPOne == callerId) ? g.IdPTwo : g.IdPOne;
                        string discName = (g.IdPOne == callerId) ? g.NamePOne : g.NamePTwo;
                        if (!string.IsNullOrEmpty(oppId))
                        {
                            _ = Clients.Client(oppId).SendAsync("OpponentDisconnectedNotice1vs1", new
                            {
                                gameId = g.Id,
                                disconnectedPlayerName = discName,
                                disconnectedConnectionId = callerId
                            });
                        }
                    }
                }
            }

            // Desconexión en salas 1 vs 1
            List<(string key, Room1v1Session session, Seat2v2? disconnectedSeat)> affectedRooms1v1 = new();
            lock (rooms1v1Lock)
            {
                foreach (var kvp in rooms1v1)
                {
                    var seat = kvp.Value.Seats.FirstOrDefault(s => s.ConnectionId == callerId);
                    if (seat != null)
                    {
                        seat.IsConnected = false;
                        seat.DisconnectedAt = DateTime.UtcNow;
                        affectedRooms1v1.Add((kvp.Key, kvp.Value, seat));
                    }
                }
            }

            foreach (var (key, session, disconnectedSeat) in affectedRooms1v1)
            {
                var roomState = new
                {
                    roomName = session.RoomName,
                    bet = session.Bet,
                    seats = session.Seats.OrderBy(s => s.SeatIndex).ToList(),
                    mySeatIndex = -1,
                    isFull = session.Seats.Count >= 2,
                    gameStarted = session.GameStarted
                };
                await Clients.Group(key).SendAsync("RoomUpdate1v1", roomState);
            }

            // Desconexión en salas 2 vs 2
            List<(string key, Room2v2Session session, Seat2v2? disconnectedSeat)> affectedRooms = new();
            lock (rooms2v2Lock)
            {
                foreach (var kvp in rooms2v2)
                {
                    var seat = kvp.Value.Seats.FirstOrDefault(s => s.ConnectionId == callerId);
                    if (seat != null)
                    {
                        // NO remover inmediatamente el asiento, ni en partida ni en lobby!
                        // Los teléfonos móviles desconectan el websocket brevemente al cambiar de app (WhatsApp, compartir enlace) o bloquear pantalla.
                        // Mantener el asiento reservado y marcarlo como desconectado para permitir reconexión transparente.
                        seat.IsConnected = false;
                        seat.DisconnectedAt = DateTime.UtcNow;
                        affectedRooms.Add((kvp.Key, kvp.Value, seat));
                    }
                }
            }

            foreach (var (key, session, disconnectedSeat) in affectedRooms)
            {
                if (disconnectedSeat != null)
                {
                    await Clients.Group(key).SendAsync("PlayerDisconnectedNotice2v2", new
                    {
                        seatIndex = disconnectedSeat.SeatIndex,
                        name = disconnectedSeat.Name,
                        message = $"⚠️ {disconnectedSeat.Name} se ha desconectado. Esperando reconexión..."
                    });
                }

                var roomState = new
                {
                    roomName = session.RoomName,
                    bet = session.Bet,
                    seats = session.Seats.OrderBy(s => s.SeatIndex).ToList(),
                    isFull = session.Seats.Count >= 4,
                    gameStarted = session.GameStarted
                };
                await Clients.Group(key).SendAsync("RoomUpdate2v2", roomState);
            }

            await base.OnDisconnectedAsync(exception);
        }

    }
}
