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
            this.Id = Id; this.Name = Name; this.Email = Email; this.Coins = 100; Active = true;
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

        public MessagingHub(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        private static List<GamePlayOneVsOne> games = new List<GamePlayOneVsOne>(); 
        private static List<GamePlayTwoVsTwo> games2vs2 = new List<GamePlayTwoVsTwo>(); 

        private GamePlayOneVsOne example = new GamePlayOneVsOne(1);

        private static List<GamePlayer> users = new List<GamePlayer>();

        public class MatchQueueItem
        {
            public string ConnectionId { get; set; } = string.Empty;
            public string Mode { get; set; } = "1 vs 1";
            public int Bet { get; set; } = 10;
            public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
        }

        public class Seat2v2
        {
            public int SeatIndex { get; set; } // 0 = P1 (Azul), 1 = P2 (Rojo), 2 = P3 (Azul), 3 = P4 (Rojo)
            public string ConnectionId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Team { get; set; } // 1 or 2
            public string Role { get; set; } = string.Empty;
            public bool IsReady { get; set; } = true;
        }

        public class Room2v2Session
        {
            public string RoomName { get; set; } = string.Empty;
            public int Bet { get; set; } = 100;
            public List<Seat2v2> Seats { get; set; } = new List<Seat2v2>();
            public bool GameStarted { get; set; } = false;
            public string CurrentInitHand { get; set; } = string.Empty;
            public int PointsTeam1 { get; set; } = 0;
            public int PointsTeam2 { get; set; } = 0;
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        }

        private static Dictionary<string, Room2v2Session> rooms2v2 = new Dictionary<string, Room2v2Session>();
        private static readonly object rooms2v2Lock = new object();

        private static Dictionary<string, GamePlayOneVsOne> solitaireSessions = new Dictionary<string, GamePlayOneVsOne>();
        private static readonly object solitaireLock = new object();

        private static List<MatchQueueItem> matchmakingQueue = new List<MatchQueueItem>();
        private static readonly object queueLock = new object();

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
            foreach (var user in users)
            {
                if (user.Id.Equals(Context.ConnectionId))
                {
                    if (!string.IsNullOrWhiteSpace(playerName)) user.Name = playerName;
                    if (!string.IsNullOrWhiteSpace(email)) user.Email = email;
                    if (coins > 0) user.Coins = coins;
                    await Clients.Client(user.Id).SendAsync("GetPlayer", user);
                    return;
                }
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

            string[] daticos = move.content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (daticos.Length < 2) return;

            // En Tumba no está permitido pedir
            if (games[numg].IsTumbaOne || games[numg].IsTumbaTwo || games[numg].PointsOne >= 9 || games[numg].PointsTwo >= 9 ||
                (games[numg].IsTumbaDeParaAtrasOne && games[numg].PointsOne == 8) || (games[numg].IsTumbaDeParaAtrasTwo && games[numg].PointsTwo == 8))
            {
                Console.WriteLine("[Ask369Game] Pedir bloqueado porque un jugador está en Tumba.");
                return;
            }

            string sentto = daticos[1];
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
            await Clients.Client(sentto).SendAsync("Asked369Game", data);
        }

        public async Task Answer369Game(GameMessage move)
        {
            Console.WriteLine($"Answer369Game. Cliente: {Context.ConnectionId}, Orden: {move}");
            GamePlayer dataplay = SearchPlayer(Context.ConnectionId);
            int numg = FindGame1vs1(move.game);
            if (numg < 0 || numg >= games.Count) return;

            string[] daticos = move.content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (daticos.Length < 3) return;
            string ownto = daticos[0];
            string sentto = daticos[1];
            int chosen = int.Parse(daticos[daticos.Length - 1]);
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
                    data.content = $"2 {games[numg].PointsOne} {games[numg].PointsTwo}";
                    await Clients.Client(ownto).SendAsync("EndAsk369Round", data);
                    await Clients.Client(sentto).SendAsync("Answered369Game", data);
                    break;
                case 3: // Rechaza 3 -> Quien pidió 3 (sentto) gana 1 punto
                    games[numg].Ask369 = -1;
                    games[numg].RoundOne = 0;
                    games[numg].RoundTwo = 0;
                    if (sentto == games[numg].IdPOne) games[numg].PointsOne += 1;
                    else games[numg].PointsTwo += 1;
                    games[numg].UpdateTumbaStatus(oldP1, oldP2);
                    data.content = $"3 {games[numg].PointsOne} {games[numg].PointsTwo}";
                    await Clients.Client(ownto).SendAsync("EndAsk369Round", data);
                    await Clients.Client(sentto).SendAsync("Answered369Game", data);
                    break;
                case 4: // Revira a 6 (propone 6 a sentto)
                    data.order = 76;
                    data.content = "4";
                    data.game = move.game;
                    await Clients.Client(sentto).SendAsync("Asked369Game", data);
                    break;
                case 5: // Acepta 6
                    games[numg].CurrentStake = 6;
                    games[numg].Ask369 = 6;
                    data.content = $"5 {games[numg].PointsOne} {games[numg].PointsTwo}";
                    await Clients.Client(ownto).SendAsync("EndAsk369Round", data);
                    await Clients.Client(sentto).SendAsync("Answered369Game", data);
                    break;
                case 6: // Rechaza 6 -> Quien propuso 6 (sentto) gana las 3 piedras ya pactadas
                    games[numg].Ask369 = -1;
                    games[numg].RoundOne = 0;
                    games[numg].RoundTwo = 0;
                    if (sentto == games[numg].IdPOne) games[numg].PointsOne += 3;
                    else games[numg].PointsTwo += 3;
                    games[numg].UpdateTumbaStatus(oldP1, oldP2);
                    data.content = $"6 {games[numg].PointsOne} {games[numg].PointsTwo}";
                    await Clients.Client(ownto).SendAsync("EndAsk369Round", data);
                    await Clients.Client(sentto).SendAsync("Answered369Game", data);
                    break;
                case 7: // Revira a 9 (propone 9 a sentto)
                    data.order = 76;
                    data.content = "7";
                    data.game = move.game;
                    await Clients.Client(sentto).SendAsync("Asked369Game", data);
                    break;
                case 8: // Acepta 9
                    games[numg].CurrentStake = 9;
                    games[numg].Ask369 = 9;
                    data.content = $"8 {games[numg].PointsOne} {games[numg].PointsTwo}";
                    await Clients.Client(ownto).SendAsync("EndAsk369Round", data);
                    await Clients.Client(sentto).SendAsync("Answered369Game", data);
                    break;
                case 9: // Rechaza 9 -> Quien propuso 9 (sentto) gana las 6 piedras ya pactadas
                    games[numg].Ask369 = -1;
                    games[numg].RoundOne = 0;
                    games[numg].RoundTwo = 0;
                    if (sentto == games[numg].IdPOne) games[numg].PointsOne += 6;
                    else games[numg].PointsTwo += 6;
                    games[numg].UpdateTumbaStatus(oldP1, oldP2);
                    data.content = $"9 {games[numg].PointsOne} {games[numg].PointsTwo}";
                    await Clients.Client(ownto).SendAsync("EndAsk369Round", data);
                    await Clients.Client(sentto).SendAsync("Answered369Game", data);
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
            newgame.NamePOne = QOne.Name;
            newgame.NamePTwo = QTwo.Name;

            // Sorteo de mano inicial 50% / 50%
            Random rng = new Random();
            bool p1Starts = rng.Next(2) == 0;
            newgame.PlayerTurn = p1Starts;

            newgame.Deck.RandomCards();
            newgame.Id = newgame.GenerateSeed(games);
            newgame.ShuffleCards_1vs1();
            games.Add(newgame);
            GameMessage sentence = new GameMessage();
            sentence.game = newgame.Id;
            sentence.order = 99;
            string previewcontent = POne + " " + QOne.Name + " " + PTwo + " " + QTwo.Name + " ";
            sentence.content = previewcontent + "1";
            Console.WriteLine($"Juego creado: {newgame.Id}, Mano inicial: {newgame.InitHand}, Inicia POne: {p1Starts}");
            await Clients.Client(POne).SendAsync("ReadyToGame1vs1",sentence);
            sentence.content = previewcontent + "0";
            await Clients.Client(PTwo).SendAsync("ReadyToGame1vs1",sentence);
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
            games[numg].Deck.RandomCards();
            games[numg].ShuffleCards_1vs1();
            games[numg].PlayerTurn = !games[numg].PlayerTurn;
            games[numg].CurrentStake = 1;
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
            string PFour = (games[numg].PlayerTurn == true ? "1" : "0");
            string PFive = (games[numg].PlayerTurn == true ? "0" : "1");
            string PScore = $"-{games[numg].PointsOne}-{games[numg].PointsTwo}";
            sentence.content = previewcontent + PFour + PScore;
            Console.WriteLine($"Enviando: {sentence}");
            await Clients.Client(POne).SendAsync("setChangeHand", sentence);
            sentence.content = previewcontent + PFive + PScore;
            await Clients.Client(PTwo).SendAsync("setChangeHand", sentence);
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
            games[numg].PlayerTurn = !games[numg].PlayerTurn;
            games[numg].CurrentStake = 1;
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
            string PFour = (games[numg].PlayerTurn == true ? "1" : "0");
            string PFive = (games[numg].PlayerTurn == true ? "0" : "1");
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
            string rivalId = callerIsP1 ? games[numg].IdPTwo : games[numg].IdPOne;

            await Clients.Client(rivalId).SendAsync("TumbaAcceptedNotice", new
            {
                message = "¡El rival aceptó jugar la ronda en Tumba!"
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
            Console.WriteLine($"GetInitHandGame1vs1. Cliente: {Context.ConnectionId}, Juego: {id}");
            string PZero = FindInitHand(id);
            int numg = FindGame1vs1(id);
            int p1 = (numg >= 0 && numg < games.Count) ? games[numg].PointsOne : 0;
            int p2 = (numg >= 0 && numg < games.Count) ? games[numg].PointsTwo : 0;
            bool isMyTurn = (numg >= 0 && numg < games.Count)
                ? (flag == games[numg].PlayerTurn)
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
            string[] daticos = move.content.Split(" ");
            string sentto = "";
            GameMessage sentence = new GameMessage();
            if (move.order == 82) // Juego del que lleva la mano
            {
                sentto = daticos[1];
                sentence.game = move.game;
                sentence.order = 84;
                sentence.content = move.content;
                Console.WriteLine("Enviando: {0}", "84 " + sentence.content);
                await Clients.Client(sentto).SendAsync("ResponseCard1vs1", sentence);
            }
            else if (move.order == 83) // Juego del que responde
            {
                sentto = daticos[0];
                int numg = FindGame1vs1(move.game);
                if (numg < 0 || numg >= games.Count) return;

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

                bool leadIsPlayerOne = (sentto == games[numg].IdPOne);

                // Verificación de "La Cogía": Si se juega el 10 de Oro (7) y el rival responde con el 1 de Oro (0)
                // No aplica si se está en Tumba
                bool isTumbaMulti = games[numg].IsTumbaOne || games[numg].IsTumbaTwo ||
                                   games[numg].PointsOne >= 9 || games[numg].PointsTwo >= 9 ||
                                   (games[numg].IsTumbaDeParaAtrasOne && games[numg].PointsOne == 8) ||
                                   (games[numg].IsTumbaDeParaAtrasTwo && games[numg].PointsTwo == 8);

                bool isCogida = false;
                int cogidaWinner = 0; // 1 = playerOne, 2 = playerTwo
                if (!isTumbaMulti && leadCard == 7 && respCard == 0)
                {
                    isCogida = true;
                    cogidaWinner = leadIsPlayerOne ? 2 : 1; // El que responde coge al que salió
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
                Console.WriteLine("Enviando: {0}", "85 " + sentence.content);
                await Clients.Client(sentto).SendAsync("ResponseCard1vs1", sentence);
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

            int bet = game.Coins > 0 ? game.Coins : 10;
            int totalPot = bet * 2;
            int houseCommission = (int)Math.Round(totalPot * 0.20); // 20% retenido por la plataforma
            int winnerPrize = totalPot - houseCommission;           // 80% que se lleva el ganador

            int winnerNewCoins = 0;
            int loserNewCoins = 0;

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

                    var dbWinner = db.Users.FirstOrDefault(u => 
                        u.Username.ToLower() == winnerName.ToLower() || 
                        (!string.IsNullOrEmpty(winnerEmail) && u.Email.ToLower() == winnerEmail.ToLower()));

                    var dbLoser = db.Users.FirstOrDefault(u => 
                        u.Username.ToLower() == loserName.ToLower() || 
                        (!string.IsNullOrEmpty(loserEmail) && u.Email.ToLower() == loserEmail.ToLower()));

                    if (dbLoser != null)
                    {
                        int loserDeduction = Math.Min(dbLoser.Coins, bet);
                        dbLoser.Coins -= loserDeduction;
                        dbLoser.Losses += 1;
                        loserNewCoins = dbLoser.Coins;
                    }

                    if (dbWinner != null)
                    {
                        int netWinnerGain = Math.Max(0, winnerPrize - bet);
                        dbWinner.Coins += netWinnerGain;
                        dbWinner.Wins += 1;
                        winnerNewCoins = dbWinner.Coins;
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
                            EndReason = reason,
                            CreatedAt = DateTime.UtcNow
                        };
                        db.MatchBetRecords.Add(betRecord);
                        await db.SaveChangesAsync();

                        GameLogger.Log(game.Id, "ProcessMatchPayout", $"Ganador={dbWinner?.Username ?? winnerName} (Saldo={winnerNewCoins}), Perdedor={dbLoser?.Username ?? loserName} (Saldo={loserNewCoins}), Premio={winnerPrize}, Casa={houseCommission}, Razon={reason}");
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
                await Clients.Client(winnerConnectionId).SendAsync("MatchFinishedPayout", new
                {
                    isWinner = true,
                    bet = bet,
                    totalPot = totalPot,
                    houseCommission = houseCommission,
                    winnerPrize = winnerPrize,
                    netGain = winnerPrize - bet,
                    newBalance = winnerNewCoins,
                    message = $"🏆 ¡Ganaste la partida! Te llevas {winnerPrize} monedas (80% del pozo de {totalPot}). Comisión de sala (20%): {houseCommission} monedas."
                });

                await Clients.Client(loserConnectionId).SendAsync("MatchFinishedPayout", new
                {
                    isWinner = false,
                    bet = bet,
                    totalPot = totalPot,
                    houseCommission = houseCommission,
                    winnerPrize = winnerPrize,
                    netGain = -bet,
                    newBalance = loserNewCoins,
                    message = $"Partida finalizada. Se descontaron {bet} monedas de tu monedero."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessMatchPayout Send Error] {ex.Message}");
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

        public async Task JoinMatchmaking(string mode, int bet)
        {
            string callerId = Context.ConnectionId;
            Console.WriteLine($"JoinMatchmaking recibido. Cliente: {callerId}, Modo: {mode}, Apuesta: {bet}");

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

                    GamePlayer q1 = GetPlayerData(p1);
                    GamePlayer q2 = GetPlayerData(p2);
                    GamePlayer q3 = GetPlayerData(p3);
                    GamePlayer q4 = GetPlayerData(p4);

                    GamePlayTwoVsTwo newGame2v2 = new GamePlayTwoVsTwo(p1, p2, p3, p4);
                    newGame2v2.Coins = bet;
                    newGame2v2.Name1 = q1.Name;
                    newGame2v2.Name2 = q2.Name;
                    newGame2v2.Name3 = q3.Name;
                    newGame2v2.Name4 = q4.Name;

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
                    matchSession.Seats.Add(new Seat2v2 { SeatIndex = 0, ConnectionId = p1, Name = q1.Name, Team = 1, Role = "Anfitrión" });
                    matchSession.Seats.Add(new Seat2v2 { SeatIndex = 1, ConnectionId = p2, Name = q2.Name, Team = 2, Role = "Rival 1" });
                    matchSession.Seats.Add(new Seat2v2 { SeatIndex = 2, ConnectionId = p3, Name = q3.Name, Team = 1, Role = "Compañero" });
                    matchSession.Seats.Add(new Seat2v2 { SeatIndex = 3, ConnectionId = p4, Name = q4.Name, Team = 2, Role = "Rival 2" });

                    lock (rooms2v2Lock)
                    {
                        rooms2v2[matchRoomKey] = matchSession;
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        await Groups.AddToGroupAsync(shuffled[i].ConnectionId, matchRoomKey);
                    }

                    Console.WriteLine($"[Matchmaking 2vs2] 4 Jugadores emparejados: {q1.Name} & {q3.Name} vs {q2.Name} & {q4.Name}. Sala: {matchRoomKey}");

                    for (int i = 0; i < 4; i++)
                    {
                        string targetConn = shuffled[i].ConnectionId;
                        GameMessage msg = new GameMessage
                        {
                            game = newGame2v2.Id,
                            order = 220, // MatchFound2v2
                            content = $"{newGame2v2.Id} {bet} {p1} {q1.Name} {p2} {q2.Name} {p3} {q3.Name} {p4} {q4.Name} {i} {newGame2v2.InitHand} {matchRoomKey}"
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
                newGame.NamePOne = q1.Name;
                newGame.NamePTwo = q2.Name;

                // Sorteo de mano inicial 50% / 50%
                Random rng = new Random();
                newGame.PlayerTurn = (rng.Next(2) == 0);

                newGame.Deck.RandomCards();
                newGame.Id = newGame.GenerateSeed(games);
                newGame.ShuffleCards_1vs1();
                games.Add(newGame);

                Console.WriteLine($"[Matchmaking] Emparejados {p1} vs {p2}. Juego: {newGame.Id}");

                GameMessage msgP1 = new GameMessage
                {
                    game = newGame.Id,
                    order = 99,
                    content = $"{p1} {q1.Name} {p2} {q2.Name} 1"
                };

                GameMessage msgP2 = new GameMessage
                {
                    game = newGame.Id,
                    order = 99,
                    content = $"{p1} {q1.Name} {p2} {q2.Name} 0"
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
        // SALAS MULTIJUGADOR 2 VS 2 (USUARIO VS USUARIO)
        // ==========================================

        public async Task JoinRoom2v2(string roomName, string playerName, int bet)
        {
            string callerId = Context.ConnectionId;
            string roomKey = (roomName ?? "sala-pericon").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(playerName) || playerName == "nulo")
            {
                playerName = $"Jugador-{callerId.Substring(0, Math.Min(4, callerId.Length))}";
            }

            Room2v2Session session;
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

                // Verificar si ya está en algún asiento por ConnectionId o por nombre si reconecta
                var existingSeat = session.Seats.FirstOrDefault(s => s.ConnectionId == callerId || s.Name == playerName);
                if (existingSeat == null)
                {
                    var takenIndices = session.Seats.Select(s => s.SeatIndex).ToHashSet();
                    int freeSeat = -1;
                    for (int i = 0; i < 4; i++)
                    {
                        if (!takenIndices.Contains(i))
                        {
                            freeSeat = i;
                            break;
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

                        session.Seats.Add(new Seat2v2
                        {
                            SeatIndex = freeSeat,
                            ConnectionId = callerId,
                            Name = playerName,
                            Team = team,
                            Role = role,
                            IsReady = true
                        });
                    }
                }
                else
                {
                    existingSeat.ConnectionId = callerId;
                    existingSeat.Name = playerName;
                }
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

            // Si la partida ya fue iniciada (ej. por Matchmaking automático), enviar GameStarted2v2 al cliente conectado
            if (session.GameStarted && !string.IsNullOrEmpty(session.CurrentInitHand))
            {
                var startedPayload = new
                {
                    roomName = session.RoomName,
                    initHand = session.CurrentInitHand,
                    starterPlayer = 0,
                    bet = session.Bet,
                    seats = session.Seats.OrderBy(s => s.SeatIndex).ToList()
                };
                await Clients.Caller.SendAsync("GameStarted2v2", startedPayload);
            }
            // Si se completaron los 4 jugadores y la partida aún no ha iniciado, iniciar automáticamente
            else if (session.Seats.Count >= 4 && !session.GameStarted)
            {
                await StartGame2v2Internal(roomKey);
            }
        }

        public async Task StartGame2v2(string roomName)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            await StartGame2v2Internal(roomKey);
        }

        private async Task StartGame2v2Internal(string roomKey)
        {
            Room2v2Session? session;
            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session)) return;
                session.GameStarted = true;
                session.PointsTeam1 = 0;
                session.PointsTeam2 = 0;
            }

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

            string initHand = string.Join("-", parts);
            session.CurrentInitHand = initHand;

            var payload = new
            {
                roomName = session.RoomName,
                initHand = initHand,
                starterPlayer = 0, // Inicia Seat 0 (Anfitrión)
                bet = session.Bet,
                seats = session.Seats.OrderBy(s => s.SeatIndex).ToList()
            };

            await Clients.Group(roomKey).SendAsync("GameStarted2v2", payload);
        }

        public async Task DealNewHand2v2(string roomName, int starterPlayer)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            Room2v2Session? session;
            lock (rooms2v2Lock)
            {
                if (!rooms2v2.TryGetValue(roomKey, out session)) return;
            }

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

            string initHand = string.Join("-", parts);
            session.CurrentInitHand = initHand;

            var payload = new
            {
                roomName = session.RoomName,
                initHand = initHand,
                starterPlayer = starterPlayer
            };

            await Clients.Group(roomKey).SendAsync("NewHandDealt2v2", payload);
        }

        public async Task PlayCard2v2(string roomName, int seatIndex, int cardId)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            await Clients.Group(roomKey).SendAsync("CardPlayed2v2", new
            {
                seatIndex,
                cardId
            });
        }

        public async Task PedirStake2v2(string roomName, int seatIndex, int nextStake)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            await Clients.Group(roomKey).SendAsync("StakeAsked2v2", new
            {
                seatIndex,
                nextStake
            });
        }

        public async Task AnswerStake2v2(string roomName, int seatIndex, bool accepted)
        {
            string roomKey = (roomName ?? "").Trim().ToLowerInvariant();
            await Clients.Group(roomKey).SendAsync("StakeAnswered2v2", new
            {
                seatIndex,
                accepted
            });
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
                    session.Seats.RemoveAll(s => s.ConnectionId == callerId);
                    if (session.Seats.Count == 0)
                    {
                        rooms2v2.Remove(roomKey);
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
                await Clients.Group(roomKey).SendAsync("RoomUpdate2v2", roomState);
            }
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

            // Desconexión en salas 2 vs 2
            List<(string key, Room2v2Session session)> affectedRooms = new List<(string, Room2v2Session)>();
            lock (rooms2v2Lock)
            {
                foreach (var kvp in rooms2v2)
                {
                    if (kvp.Value.Seats.Any(s => s.ConnectionId == callerId))
                    {
                        kvp.Value.Seats.RemoveAll(s => s.ConnectionId == callerId);
                        affectedRooms.Add((kvp.Key, kvp.Value));
                    }
                }
            }

            foreach (var (key, session) in affectedRooms)
            {
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
