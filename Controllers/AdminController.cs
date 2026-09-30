using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PericonAPI.Classes;
using PericonAPI.Data;
using PericonAPI.Hubs;
using PericonAPI.Models;

namespace PericonAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AdminAuthorize]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<MessagingHub> _hubContext;

        public AdminController(AppDbContext context, IHubContext<MessagingHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var totalUsers = await _context.Users.CountAsync();
            var pendingRechargesList = await _context.PaymentRecharges
                .Where(r => r.Status == "PENDIENTE")
                .ToListAsync();
            var pendingRecharges = pendingRechargesList.Count;
            var totalBsPending = pendingRechargesList.Sum(r => r.AmountBs);
            var totalCoinsPending = pendingRechargesList.Sum(r => r.CoinsAmount);

            var approvedRecharges = await _context.PaymentRecharges
                .Where(r => r.Status == "APROBADO")
                .ToListAsync();

            var totalBsApproved = approvedRecharges.Sum(r => r.AmountBs);
            var totalCoinsApproved = approvedRecharges.Sum(r => r.CoinsAmount);

            var todayVzla = VenezuelaTime.Now.Date;
            var todayRechargesList = await _context.PaymentRecharges
                .Where(r => r.CreatedAt.Date == todayVzla)
                .ToListAsync();
            var todayRechargesCount = todayRechargesList.Count;
            var todayRechargesBs = todayRechargesList.Sum(r => r.AmountBs);
            var todayApprovedBs = todayRechargesList.Where(r => r.Status == "APROBADO").Sum(r => r.AmountBs);
            var todayPendingBs = todayRechargesList.Where(r => r.Status == "PENDIENTE").Sum(r => r.AmountBs);

            var pendingWithdrawalsList = await _context.PaymentWithdrawals
                .Where(w => w.Status == "PENDIENTE")
                .ToListAsync();
            var pendingWithdrawals = pendingWithdrawalsList.Count;
            var totalBsPendingWithdrawals = pendingWithdrawalsList.Sum(w => w.AmountBs);

            var paidWithdrawals = await _context.PaymentWithdrawals
                .Where(w => w.Status == "PAGADO")
                .ToListAsync();

            var totalBsWithdrawn = paidWithdrawals.Sum(w => w.AmountBs);

            var matchRecords = await _context.MatchBetRecords.ToListAsync();
            var totalHouseCommissions = matchRecords.Sum(m => m.HouseCommission);
            var totalMatchesFinished = matchRecords.Count;
            var totalCoinsWagered = matchRecords.Sum(m => m.TotalPot);

            var botMatches = await _context.BotMatchRecords.ToListAsync();
            var totalBotMatches = botMatches.Count;
            var totalBotCoinsWagered = botMatches.Sum(m => m.BetAmount);
            var totalBotHouseProfit = botMatches.Sum(m => m.HouseProfit);
            var totalBotUserWins = botMatches.Count(m => m.UserWon);
            var totalBotWins = botMatches.Count(m => !m.UserWon);

            var combinedTotalMatches = totalMatchesFinished + totalBotMatches;
            var combinedCoinsWagered = totalCoinsWagered + totalBotCoinsWagered;
            var combinedHouseProfit = totalHouseCommissions + totalBotHouseProfit;

            try
            {
                var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "BotDifficultyMode");
                if (setting != null && !string.IsNullOrWhiteSpace(setting.Value) && setting.Value != GamePlayOneVsOne.BotDifficultyMode)
                {
                    GamePlayOneVsOne.SetBotDifficulty(setting.Value);
                }
            }
            catch { }

            return Ok(new
            {
                totalUsers,
                pendingRecharges,
                totalApprovedCount = approvedRecharges.Count,
                totalBsApproved,
                totalCoinsApproved,
                totalBsPending,
                totalCoinsPending,
                todayRechargesCount,
                todayRechargesBs,
                todayApprovedBs,
                todayPendingBs,
                pendingWithdrawals,
                totalPaidWithdrawalsCount = paidWithdrawals.Count,
                totalBsWithdrawn,
                totalBsPendingWithdrawals,
                totalHouseCommissions,
                totalMatchesFinished,
                totalCoinsWagered,
                totalBotMatches,
                totalBotCoinsWagered,
                totalBotHouseProfit,
                totalBotUserWins,
                totalBotWins,
                combinedTotalMatches,
                combinedCoinsWagered,
                combinedHouseProfit,
                live = Hubs.MessagingHub.GetLiveActivityStatus(),
                botDifficulty = new
                {
                    mode = GamePlayOneVsOne.BotDifficultyMode,
                    housePercent = Math.Round(GamePlayOneVsOne.BotAdvantageProbability * 100),
                    userPercent = Math.Round((1.0 - GamePlayOneVsOne.BotAdvantageProbability) * 100)
                }
            });
        }

        [HttpGet("bot-difficulty")]
        public async Task<IActionResult> GetBotDifficulty()
        {
            try
            {
                var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "BotDifficultyMode");
                if (setting != null && !string.IsNullOrWhiteSpace(setting.Value) && setting.Value != GamePlayOneVsOne.BotDifficultyMode)
                {
                    GamePlayOneVsOne.SetBotDifficulty(setting.Value);
                }
            }
            catch { }

            string mode = GamePlayOneVsOne.BotDifficultyMode ?? "medio";
            double houseAdvantage = GamePlayOneVsOne.BotAdvantageProbability;
            double userAdvantage = Math.Round(1.0 - houseAdvantage, 2);

            string description = mode switch
            {
                "facil" => "Modo Fácil (50% Casa / 50% Jugador - 5 de cada 10 para los jugadores)",
                "dificil" => "Modo Difícil (65% Casa / 35% Jugador - Mayor dificultad)",
                _ => "Modo Medio (60% Casa / 40% Jugador - Balance gradual 60-40)"
            };

            return Ok(new
            {
                mode,
                houseAdvantage,
                userAdvantage,
                housePercent = Math.Round(houseAdvantage * 100),
                userPercent = Math.Round(userAdvantage * 100),
                description
            });
        }

        public class SetBotDifficultyRequest
        {
            public string Mode { get; set; } = "medio";
        }

        [HttpPost("bot-difficulty")]
        public async Task<IActionResult> SetBotDifficulty([FromBody] SetBotDifficultyRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Mode))
            {
                return BadRequest(new { message = "El modo de dificultad es requerido ('facil', 'medio' o 'dificil')." });
            }

            string cleanMode = request.Mode.Trim().ToLowerInvariant();
            if (cleanMode != "facil" && cleanMode != "medio" && cleanMode != "dificil")
            {
                return BadRequest(new { message = "Modo inválido. Debe ser 'facil', 'medio' o 'dificil'." });
            }

            GamePlayOneVsOne.SetBotDifficulty(cleanMode);

            try
            {
                var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "BotDifficultyMode");
                if (setting == null)
                {
                    _context.SystemSettings.Add(new SystemSetting
                    {
                        Key = "BotDifficultyMode",
                        Value = cleanMode,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    setting.Value = cleanMode;
                    setting.UpdatedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminController] Error al persistir BotDifficultyMode en BD: {ex.Message}");
            }

            string description = cleanMode switch
            {
                "facil" => "Modo Fácil activado (50% Casa / 50% Jugador)",
                "dificil" => "Modo Difícil activado (65% Casa / 35% Jugador)",
                _ => "Modo Medio activado (60% Casa / 40% Jugador)"
            };

            return Ok(new
            {
                success = true,
                mode = cleanMode,
                houseAdvantage = GamePlayOneVsOne.BotAdvantageProbability,
                description,
                message = $"Dificultad del Bot actualizada a {description}."
            });
        }

        [HttpGet("bot-stabilization")]
        public async Task<IActionResult> GetBotStabilization()
        {
            var usersList = new List<object>();

            var allStabilizedNames = GamePlayOneVsOne.StabilizedUsers.ToList();
            var allStabilizedIds = GamePlayOneVsOne.StabilizedUserIds.ToList();

            var botMatches = await _context.BotMatchRecords.ToListAsync();

            var targetUsers = await _context.Users
                .Where(u => allStabilizedIds.Contains(u.Id) || allStabilizedNames.Contains(u.Username))
                .ToListAsync();

            if (!targetUsers.Any(u => u.Id == 34 || u.Username.Equals("Memo", StringComparison.OrdinalIgnoreCase)))
            {
                var memoUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == 34 || u.Username == "Memo");
                if (memoUser != null) targetUsers.Add(memoUser);
            }

            foreach (var u in targetUsers)
            {
                var userMatches = botMatches.Where(m => m.UserId == u.Id || m.Username.Equals(u.Username, StringComparison.OrdinalIgnoreCase)).ToList();
                int totalMatches = userMatches.Count;
                int userWins = userMatches.Count(m => m.UserWon);
                int botWins = userMatches.Count(m => !m.UserWon);
                double houseWinRate = totalMatches > 0 ? Math.Round((double)botWins / totalMatches * 100, 1) : 0.0;
                double userWinRate = totalMatches > 0 ? Math.Round((double)userWins / totalMatches * 100, 1) : 0.0;
                int houseNetProfit = userMatches.Sum(m => m.HouseProfit);

                bool isTargetAchieved = (houseWinRate >= GamePlayOneVsOne.StabilizationTargetHouseWinRate * 100);

                usersList.Add(new
                {
                    userId = u.Id,
                    username = u.Username,
                    currentCoins = u.Coins,
                    totalMatches,
                    userWins,
                    botWins,
                    houseWinRatePercent = houseWinRate,
                    userWinRatePercent = userWinRate,
                    houseNetProfit,
                    targetHouseWinRatePercent = Math.Round(GamePlayOneVsOne.StabilizationTargetHouseWinRate * 100),
                    isTargetAchieved,
                    status = isTargetAchieved ? "Estabilizado (60-40 Alcanzado)" : "Estabilización Activa (Bot ganará partidas)"
                });
            }

            return Ok(new
            {
                targetHouseWinRate = GamePlayOneVsOne.StabilizationTargetHouseWinRate,
                stabilizedUsernames = allStabilizedNames,
                stabilizedUserIds = allStabilizedIds,
                users = usersList
            });
        }

        public class ManageStabilizationRequest
        {
            public string? Username { get; set; }
            public int UserId { get; set; } = 0;
        }

        [HttpPost("bot-stabilization")]
        public async Task<IActionResult> AddBotStabilization([FromBody] ManageStabilizationRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) && req.UserId <= 0)
            {
                return BadRequest(new { message = "Debe especificar un nombre de usuario o ID." });
            }

            string uname = req.Username?.Trim() ?? "";
            int uid = req.UserId;

            if (uid <= 0 && !string.IsNullOrEmpty(uname))
            {
                var dbU = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == uname.ToLower());
                if (dbU != null) uid = dbU.Id;
            }
            else if (uid > 0 && string.IsNullOrEmpty(uname))
            {
                var dbU = await _context.Users.FindAsync(uid);
                if (dbU != null) uname = dbU.Username;
            }

            GamePlayOneVsOne.AddStabilizedUser(uname, uid);

            return Ok(new
            {
                success = true,
                message = $"Usuario '{uname}' (ID {uid}) añadido a la lista de estabilización.",
                stabilizedUsers = GamePlayOneVsOne.StabilizedUsers,
                stabilizedUserIds = GamePlayOneVsOne.StabilizedUserIds
            });
        }

        [HttpDelete("bot-stabilization/{identifier}")]
        public IActionResult RemoveBotStabilization(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return BadRequest(new { message = "Identificador requerido." });
            }

            string clean = identifier.Trim();
            if (int.TryParse(clean, out int uid))
            {
                GamePlayOneVsOne.RemoveStabilizedUser("", uid);
            }
            else
            {
                GamePlayOneVsOne.RemoveStabilizedUser(clean, 0);
            }

            return Ok(new
            {
                success = true,
                message = $"Usuario '{clean}' removido de la lista de estabilización.",
                stabilizedUsers = GamePlayOneVsOne.StabilizedUsers,
                stabilizedUserIds = GamePlayOneVsOne.StabilizedUserIds
            });
        }

        [HttpGet("matches")]
        public async Task<IActionResult> GetMatches([FromQuery] string? player, [FromQuery] string? period)
        {
            var query = _context.MatchBetRecords.AsQueryable();

            if (!string.IsNullOrWhiteSpace(player))
            {
                var clean = player.Trim().ToLowerInvariant();
                query = query.Where(m =>
                    m.PlayerOneName.ToLower().Contains(clean) ||
                    m.PlayerTwoName.ToLower().Contains(clean) ||
                    m.WinnerUsername.ToLower().Contains(clean) ||
                    m.LoserUsername.ToLower().Contains(clean));
            }

            var vzlaNow = VenezuelaTime.Now;
            var todayVzla = vzlaNow.Date;
            var todayStartUtc = todayVzla.AddHours(4);

            string activePeriod = (period ?? "todo").Trim().ToLowerInvariant();
            if (activePeriod == "dia" || activePeriod == "today" || activePeriod == "hoy")
            {
                query = query.Where(m => m.CreatedAt >= todayStartUtc);
            }
            else if (activePeriod == "semana" || activePeriod == "week")
            {
                var weekStartUtc = todayStartUtc.AddDays(-7);
                query = query.Where(m => m.CreatedAt >= weekStartUtc);
            }
            else if (activePeriod == "mes" || activePeriod == "month")
            {
                var monthStartUtc = todayStartUtc.AddDays(-30);
                query = query.Where(m => m.CreatedAt >= monthStartUtc);
            }

            var allFiltered = await query.ToListAsync();

            var matches = allFiltered
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new
                {
                    id = m.Id,
                    gameId = m.GameId,
                    playerOneName = m.PlayerOneName,
                    playerTwoName = m.PlayerTwoName,
                    betPerPlayer = m.BetPerPlayer,
                    totalPot = m.TotalPot,
                    houseCommission = m.HouseCommission,
                    winnerPrize = m.WinnerPrize,
                    winnerUsername = m.WinnerUsername,
                    loserUsername = m.LoserUsername,
                    endReason = m.EndReason,
                    createdAt = m.CreatedAt.AddHours(-4).ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToList();

            var dailyActivity = allFiltered
                .GroupBy(m => m.CreatedAt.AddHours(-4).ToString("yyyy-MM-dd"))
                .OrderByDescending(g => g.Key)
                .Select(g => new
                {
                    date = g.Key,
                    count = g.Count(),
                    totalPot = g.Sum(m => m.TotalPot),
                    commission = g.Sum(m => m.HouseCommission),
                    prizes = g.Sum(m => m.WinnerPrize)
                })
                .ToList();

            var summary = new
            {
                totalMatches = allFiltered.Count,
                totalCoinsWagered = allFiltered.Sum(m => m.TotalPot),
                totalHouseCommissions = allFiltered.Sum(m => m.HouseCommission),
                totalPrizesAwarded = allFiltered.Sum(m => m.WinnerPrize),
                averageBet = allFiltered.Count > 0 ? (int)Math.Round(allFiltered.Average(m => m.BetPerPlayer)) : 0
            };

            return Ok(new
            {
                period = activePeriod,
                summary,
                dailyActivity,
                matches
            });
        }

        [HttpGet("bot-matches")]
        public async Task<IActionResult> GetBotMatches([FromQuery] string? search, [FromQuery] string? filter)
        {
            var query = _context.BotMatchRecords.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var cleanSearch = search.Trim().ToLowerInvariant();
                query = query.Where(m => m.Username.ToLower().Contains(cleanSearch) || m.BotName.ToLower().Contains(cleanSearch));
            }

            if (!string.IsNullOrWhiteSpace(filter))
            {
                if (filter.ToLower() == "user_won") query = query.Where(m => m.UserWon);
                else if (filter.ToLower() == "bot_won") query = query.Where(m => !m.UserWon);
            }

            var allList = await _context.BotMatchRecords.ToListAsync();
            var totalBotMatches = allList.Count;
            var userWinsCount = allList.Count(m => m.UserWon);
            var botWinsCount = allList.Count(m => !m.UserWon);
            var userWinRate = totalBotMatches > 0 ? Math.Round((double)userWinsCount / totalBotMatches * 100, 1) : 0;
            var botWinRate = totalBotMatches > 0 ? Math.Round((double)botWinsCount / totalBotMatches * 100, 1) : 0;
            var totalCoinsWagered = allList.Sum(m => m.BetAmount);
            var totalCoinsWonByUser = allList.Sum(m => m.CoinsWon);
            var totalCoinsWonByHouse = allList.Sum(m => m.CoinsLost);
            var netHouseProfit = allList.Sum(m => m.HouseProfit);

            // CÁLCULO DIARIO (Hora de Venezuela UTC-4): Inicia en Cero a la medianoche
            var vzlaNow = VenezuelaTime.Now;
            var todayVzla = vzlaNow.Date;
            var todayStartUtc = todayVzla.AddHours(4);
            var todayEndUtc = todayStartUtc.AddDays(1);

            var todayList = allList.Where(m => m.CreatedAt >= todayStartUtc && m.CreatedAt < todayEndUtc).ToList();
            var todayTotalMatches = todayList.Count;
            var todayUserWins = todayList.Count(m => m.UserWon);
            var todayBotWins = todayList.Count(m => !m.UserWon);
            var todayUserWinRate = todayTotalMatches > 0 ? Math.Round((double)todayUserWins / todayTotalMatches * 100, 1) : 0;
            var todayBotWinRate = todayTotalMatches > 0 ? Math.Round((double)todayBotWins / todayTotalMatches * 100, 1) : 0;
            var todayCoinsWagered = todayList.Sum(m => m.BetAmount);
            var todayCoinsWonByUser = todayList.Sum(m => m.CoinsWon);
            var todayCoinsWonByHouse = todayList.Sum(m => m.CoinsLost);
            var todayNetHouseProfit = todayList.Sum(m => m.HouseProfit);

            var todaySummary = new
            {
                date = todayVzla.ToString("yyyy-MM-dd"),
                totalMatches = todayTotalMatches,
                botWins = todayBotWins,
                userWins = todayUserWins,
                botWinRate = todayBotWinRate,
                userWinRate = todayUserWinRate,
                totalCoinsWagered = todayCoinsWagered,
                coinsWonByUser = todayCoinsWonByUser,
                coinsWonByHouse = todayCoinsWonByHouse,
                netHouseProfit = todayNetHouseProfit,
                targetWinRate = 60.0
            };

            // DESGLOSE HISTÓRICO DÍA POR DÍA (HORA VENEZUELA)
            var dailyBreakdown = allList
                .GroupBy(m => m.CreatedAt.AddHours(-4).ToString("yyyy-MM-dd"))
                .OrderByDescending(g => g.Key)
                .Select(g =>
                {
                    var count = g.Count();
                    var bWins = g.Count(m => !m.UserWon);
                    var uWins = g.Count(m => m.UserWon);
                    var bRate = count > 0 ? Math.Round((double)bWins / count * 100, 1) : 0;
                    var uRate = count > 0 ? Math.Round((double)uWins / count * 100, 1) : 0;
                    return new
                    {
                        date = g.Key,
                        totalMatches = count,
                        botWins = bWins,
                        userWins = uWins,
                        botWinRate = bRate,
                        userWinRate = uRate,
                        totalCoinsWagered = g.Sum(m => m.BetAmount),
                        coinsWonByUser = g.Sum(m => m.CoinsWon),
                        coinsWonByHouse = g.Sum(m => m.CoinsLost),
                        netHouseProfit = g.Sum(m => m.HouseProfit)
                    };
                })
                .ToList();

            var matches = await query
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new
                {
                    id = m.Id,
                    userId = m.UserId,
                    username = m.Username,
                    botName = m.BotName,
                    betAmount = m.BetAmount,
                    userWon = m.UserWon,
                    coinsWon = m.CoinsWon,
                    coinsLost = m.CoinsLost,
                    houseProfit = m.HouseProfit,
                    userCoinsBefore = m.UserCoinsBefore,
                    userCoinsAfter = m.UserCoinsAfter,
                    endReason = m.EndReason,
                    createdAt = m.CreatedAt.AddHours(-4).ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync();

            return Ok(new
            {
                summary = new
                {
                    totalBotMatches,
                    userWinsCount,
                    botWinsCount,
                    userWinRate,
                    botWinRate,
                    totalCoinsWagered,
                    totalCoinsWonByUser,
                    totalCoinsWonByHouse,
                    netHouseProfit
                },
                todaySummary,
                dailyBreakdown,
                matches
            });
        }

        [HttpPost("matches/reset-history")]
        public async Task<IActionResult> ResetMatchHistory()
        {
            var pvpMatches = await _context.MatchBetRecords.ToListAsync();
            var botMatches = await _context.BotMatchRecords.ToListAsync();

            int pvpCount = pvpMatches.Count;
            int botCount = botMatches.Count;

            _context.MatchBetRecords.RemoveRange(pvpMatches);
            _context.BotMatchRecords.RemoveRange(botMatches);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Panel reiniciado a CERO con éxito. Se eliminaron {pvpCount} partidas multijugador y {botCount} partidas contra el Bot. Las estadísticas y ganancias arrancan desde cero a partir de la próxima jugada.",
                pvpDeleted = pvpCount,
                botDeleted = botCount
            });
        }

        [HttpPost("matches/{id}/set-sala-commission")]
        public async Task<IActionResult> SetSalaCommission(int id)
        {
            var match = await _context.MatchBetRecords.FindAsync(id);
            if (match == null) return NotFound(new { success = false, message = "Partida no encontrada." });

            match.HouseCommission = match.TotalPot;
            match.WinnerPrize = 0;
            if (!match.EndReason.StartsWith("[SALA"))
            {
                match.EndReason = $"[SALA 100%] {match.EndReason}";
            }
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = $"Partida #{id} actualizada con comisión del 100% (+🪙 {match.TotalPot} para la casa)." });
        }

        [HttpGet("recharges")]
        public async Task<IActionResult> GetRecharges([FromQuery] string? status)
        {
            var query = _context.PaymentRecharges
                .Include(r => r.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status.ToUpper() != "ALL")
            {
                query = query.Where(r => r.Status == status.ToUpper());
            }

            var list = await query
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    id = r.Id,
                    userId = r.UserId,
                    username = r.User != null ? r.User.Username : "Desconocido",
                    userEmail = r.User != null ? r.User.Email : "",
                    userPhone = r.User != null ? r.User.PhoneNumber : "",
                    userCoins = r.User != null ? r.User.Coins : 0,
                    amountBs = r.AmountBs,
                    coinsAmount = r.CoinsAmount,
                    reference = r.Reference,
                    receiptImageUrl = !string.IsNullOrWhiteSpace(r.ReceiptImageUrl) && !r.ReceiptImageUrl.StartsWith("/uploads/")
                        ? r.ReceiptImageUrl
                        : $"/api/payment/receipt/{r.Id}",
                    status = r.Status,
                    adminNotes = r.AdminNotes,
                    createdAt = r.CreatedAt,
                    processedAt = r.ProcessedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("recharge/{id}/approve")]
        public async Task<IActionResult> ApproveRecharge(int id)
        {
            var recharge = await _context.PaymentRecharges
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recharge == null)
            {
                return NotFound(new { message = "Recarga no encontrada." });
            }

            if (recharge.Status == "APROBADO")
            {
                return BadRequest(new { message = "Esta recarga ya fue aprobada previamente." });
            }

            if (recharge.User == null)
            {
                return BadRequest(new { message = "El usuario asociado a esta recarga no existe." });
            }

            // Acreditar monedas al monedero del usuario
            recharge.User.Coins += recharge.CoinsAmount;
            recharge.Status = "APROBADO";
            recharge.ProcessedAt = VenezuelaTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = recharge.Id,
                status = recharge.Status,
                coinsCredited = recharge.CoinsAmount,
                userNewCoins = recharge.User.Coins,
                username = recharge.User.Username,
                message = $"¡Recarga de {recharge.CoinsAmount} monedas aprobada para el usuario {recharge.User.Username}!"
            });
        }

        [HttpPost("recharge/{id}/reject")]
        public async Task<IActionResult> RejectRecharge(int id, [FromBody] RejectDto? dto)
        {
            var recharge = await _context.PaymentRecharges.FindAsync(id);

            if (recharge == null)
            {
                return NotFound(new { message = "Recarga no encontrada." });
            }

            if (recharge.Status == "APROBADO")
            {
                return BadRequest(new { message = "No se puede rechazar una recarga que ya fue aprobada." });
            }

            recharge.Status = "RECHAZADO";
            recharge.AdminNotes = dto?.Reason ?? "Comprobante inválido o no verificado en banco.";
            recharge.ProcessedAt = VenezuelaTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = recharge.Id,
                status = recharge.Status,
                adminNotes = recharge.AdminNotes,
                message = "La recarga ha sido rechazada."
            });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var userList = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            // Respaldo de teléfonos desde retiros para usuarios antiguos sin PhoneNumber explícito
            var withdrawalPhones = await _context.PaymentWithdrawals
                .Where(w => !string.IsNullOrEmpty(w.PhoneNumber))
                .OrderByDescending(w => w.Id)
                .GroupBy(w => w.UserId)
                .Select(g => new { UserId = g.Key, Phone = g.First().PhoneNumber })
                .ToDictionaryAsync(x => x.UserId, x => x.Phone);

            var users = userList.Select(u =>
            {
                var total = u.Wins + u.Losses;
                var rate = total > 0 ? Math.Round((double)u.Wins / total * 100, 1) : 0;
                string phone = !string.IsNullOrWhiteSpace(u.PhoneNumber)
                    ? u.PhoneNumber
                    : (withdrawalPhones.TryGetValue(u.Id, out var wp) ? wp : "");

                return new
                {
                    id = u.Id,
                    username = u.Username,
                    email = u.Email,
                    phoneNumber = phone,
                    coins = u.Coins,
                    wins = u.Wins,
                    losses = u.Losses,
                    totalMatches = total,
                    winRate = rate,
                    level = u.GetCalculatedLevel(),
                    createdAt = u.CreatedAt,
                    avatarUrl = u.AvatarUrl,
                    isActive = u.IsActive,
                    isAdmin = u.IsAdmin
                };
            }).ToList();

            return Ok(users);
        }

        [HttpPost("user/{id}/adjust-coins")]
        public async Task<IActionResult> AdjustCoins(int id, [FromBody] AdjustCoinsDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            user.Coins = Math.Max(0, user.Coins + dto.Amount);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                coins = user.Coins,
                message = $"Saldo de {user.Username} ajustado con éxito. Saldo actual: {user.Coins} monedas."
            });
        }

        [HttpGet("withdrawals")]
        public async Task<IActionResult> GetWithdrawals([FromQuery] string? status)
        {
            var query = _context.PaymentWithdrawals
                .Include(w => w.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status.ToUpper() != "ALL")
            {
                query = query.Where(w => w.Status == status.ToUpper());
            }

            var list = await query
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new
                {
                    id = w.Id,
                    userId = w.UserId,
                    username = w.User != null ? w.User.Username : "Desconocido",
                    userEmail = w.User != null ? w.User.Email : "",
                    userCurrentCoins = w.User != null ? w.User.Coins : 0,
                    coinsAmount = w.CoinsAmount,
                    amountBs = w.AmountBs,
                    bankName = w.BankName,
                    phoneNumber = w.PhoneNumber,
                    idCard = w.IdCard,
                    status = w.Status,
                    adminReference = w.AdminReference,
                    adminNotes = w.AdminNotes,
                    createdAt = w.CreatedAt,
                    processedAt = w.ProcessedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("withdrawal/{id}/approve")]
        public async Task<IActionResult> ApproveWithdrawal(int id, [FromBody] ApproveWithdrawalDto dto)
        {
            var withdrawal = await _context.PaymentWithdrawals
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (withdrawal == null)
            {
                return NotFound(new { message = "Solicitud de retiro no encontrada." });
            }

            if (withdrawal.Status == "PAGADO")
            {
                return BadRequest(new { message = "Este retiro ya fue procesado como pagado previamente." });
            }

            if (withdrawal.Status == "RECHAZADO")
            {
                return BadRequest(new { message = "No se puede pagar un retiro que ya fue rechazado y reembolsado." });
            }

            withdrawal.Status = "PAGADO";
            withdrawal.AdminReference = dto.Reference?.Trim() ?? "PAGO_MOVIL_OK";
            withdrawal.ProcessedAt = VenezuelaTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = withdrawal.Id,
                status = withdrawal.Status,
                adminReference = withdrawal.AdminReference,
                amountBs = withdrawal.AmountBs,
                username = withdrawal.User?.Username,
                message = $"¡Retiro de {withdrawal.AmountBs} Bs. marcado como PAGADO exitosamente!"
            });
        }

        [HttpPost("withdrawal/{id}/reject")]
        public async Task<IActionResult> RejectWithdrawal(int id, [FromBody] RejectDto? dto)
        {
            var withdrawal = await _context.PaymentWithdrawals
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (withdrawal == null)
            {
                return NotFound(new { message = "Solicitud de retiro no encontrada." });
            }

            if (withdrawal.Status == "PAGADO")
            {
                return BadRequest(new { message = "No se puede rechazar un retiro que ya fue marcado como pagado." });
            }

            if (withdrawal.Status == "RECHAZADO")
            {
                return BadRequest(new { message = "Este retiro ya fue rechazado anteriormente." });
            }

            withdrawal.Status = "RECHAZADO";
            withdrawal.AdminNotes = dto?.Reason ?? "Datos de pago móvil no válidos.";
            withdrawal.ProcessedAt = VenezuelaTime.Now;

            // Reembolsar monedas al usuario
            if (withdrawal.User != null)
            {
                withdrawal.User.Coins += withdrawal.CoinsAmount;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = withdrawal.Id,
                status = withdrawal.Status,
                adminNotes = withdrawal.AdminNotes,
                coinsRefunded = withdrawal.CoinsAmount,
                userNewCoins = withdrawal.User?.Coins,
                message = $"Retiro rechazado. Se han reembolsado {withdrawal.CoinsAmount} monedas a {withdrawal.User?.Username}."
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> AdminLogin([FromBody] AdminLoginDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { message = "Debes ingresar usuario y contraseña." });
            }

            var cleanUsername = dto.Username.Trim();
            if (!cleanUsername.Equals("Guardian", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { message = "Acceso denegado. Este panel es exclusivo para el usuario Guardian." });
            }

            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == "guardian");
            if (admin == null)
            {
                return Unauthorized(new { message = "Usuario administrador no encontrado en el sistema." });
            }

            bool valid = false;
            try
            {
                valid = BCrypt.Net.BCrypt.Verify(dto.Password, admin.PasswordHash);
            }
            catch
            {
                valid = false;
            }

            // Fallback de contingencia si las credenciales coinciden exactamente
            if (!valid && dto.Password == "Guardian.2026")
            {
                valid = true;
                admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Guardian.2026");
                admin.IsAdmin = true;
                admin.IsActive = true;
                await _context.SaveChangesAsync();
            }

            if (!valid)
            {
                return Unauthorized(new { message = "Contraseña de administrador incorrecta." });
            }

            return Ok(new
            {
                success = true,
                id = admin.Id,
                username = admin.Username,
                email = admin.Email,
                coins = admin.Coins,
                token = AdminSecurity.GenerateAdminToken(),
                message = "Bienvenido, Administrador Guardian."
            });
        }

        [HttpPost("user/{id}/toggle-ban")]
        public async Task<IActionResult> ToggleBan(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            if (user.Username.Equals("Guardian", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "No es posible suspender la cuenta del Administrador Guardian." });
            }

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            var estado = user.IsActive ? "habilitado" : "suspendido/baneado";
            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                isActive = user.IsActive,
                message = $"El usuario {user.Username} ha sido {estado} exitosamente."
            });
        }

        [HttpPost("user/{id}/reset-password")]
        public async Task<IActionResult> AdminResetPassword(int id, [FromBody] AdminResetPasswordDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            if (string.IsNullOrWhiteSpace(dto?.NewPassword) || dto.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "La nueva contraseña debe tener al menos 6 caracteres." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Contraseña para '{user.Username}' restablecida con éxito.",
                userId = user.Id,
                username = user.Username,
                newPassword = dto.NewPassword
            });
        }

        [HttpGet("reports")]
        public async Task<IActionResult> GetReports()
        {
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users.CountAsync(u => u.IsActive);
            var bannedUsers = await _context.Users.CountAsync(u => !u.IsActive);

            var recharges = await _context.PaymentRecharges.ToListAsync();
            var totalBsRecharges = recharges.Where(r => r.Status == "APROBADO").Sum(r => r.AmountBs);
            var totalCoinsRecharges = recharges.Where(r => r.Status == "APROBADO").Sum(r => r.CoinsAmount);
            var pendingRechargesCount = recharges.Count(r => r.Status == "PENDIENTE");
            var pendingBsRecharges = recharges.Where(r => r.Status == "PENDIENTE").Sum(r => r.AmountBs);
            var approvedRechargesCount = recharges.Count(r => r.Status == "APROBADO");

            var todayVzla = VenezuelaTime.Now.Date;
            var todayRechargesList = recharges.Where(r => r.CreatedAt.Date == todayVzla).ToList();
            var todayRechargesCount = todayRechargesList.Count;
            var todayRechargesBs = todayRechargesList.Sum(r => r.AmountBs);
            var todayApprovedBs = todayRechargesList.Where(r => r.Status == "APROBADO").Sum(r => r.AmountBs);
            var todayPendingBs = todayRechargesList.Where(r => r.Status == "PENDIENTE").Sum(r => r.AmountBs);

            var withdrawals = await _context.PaymentWithdrawals.ToListAsync();
            var totalBsWithdrawals = withdrawals.Where(w => w.Status == "PAGADO").Sum(w => w.AmountBs);
            var totalCoinsWithdrawals = withdrawals.Where(w => w.Status == "PAGADO").Sum(w => w.CoinsAmount);
            var pendingWithdrawalsCount = withdrawals.Count(w => w.Status == "PENDIENTE");
            var pendingBsWithdrawals = withdrawals.Where(w => w.Status == "PENDIENTE").Sum(w => w.AmountBs);
            var paidWithdrawalsCount = withdrawals.Count(w => w.Status == "PAGADO");

            var matches = await _context.MatchBetRecords.ToListAsync();
            var totalCommissions = matches.Sum(m => m.HouseCommission);
            var totalWagered = matches.Sum(m => m.TotalPot);
            var totalMatches = matches.Count;

            var botMatches = await _context.BotMatchRecords.ToListAsync();
            var totalBotMatches = botMatches.Count;
            var totalBotWagered = botMatches.Sum(m => m.BetAmount);
            var totalBotHouseProfit = botMatches.Sum(m => m.HouseProfit);

            var userCoinsInCirculation = await _context.Users.SumAsync(u => u.Coins);

            return Ok(new
            {
                users = new { total = totalUsers, active = activeUsers, banned = bannedUsers },
                live = Hubs.MessagingHub.GetLiveActivityStatus(),
                financial = new
                {
                    totalBsDeposited = totalBsRecharges,
                    approvedRechargesCount,
                    pendingBsDeposited = pendingBsRecharges,
                    pendingRechargesCount,
                    todayRechargesCount,
                    todayRechargesBs,
                    todayApprovedBs,
                    todayPendingBs,
                    totalBsPaid = totalBsWithdrawals,
                    paidWithdrawalsCount,
                    pendingBsPaid = pendingBsWithdrawals,
                    pendingWithdrawalsCount,
                    netBsBalance = totalBsRecharges - totalBsWithdrawals,
                    netBsBalanceWithPending = (totalBsRecharges + pendingBsRecharges) - (totalBsWithdrawals + pendingBsWithdrawals),
                    totalCoinsCirculating = userCoinsInCirculation,
                    totalCommissionsCollected = totalCommissions,
                    totalMatchesPlayed = totalMatches,
                    totalCoinsWagered = totalWagered,
                    totalBotMatchesPlayed = totalBotMatches,
                    totalBotCoinsWagered = totalBotWagered,
                    totalBotHouseProfit = totalBotHouseProfit,
                    totalCombinedProfit = totalCommissions + totalBotHouseProfit
                }
            });
        }

        [HttpGet("live-activity")]
        public IActionResult GetLiveActivity()
        {
            return Ok(Hubs.MessagingHub.GetLiveActivityStatus());
        }

        [HttpGet("promos")]
        public async Task<IActionResult> GetPromoCodes()
        {
            var promos = await _context.PromoCodes
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    id = p.Id,
                    code = p.Code,
                    coinsReward = p.CoinsReward,
                    maxUses = p.MaxUses,
                    timesUsed = p.TimesUsed,
                    isActive = p.IsActive,
                    createdAt = p.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    expiresAt = p.ExpiresAt.HasValue ? p.ExpiresAt.Value.ToString("yyyy-MM-dd") : null
                })
                .ToListAsync();

            return Ok(promos);
        }

        [HttpPost("promos")]
        public async Task<IActionResult> CreatePromoCode([FromBody] CreatePromoDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code) || dto.CoinsReward <= 0)
            {
                return BadRequest(new { message = "El código y el monto de monedas deben ser válidos." });
            }

            var cleanCode = dto.Code.Trim().ToUpperInvariant();
            if (await _context.PromoCodes.AnyAsync(p => p.Code == cleanCode))
            {
                return BadRequest(new { message = $"El código promocional '{cleanCode}' ya existe." });
            }

            var promo = new PromoCode
            {
                Code = cleanCode,
                CoinsReward = dto.CoinsReward,
                MaxUses = dto.MaxUses > 0 ? dto.MaxUses : 1000,
                TimesUsed = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.PromoCodes.Add(promo);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Código promocional '{promo.Code}' (+{promo.CoinsReward} monedas) creado con éxito.",
                promo
            });
        }

        [HttpPost("promos/{id}/toggle")]
        public async Task<IActionResult> TogglePromoCode(int id)
        {
            var promo = await _context.PromoCodes.FindAsync(id);
            if (promo == null)
            {
                return NotFound(new { message = "Código promocional no encontrado." });
            }

            promo.IsActive = !promo.IsActive;
            await _context.SaveChangesAsync();

            var status = promo.IsActive ? "activado" : "desactivado";
            return Ok(new
            {
                message = $"Código '{promo.Code}' {status} con éxito.",
                isActive = promo.IsActive
            });
        }

        [HttpPost("broadcast-announcement")]
        public async Task<IActionResult> BroadcastAnnouncement([FromBody] BroadcastDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
            {
                return BadRequest(new { message = "El mensaje no puede estar vacío." });
            }

            var title = !string.IsNullOrWhiteSpace(dto.Title) ? dto.Title.Trim() : "📢 COMUNICADO OFICIAL";
            var type = !string.IsNullOrWhiteSpace(dto.Type) ? dto.Type.Trim() : "info";

            // Desactivar anuncios anteriores para que este sea el principal activo
            var activeAnnouncements = await _context.SystemAnnouncements.Where(a => a.IsActive).ToListAsync();
            foreach (var a in activeAnnouncements)
            {
                a.IsActive = false;
            }

            var announcement = new SystemAnnouncement
            {
                Title = title,
                Message = dto.Message.Trim(),
                Type = type,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Guardian"
            };

            _context.SystemAnnouncements.Add(announcement);
            await _context.SaveChangesAsync();

            // Emitir en vivo por SignalR a todos los jugadores conectados en cualquier mesa o lobby
            await _hubContext.Clients.All.SendAsync("GlobalAnnouncement", new
            {
                id = announcement.Id,
                title = announcement.Title,
                message = announcement.Message,
                type = announcement.Type,
                createdAt = announcement.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                timestamp = DateTime.UtcNow.ToString("HH:mm")
            });

            return Ok(new
            {
                message = "Anuncio transmitido en vivo y publicado con éxito para todos los usuarios.",
                announcement = new
                {
                    id = announcement.Id,
                    title = announcement.Title,
                    message = announcement.Message,
                    type = announcement.Type,
                    isActive = announcement.IsActive,
                    createdAt = announcement.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                }
            });
        }

        [HttpGet("announcements")]
        public async Task<IActionResult> GetAnnouncements()
        {
            var list = await _context.SystemAnnouncements
                .OrderByDescending(a => a.CreatedAt)
                .Take(50)
                .Select(a => new
                {
                    id = a.Id,
                    title = a.Title,
                    message = a.Message,
                    type = a.Type,
                    isActive = a.IsActive,
                    createdAt = a.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    createdBy = a.CreatedBy
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("announcements/{id}/toggle")]
        [HttpPost("announcement/{id}/toggle")]
        public async Task<IActionResult> ToggleAnnouncement(int id)
        {
            var item = await _context.SystemAnnouncements.FindAsync(id);
            if (item == null) return NotFound(new { message = "Anuncio no encontrado." });

            bool newState = !item.IsActive;

            if (newState)
            {
                // Si se activa este aviso, desactivar los demás para mantener solo uno activo
                var activeAnnouncements = await _context.SystemAnnouncements
                    .Where(a => a.Id != id && a.IsActive)
                    .ToListAsync();
                foreach (var a in activeAnnouncements)
                {
                    a.IsActive = false;
                }
                item.IsActive = true;
                await _context.SaveChangesAsync();

                // Transmitir en vivo a todos los jugadores conectados en la sala
                await _hubContext.Clients.All.SendAsync("GlobalAnnouncement", new
                {
                    id = item.Id,
                    title = item.Title,
                    message = item.Message,
                    type = item.Type,
                    createdAt = item.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                });

                return Ok(new { message = "Anuncio activado y transmitido con éxito.", isActive = true });
            }
            else
            {
                // Si se pausa / desactiva, retirar el aviso de las pantallas en vivo inmediatamente
                item.IsActive = false;
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("GlobalAnnouncement", null);

                return Ok(new { message = "Anuncio pausado y retirado de las pantallas con éxito.", isActive = false });
            }
        }

        [HttpDelete("announcements/{id}")]
        [HttpDelete("announcement/{id}")]
        public async Task<IActionResult> DeleteAnnouncement(int id)
        {
            var item = await _context.SystemAnnouncements.FindAsync(id);
            if (item == null) return NotFound(new { message = "Anuncio no encontrado." });

            bool wasActive = item.IsActive;
            _context.SystemAnnouncements.Remove(item);
            await _context.SaveChangesAsync();

            if (wasActive)
            {
                // Si el anuncio estaba visible en pantalla, retirarlo en vivo
                await _hubContext.Clients.All.SendAsync("GlobalAnnouncement", null);
            }

            return Ok(new { message = "Anuncio eliminado exitosamente." });
        }

        [HttpGet("announcement/active")]
        [HttpGet("announcements/active")]
        public async Task<IActionResult> GetActiveAnnouncement()
        {
            var item = await _context.SystemAnnouncements
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    id = a.Id,
                    title = a.Title,
                    message = a.Message,
                    type = a.Type,
                    createdAt = a.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                announcement = item,
                id = item?.id,
                title = item?.title,
                message = item?.message,
                type = item?.type,
                createdAt = item?.createdAt
            });
        }

        // ==========================================
        // MÓDULO DE ERRORES E INCIDENCIAS (TELEMETRÍA)
        // ==========================================

        [HttpPost("errors/report")]
        public async Task<IActionResult> ReportError([FromBody] CreateErrorLogDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ErrorMessage))
            {
                return BadRequest(new { message = "El mensaje de error es requerido." });
            }

            var log = new AppErrorLog
            {
                Source = !string.IsNullOrWhiteSpace(dto.Source) ? dto.Source : "Client",
                RoomName = dto.RoomName,
                Username = dto.Username,
                UserId = dto.UserId,
                ErrorMessage = dto.ErrorMessage,
                StackTrace = dto.StackTrace,
                ExtraData = dto.ExtraData,
                Status = "NUEVO",
                CreatedAt = DateTime.UtcNow
            };

            _context.AppErrorLogs.Add(log);
            await _context.SaveChangesAsync();

            Console.WriteLine($"[ErrorLog #{log.Id}] [{log.Source}] Sala: {log.RoomName} | User: {log.Username} | {log.ErrorMessage}");

            return Ok(new
            {
                message = "Error registrado correctamente.",
                errorId = log.Id
            });
        }

        [HttpGet("errors")]
        public async Task<IActionResult> GetErrors([FromQuery] string? status = "ALL", [FromQuery] string? source = null, [FromQuery] int limit = 200)
        {
            var query = _context.AppErrorLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status.ToUpperInvariant() != "ALL")
            {
                var upper = status.ToUpperInvariant();
                query = query.Where(e => e.Status == upper);
            }

            if (!string.IsNullOrWhiteSpace(source) && source.ToUpperInvariant() != "ALL")
            {
                query = query.Where(e => e.Source.ToLower() == source.ToLower());
            }

            var totalErrors = await _context.AppErrorLogs.CountAsync();
            var newErrors = await _context.AppErrorLogs.CountAsync(e => e.Status == "NUEVO");
            var resolvedErrors = await _context.AppErrorLogs.CountAsync(e => e.Status == "RESUELTO");

            var items = await query
                .OrderByDescending(e => e.CreatedAt)
                .Take(Math.Min(limit, 500))
                .Select(e => new
                {
                    id = e.Id,
                    source = e.Source,
                    roomName = e.RoomName,
                    username = e.Username,
                    userId = e.UserId,
                    errorMessage = e.ErrorMessage,
                    stackTrace = e.StackTrace,
                    extraData = e.ExtraData,
                    status = e.Status,
                    adminNotes = e.AdminNotes,
                    createdAt = e.CreatedAt,
                    resolvedAt = e.ResolvedAt
                })
                .ToListAsync();

            return Ok(new
            {
                totalErrors,
                newErrors,
                resolvedErrors,
                items
            });
        }

        [HttpPut("errors/{id}/status")]
        public async Task<IActionResult> UpdateErrorStatus(int id, [FromBody] UpdateErrorStatusDto dto)
        {
            var log = await _context.AppErrorLogs.FindAsync(id);
            if (log == null)
            {
                return NotFound(new { message = "Registro de error no encontrado." });
            }

            log.Status = !string.IsNullOrWhiteSpace(dto.Status) ? dto.Status.ToUpperInvariant() : "REVISADO";
            if (dto.AdminNotes != null)
            {
                log.AdminNotes = dto.AdminNotes;
            }

            if (log.Status == "RESUELTO")
            {
                log.ResolvedAt = DateTime.UtcNow;
            }
            else
            {
                log.ResolvedAt = null;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Estado de incidencia #{id} actualizado a {log.Status}.",
                log
            });
        }

        [HttpDelete("errors/{id}")]
        public async Task<IActionResult> DeleteError(int id)
        {
            var log = await _context.AppErrorLogs.FindAsync(id);
            if (log == null)
            {
                return NotFound(new { message = "Registro de error no encontrado." });
            }

            _context.AppErrorLogs.Remove(log);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Incidencia #{id} eliminada correctamente." });
        }

        [HttpPost("errors/clear-resolved")]
        public async Task<IActionResult> ClearResolvedErrors()
        {
            var resolved = await _context.AppErrorLogs.Where(e => e.Status == "RESUELTO").ToListAsync();
            _context.AppErrorLogs.RemoveRange(resolved);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Se eliminaron {resolved.Count} incidencias resueltas." });
        }

        [HttpPost("reset-season-stats")]
        public async Task<IActionResult> ResetSeasonStats([FromBody] ResetSeasonDto? dto)
        {
            var targetCoins = dto != null && dto.Coins > 0 ? dto.Coins : 200;
            var users = await _context.Users.ToListAsync();
            int resetCount = 0;

            foreach (var u in users)
            {
                u.Coins = targetCoins;
                u.BonusCoins = targetCoins;
                u.Wins = 0;
                u.Losses = 0;
                u.Experience = 0;
                u.Level = "Peón de Casona";
                resetCount++;
            }

            if (dto?.ClearMatchHistory == true)
            {
                _context.MatchBetRecords.RemoveRange(_context.MatchBetRecords);
                _context.BotMatchRecords.RemoveRange(_context.BotMatchRecords);
            }

            // Desactivar anuncios anteriores
            var activeAnnouncements = await _context.SystemAnnouncements.Where(a => a.IsActive).ToListAsync();
            foreach (var a in activeAnnouncements)
            {
                a.IsActive = false;
            }

            var announcement = new SystemAnnouncement
            {
                Title = "🚨 ¡COMIENZA LA ERA DE DINERO REAL! • 300 MONEDAS DE CORTESÍA Y RANKING EN CERO 🚨",
                Message = "¡Atención a todos los jugadores de El Pericón! A partir de hoy iniciamos oficialmente las partidas con DINERO REAL. Con motivo del lanzamiento, todos los jugadores han recibido 300 MONEDAS DE CORTESÍA para disputar 3 partidas de prueba y el ranking de victorias se ha reiniciado a cero para una competencia 100% limpia y justa. ¡Recarga desde 800 Bs. por Pago Móvil, compite en mesas 1v1 y 2v2 y retira tus ganancias directo a tu cuenta bancaria! Entra a www.pericon.lat",
                Type = "alerta",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Guardian"
            };
            _context.SystemAnnouncements.Add(announcement);

            await _context.SaveChangesAsync();

            // Emitir en vivo por SignalR a todos los jugadores conectados
            await _hubContext.Clients.All.SendAsync("GlobalAnnouncement", new
            {
                id = announcement.Id,
                title = announcement.Title,
                message = announcement.Message,
                type = announcement.Type,
                createdAt = announcement.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                timestamp = DateTime.UtcNow.ToString("HH:mm")
            });

            return Ok(new
            {
                success = true,
                usersReset = resetCount,
                coinsSet = targetCoins,
                message = $"Se reiniciaron con éxito {resetCount} usuarios a {targetCoins} monedas de cortesía, 0 victorias, ranking en cero y comunicado activo publicado.",
                announcement = new
                {
                    id = announcement.Id,
                    title = announcement.Title,
                    message = announcement.Message,
                    type = announcement.Type
                }
            });
        }

        [HttpGet("feedbacks")]
        public async Task<IActionResult> GetFeedbacks([FromQuery] string? category, [FromQuery] int? rating)
        {
            var query = _context.PlayerFeedbacks.AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) && category != "ALL")
            {
                query = query.Where(f => f.Category.ToLower() == category.ToLower());
            }

            if (rating.HasValue && rating.Value > 0)
            {
                query = query.Where(f => f.Rating == rating.Value);
            }

            var feedbacks = await query
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new
                {
                    id = f.Id,
                    userId = f.UserId,
                    username = f.Username,
                    userEmail = f.UserEmail,
                    userPhone = f.UserPhone,
                    rating = f.Rating,
                    category = f.Category,
                    message = f.Message,
                    canPublish = f.CanPublish,
                    isFeatured = f.IsFeatured,
                    createdAt = f.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync();

            var total = await _context.PlayerFeedbacks.CountAsync();
            var avg = total > 0 ? await _context.PlayerFeedbacks.AverageAsync(f => f.Rating) : 5.0;

            return Ok(new
            {
                success = true,
                total,
                averageRating = Math.Round(avg, 1),
                feedbacks
            });
        }

        [HttpPost("feedback/{id}/toggle-featured")]
        public async Task<IActionResult> ToggleFeaturedFeedback(int id)
        {
            var feedback = await _context.PlayerFeedbacks.FindAsync(id);
            if (feedback == null) return NotFound(new { success = false, message = "Opinión no encontrada." });

            feedback.IsFeatured = !feedback.IsFeatured;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, isFeatured = feedback.IsFeatured, message = feedback.IsFeatured ? "Opinión destacada como testimonio." : "Opinión removida de destacados." });
        }

        [HttpDelete("feedback/{id}")]
        public async Task<IActionResult> DeleteFeedback(int id)
        {
            var feedback = await _context.PlayerFeedbacks.FindAsync(id);
            if (feedback == null) return NotFound(new { success = false, message = "Opinión no encontrada." });

            _context.PlayerFeedbacks.Remove(feedback);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Opinión eliminada correctamente." });
        }
    }

    public class ResetSeasonDto
    {
        public int Coins { get; set; } = 300;
        public bool ClearMatchHistory { get; set; } = false;
    }

    public class CreatePromoDto
    {
        public string Code { get; set; } = string.Empty;
        public int CoinsReward { get; set; } = 200;
        public int MaxUses { get; set; } = 1000;
    }

    public class BroadcastDto
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "info";
    }

    public class AdminLoginDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RejectDto
    {
        public string? Reason { get; set; }
    }

    public class AdjustCoinsDto
    {
        public int Amount { get; set; }
        public string? Reason { get; set; }
    }

    public class ApproveWithdrawalDto
    {
        public string Reference { get; set; } = string.Empty;
    }
}
