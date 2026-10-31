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
            var pvpRecords = matchRecords.Where(m => !m.IsBotMatch).ToList();
            var totalHouseCommissions = matchRecords.Sum(m => m.HouseCommission);
            var totalMatchesFinished = pvpRecords.Count;
            var totalCoinsWagered = pvpRecords.Sum(m => m.TotalPot);

            var botMatches = await _context.BotMatchRecords.ToListAsync();
            var totalBotMatches = botMatches.Count;
            var totalBotCoinsWagered = botMatches.Sum(m => m.BetAmount);
            var totalBotHouseProfit = botMatches.Sum(m => m.HouseProfit);
            var totalBotHouseCommissions = botMatches.Sum(m => m.HouseCommission);
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
                totalBotHouseCommissions,
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

            string mode = GamePlayOneVsOne.BotDifficultyMode ?? "facil";
            double houseAdvantage = GamePlayOneVsOne.BotAdvantageProbability;
            double userAdvantage = Math.Round(1.0 - houseAdvantage, 2);

            string description = mode switch
            {
                "facil" => "Modo Fácil (40% Casa / 60% Jugador - 6 de cada 10 a favor de los jugadores)",
                "dificil" => "Modo Difícil (65% Casa / 35% Jugador - Mayor dificultad)",
                _ => "Modo Medio (60% Casa / 40% Jugador - Retención Progresiva)"
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
            public string Mode { get; set; } = "facil";
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

                // Sincronizar también la columna BotDifficulty de todos los bots virtuales en la tabla Users
                await _context.Users
                    .Where(u => u.IsVirtualBot)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.BotDifficulty, cleanMode));

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminController] Error al persistir BotDifficultyMode en BD: {ex.Message}");
            }

            string description = cleanMode switch
            {
                "facil" => "Modo Fácil activado (50% Casa / 50% Jugador - Modo Recreativo y Atracción)",
                "dificil" => "Modo Difícil activado (85% Casa / 15% Jugador - Muro de Contención y Bloqueo Total)",
                _ => "Modo Medio activado (60% Casa / 40% Jugador - Retención Progresiva de Tesorería)"
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

        public class UpdateBotDifficultyRequest
        {
            public int BotId { get; set; }
            public string Difficulty { get; set; } = "facil";
            public bool? IsActive { get; set; }
        }

        [HttpGet("virtual-bots")]
        public async Task<IActionResult> GetVirtualBots()
        {
            var bots = await _context.Users
                .Where(u => u.IsVirtualBot)
                .OrderBy(u => u.Id)
                .ToListAsync();

            var botMatches = await _context.BotMatchRecords.ToListAsync();

            var result = bots.Select(b =>
            {
                var matches = botMatches.Where(m => m.BotName.Equals(b.Username, StringComparison.OrdinalIgnoreCase)).ToList();
                int totalMatches = matches.Count;
                int botWins = matches.Count(m => !m.UserWon);
                int botLosses = matches.Count(m => m.UserWon);
                int houseProfit = matches.Sum(m => m.HouseProfit);
                double winRate = totalMatches > 0 ? Math.Round((double)botWins / totalMatches * 100, 1) : 0;

                return new
                {
                    id = b.Id,
                    username = b.Username,
                    avatarUrl = b.AvatarUrl,
                    level = b.GetCalculatedLevel(),
                    coins = b.Coins,
                    isActive = b.IsActive,
                    difficulty = b.BotDifficulty ?? "facil",
                    totalMatches,
                    botWins,
                    botLosses,
                    winRate,
                    houseProfit
                };
            }).ToList();

            return Ok(result);
        }

        [HttpPost("virtual-bots/difficulty")]
        public async Task<IActionResult> UpdateVirtualBotDifficulty([FromBody] UpdateBotDifficultyRequest request)
        {
            if (request == null || request.BotId <= 0)
                return BadRequest(new { message = "ID de bot inválido." });

            var bot = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.BotId && u.IsVirtualBot);
            if (bot == null)
                return NotFound(new { message = "Bot no encontrado." });

            if (!string.IsNullOrWhiteSpace(request.Difficulty))
            {
                var diff = request.Difficulty.Trim().ToLowerInvariant();
                if (diff == "facil" || diff == "medio" || diff == "dificil")
                {
                    bot.BotDifficulty = diff;
                }
            }

            if (request.IsActive.HasValue)
            {
                bot.IsActive = request.IsActive.Value;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = $"Configuración de {bot.Username} actualizada.", bot });
        }

        public class RenameVirtualBotRequest
        {
            public int BotId { get; set; }
            public string NewName { get; set; } = string.Empty;
        }

        [HttpPost("virtual-bots/rename")]
        public async Task<IActionResult> RenameVirtualBot([FromBody] RenameVirtualBotRequest request)
        {
            if (request == null || request.BotId <= 0)
                return BadRequest(new { message = "ID de bot inválido." });

            if (string.IsNullOrWhiteSpace(request.NewName))
                return BadRequest(new { message = "El nombre del bot no puede estar vacío." });

            var cleanName = request.NewName.Trim();
            if (cleanName.Length < 2 || cleanName.Length > 25)
                return BadRequest(new { message = "El nombre debe tener entre 2 y 25 caracteres." });

            var bot = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.BotId && u.IsVirtualBot);
            if (bot == null)
                return NotFound(new { message = "Bot no encontrado." });

            var nameLower = cleanName.ToLower();
            if (nameLower == "guardian" || nameLower == "admin")
                return BadRequest(new { message = "Ese nombre está reservado para el sistema." });

            var nameExists = await _context.Users.AnyAsync(u => u.Id != bot.Id && u.Username.ToLower() == nameLower);
            if (nameExists)
                return BadRequest(new { message = $"El nombre '{cleanName}' ya está en uso por otro jugador o bot." });

            var oldName = bot.Username;
            bot.Username = cleanName;

            // Actualizar histórico de partidas contra bot para que sus estadísticas sigan vinculadas
            var botMatches = await _context.BotMatchRecords
                .Where(m => m.BotName.ToLower() == oldName.ToLower())
                .ToListAsync();
            foreach (var bm in botMatches)
            {
                bm.BotName = cleanName;
            }

            var pvpMatches = await _context.MatchBetRecords
                .Where(m => m.PlayerTwoName.ToLower() == oldName.ToLower())
                .ToListAsync();
            foreach (var pm in pvpMatches)
            {
                pm.PlayerTwoName = cleanName;
                if (pm.WinnerUsername.ToLower() == oldName.ToLower()) pm.WinnerUsername = cleanName;
                if (pm.LoserUsername.ToLower() == oldName.ToLower()) pm.LoserUsername = cleanName;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Bot renombrado con éxito de '{oldName}' a '{cleanName}'.",
                botId = bot.Id,
                oldName,
                newName = cleanName
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

            if (!targetUsers.Any(u => u.Id == 39 || u.Username.Equals("Marpro74", StringComparison.OrdinalIgnoreCase)))
            {
                var marproUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == 39 || u.Username == "Marpro74");
                if (marproUser != null) targetUsers.Add(marproUser);
            }

            // Che ahora es gestionado dinámicamente mediante el nuevo modo de calibración

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
            });
        }

        [HttpGet("vip-users")]
        public async Task<IActionResult> GetVipUsers()
        {
            var vipIds = GamePlayOneVsOne.FavoredVipUserIds.ToList();
            var vipNames = GamePlayOneVsOne.FavoredVipUsers.ToList();
            var vipEmails = GamePlayOneVsOne.FavoredVipEmails.ToList();

            var usersFromDb = await _context.Users
                .Where(u => vipIds.Contains(u.Id) || vipNames.Contains(u.Username) || (u.Email != null && vipEmails.Contains(u.Email)))
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.AvatarUrl,
                    u.Coins,
                    u.Wins,
                    u.Losses
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                vipUsers = usersFromDb,
                configuredIds = vipIds,
                configuredUsernames = vipNames,
                configuredEmails = vipEmails
            });
        }

        [HttpPost("vip-users")]
        public async Task<IActionResult> AddVipUser([FromBody] ManageStabilizationRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) && req.UserId <= 0)
            {
                return BadRequest(new { message = "Debe especificar un nombre de usuario o ID." });
            }

            string uname = req.Username?.Trim() ?? "";
            int uid = req.UserId;
            string email = "";

            if (uid <= 0 && !string.IsNullOrEmpty(uname))
            {
                var dbU = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == uname.ToLower());
                if (dbU != null) { uid = dbU.Id; email = dbU.Email ?? ""; }
            }
            else if (uid > 0)
            {
                var dbU = await _context.Users.FindAsync(uid);
                if (dbU != null) { uname = dbU.Username; email = dbU.Email ?? ""; }
            }

            GamePlayOneVsOne.AddFavoredVipUser(uname, uid, email);

            return Ok(new
            {
                success = true,
                message = $"Usuario VIP '{uname}' (ID {uid}) añadido con ventaja exclusiva.",
                vipUsers = GamePlayOneVsOne.FavoredVipUsers,
                vipUserIds = GamePlayOneVsOne.FavoredVipUserIds,
                vipEmails = GamePlayOneVsOne.FavoredVipEmails
            });
        }

        [HttpDelete("vip-users/{identifier}")]
        public IActionResult RemoveVipUser(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return BadRequest(new { message = "Identificador requerido." });
            }

            string clean = identifier.Trim();
            if (int.TryParse(clean, out int uid))
            {
                GamePlayOneVsOne.RemoveFavoredVipUser("", uid);
            }
            else
            {
                GamePlayOneVsOne.RemoveFavoredVipUser(clean, 0, clean.Contains("@") ? clean : null);
            }

            return Ok(new
            {
                success = true,
                message = $"Usuario VIP '{clean}' removido de la ventaja exclusiva.",
                vipUsers = GamePlayOneVsOne.FavoredVipUsers,
                vipUserIds = GamePlayOneVsOne.FavoredVipUserIds
            });
        }

        [HttpGet("matches")]
        public async Task<IActionResult> GetMatches([FromQuery] string? player, [FromQuery] string? period, [FromQuery] string? type)
        {
            var query = _context.MatchBetRecords.AsQueryable();

            string matchType = (type ?? "pvp").Trim().ToLowerInvariant();
            if (matchType == "pvp")
            {
                query = query.Where(m => !m.IsBotMatch);
            }
            else if (matchType == "bot")
            {
                query = query.Where(m => m.IsBotMatch);
            }

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
                    isBotMatch = m.IsBotMatch,
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
                type = matchType,
                summary,
                dailyActivity,
                matches
            });
        }

        [HttpGet("bot-matches")]
        public async Task<IActionResult> GetBotMatches([FromQuery] string? search, [FromQuery] string? filter)
        {
            // Respaldo de seguridad: si BotMatchRecords está vacío y MatchBetRecords tiene partidas vs Bot, restaurar inmediatamente
            if (!await _context.BotMatchRecords.AnyAsync() && await _context.MatchBetRecords.AnyAsync(m => m.IsBotMatch))
            {
                var botBetMatches = await _context.MatchBetRecords.Where(m => m.IsBotMatch).OrderBy(m => m.CreatedAt).ToListAsync();
                var virtualBots = await _context.Users.Where(u => u.IsVirtualBot).ToListAsync();
                var botNames = new HashSet<string>(virtualBots.Select(u => u.Username.ToLower()), StringComparer.OrdinalIgnoreCase);

                foreach (var m in botBetMatches)
                {
                    bool p2IsBot = botNames.Contains(m.PlayerTwoName.ToLower()) || 
                                   m.PlayerTwoName.ToLower().Contains("bot") || 
                                   !botNames.Contains(m.PlayerOneName.ToLower());
                    
                    string botName = p2IsBot ? m.PlayerTwoName : m.PlayerOneName;
                    string humanName = p2IsBot ? m.PlayerOneName : m.PlayerTwoName;
                    bool humanWon = m.WinnerUsername.Equals(humanName, StringComparison.OrdinalIgnoreCase);

                    int bet = m.BetPerPlayer > 0 ? m.BetPerPlayer : 200;
                    int houseCommission = m.HouseCommission > 0 ? m.HouseCommission : (int)Math.Round(bet * 0.1);
                    int winnerPrize = m.WinnerPrize > 0 ? m.WinnerPrize : (bet * 2 - houseCommission);

                    var humanUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == humanName);

                    _context.BotMatchRecords.Add(new BotMatchRecord
                    {
                        UserId = humanUser?.Id ?? 0,
                        Username = humanName,
                        BotName = botName,
                        BetAmount = bet,
                        UserWon = humanWon,
                        CoinsWon = humanWon ? (winnerPrize - bet) : 0,
                        CoinsLost = humanWon ? 0 : bet,
                        HouseCommission = houseCommission,
                        HouseProfit = humanWon ? -(winnerPrize - bet) : bet,
                        UserCoinsBefore = 0,
                        UserCoinsAfter = humanUser?.Coins ?? 0,
                        EndReason = !string.IsNullOrEmpty(m.EndReason) ? m.EndReason : "VictoriaPorPuntos",
                        CreatedAt = m.CreatedAt
                    });
                }
                await _context.SaveChangesAsync();
            }

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
            var totalHouseCommission = allList.Sum(m => m.HouseCommission);
            var netHouseProfit = allList.Sum(m => m.HouseProfit);

            // CÁLCULO DIARIO (Hora de Venezuela UTC-4): Inicia en Cero a la medianoche
            var vzlaNow = VenezuelaTime.Now;
            var todayStr = vzlaNow.ToString("yyyy-MM-dd");
            var yesterdayStr = vzlaNow.AddDays(-1).ToString("yyyy-MM-dd");

            var todayList = allList.Where(m => m.CreatedAt.AddHours(-4).ToString("yyyy-MM-dd") == todayStr).ToList();
            var todayTotalMatches = todayList.Count;
            var todayUserWins = todayList.Count(m => m.UserWon);
            var todayBotWins = todayList.Count(m => !m.UserWon);
            var todayUserWinRate = todayTotalMatches > 0 ? Math.Round((double)todayUserWins / todayTotalMatches * 100, 1) : 0;
            var todayBotWinRate = todayTotalMatches > 0 ? Math.Round((double)todayBotWins / todayTotalMatches * 100, 1) : 0;
            var todayCoinsWagered = todayList.Sum(m => m.BetAmount);
            var todayCoinsWonByUser = todayList.Sum(m => m.CoinsWon);
            var todayCoinsWonByHouse = todayList.Sum(m => m.CoinsLost);
            var todayHouseCommission = todayList.Sum(m => m.HouseCommission);
            var todayNetHouseProfit = todayList.Sum(m => m.HouseProfit);

            var todaySummary = new
            {
                date = todayStr,
                totalMatches = todayTotalMatches,
                botWins = todayBotWins,
                userWins = todayUserWins,
                botWinRate = todayBotWinRate,
                userWinRate = todayUserWinRate,
                totalCoinsWagered = todayCoinsWagered,
                coinsWonByUser = todayCoinsWonByUser,
                coinsWonByHouse = todayCoinsWonByHouse,
                houseCommission = todayHouseCommission,
                netHouseProfit = todayNetHouseProfit,
                targetWinRate = 60.0
            };

            // CÁLCULO AYER (Hora de Venezuela UTC-4)
            var yesterdayList = allList.Where(m => m.CreatedAt.AddHours(-4).ToString("yyyy-MM-dd") == yesterdayStr).ToList();
            var yesterdayTotalMatches = yesterdayList.Count;
            var yesterdayUserWins = yesterdayList.Count(m => m.UserWon);
            var yesterdayBotWins = yesterdayList.Count(m => !m.UserWon);
            var yesterdayUserWinRate = yesterdayTotalMatches > 0 ? Math.Round((double)yesterdayUserWins / yesterdayTotalMatches * 100, 1) : 0;
            var yesterdayBotWinRate = yesterdayTotalMatches > 0 ? Math.Round((double)yesterdayBotWins / yesterdayTotalMatches * 100, 1) : 0;
            var yesterdayCoinsWagered = yesterdayList.Sum(m => m.BetAmount);
            var yesterdayCoinsWonByUser = yesterdayList.Sum(m => m.CoinsWon);
            var yesterdayCoinsWonByHouse = yesterdayList.Sum(m => m.CoinsLost);
            var yesterdayHouseCommission = yesterdayList.Sum(m => m.HouseCommission);
            var yesterdayNetHouseProfit = yesterdayList.Sum(m => m.HouseProfit);

            var yesterdaySummary = new
            {
                date = yesterdayStr,
                totalMatches = yesterdayTotalMatches,
                botWins = yesterdayBotWins,
                userWins = yesterdayUserWins,
                botWinRate = yesterdayBotWinRate,
                userWinRate = yesterdayUserWinRate,
                totalCoinsWagered = yesterdayCoinsWagered,
                coinsWonByUser = yesterdayCoinsWonByUser,
                coinsWonByHouse = yesterdayCoinsWonByHouse,
                houseCommission = yesterdayHouseCommission,
                netHouseProfit = yesterdayNetHouseProfit,
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
                        houseCommission = g.Sum(m => m.HouseCommission),
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
                    houseCommission = m.HouseCommission,
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
                    totalHouseCommission,
                    netHouseProfit
                },
                todaySummary,
                yesterdaySummary,
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

        [HttpPost("recharge/{id}/reassign-user")]
        public async Task<IActionResult> ReassignRechargeUser(int id, [FromBody] ReassignRechargeDto dto)
        {
            var recharge = await _context.PaymentRecharges.FindAsync(id);
            if (recharge == null)
            {
                return NotFound(new { message = "Recarga no encontrada." });
            }

            var targetUser = await _context.Users.FindAsync(dto.NewUserId);
            if (targetUser == null)
            {
                return NotFound(new { message = "Usuario de destino no encontrado." });
            }

            int oldUserId = recharge.UserId;
            recharge.UserId = dto.NewUserId;
            recharge.AdminNotes = $"Reasignada de usuario #{oldUserId} a #{dto.NewUserId} ({targetUser.Username}) por corrección de titular de pago.";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                rechargeId = id,
                oldUserId,
                newUserId = dto.NewUserId,
                newUsername = targetUser.Username,
                message = $"Recarga #{id} reasignada con éxito a {targetUser.Username} (ID #{dto.NewUserId})."
            });
        }

        [HttpPost("recharge/{id}/reset-pending")]
        public async Task<IActionResult> ResetRechargePending(int id, [FromBody] ResetPendingDto? dto)
        {
            var recharge = await _context.PaymentRecharges
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recharge == null)
            {
                return NotFound(new { message = "Recarga no encontrada." });
            }

            if (recharge.Status == "APROBADO" && recharge.User != null && dto?.RevertCoins == true)
            {
                if (recharge.User.Coins >= recharge.CoinsAmount)
                {
                    recharge.User.Coins -= recharge.CoinsAmount;
                }
            }

            recharge.Status = "PENDIENTE";
            recharge.ProcessedAt = null;
            recharge.AdminNotes = "Restablecida a estado PENDIENTE para revisión manual exclusiva del administrador.";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                id = recharge.Id,
                status = recharge.Status,
                userCoins = recharge.User != null ? recharge.User.Coins : 0,
                message = $"Recarga #{id} restablecida a estado PENDIENTE con éxito."
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
                    isAdmin = u.IsAdmin,
                    botDifficulty = u.BotDifficulty ?? "general"
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

            int previousCoins = user.Coins;
            user.Coins = Math.Max(0, user.Coins + dto.Amount);
            if (user.BonusCoins > user.Coins)
            {
                user.BonusCoins = user.Coins;
            }
            int diff = user.Coins - previousCoins;
            await _context.SaveChangesAsync();

            string actionDesc = diff >= 0 ? $"acreditadas +{diff}" : $"deducidas -{Math.Abs(diff)}";
            Console.WriteLine($"[AdminAdjustCoins] Administrador ajustó monedas a {user.Username} (ID {user.Id}): Anterior {previousCoins}, Ajuste {dto.Amount}, Actual {user.Coins} ({actionDesc}). Motivo: {dto.Reason ?? "Sin motivo especificado"}");

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                coins = user.Coins,
                previousCoins = previousCoins,
                diff = diff,
                message = diff >= 0
                    ? $"Se acreditaron con éxito {diff} monedas a {user.Username}. Saldo actual: {user.Coins} monedas."
                    : $"Se dedujeron con éxito {Math.Abs(diff)} monedas a {user.Username}. Saldo actual: {user.Coins} monedas."
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

            var withdrawals = await query
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();

            var userIds = withdrawals.Select(w => w.UserId).Distinct().ToList();
            var botRecords = await _context.BotMatchRecords
                .Where(b => userIds.Contains(b.UserId))
                .ToListAsync();

            var list = withdrawals.Select(w =>
            {
                var userBotMatches = botRecords.Where(b => b.UserId == w.UserId).ToList();
                int coinsWonAgainstBots = userBotMatches.Sum(b => b.CoinsWon);
                int coinsLostAgainstBots = userBotMatches.Sum(b => b.CoinsLost);
                int netProfitAgainstBots = coinsWonAgainstBots - coinsLostAgainstBots;
                bool isHighRiskFromBots = netProfitAgainstBots > 1000;

                return new
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
                    processedAt = w.ProcessedAt,
                    netProfitAgainstBots,
                    isHighRiskFromBots,
                    riskWarning = isHighRiskFromBots ? $"⚠️ ALTO RIESGO: Acumuló {netProfitAgainstBots} monedas ganadas a bots." : ""
                };
            }).ToList();

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

            // CANDADO FINANCIERO: Alerta si supera el límite de 5.000 Bs o si no hay depósito aprobado
            if (withdrawal.User != null && !withdrawal.User.IsAdmin && withdrawal.User.Username.ToLower() != "guardian")
            {
                if (withdrawal.CoinsAmount > PaymentController.MAX_DAILY_WITHDRAWAL_COINS && !dto.ForceApprove)
                {
                    return BadRequest(new
                    {
                        message = $"⚠️ ALERTA DE SEGURIDAD DE BANCA: Este retiro ({withdrawal.CoinsAmount:N0} Bs.) SUPERA el límite diario de 10.000 Bs. Si estás absolutamente seguro de aprobarlo por excepción, debes activar ForceApprove.",
                        isHighAmount = true,
                        amount = withdrawal.CoinsAmount
                    });
                }

                bool hasDeposit = await _context.PaymentRecharges
                    .AnyAsync(r => r.UserId == withdrawal.UserId && r.Status == "APROBADO");

                if (!hasDeposit && !dto.ForceApprove)
                {
                    return BadRequest(new
                    {
                        message = "⚠️ ALERTA CRÍTICA: Este usuario NUNCA ha realizado una recarga aprobada. No se debe pagar saldo regalado sin depósito.",
                        hasNoDeposit = true
                    });
                }
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

            if (withdrawal.Status == "PAGADO" && !(dto?.ForceCancelPaid ?? false))
            {
                return BadRequest(new { message = "No se puede rechazar un retiro que ya fue marcado como pagado. Usa ForceCancelPaid si fue error o pago rebotado." });
            }
            if (withdrawal.Status == "PAGADO" && (dto?.ForceCancelPaid ?? false))
            {
                withdrawal.AdminReference = "ANULADO_POR_FRAUDE";
            }

            if (withdrawal.Status == "RECHAZADO")
            {
                return BadRequest(new { message = "Este retiro ya fue rechazado anteriormente." });
            }

            bool isConfiscated = dto?.Confiscate ?? false;
            withdrawal.Status = "RECHAZADO";
            withdrawal.AdminNotes = isConfiscated
                ? $"[CONFISCADO POR FRAUDE/COLUSIÓN] {dto?.Reason ?? "Abuso o irregularidades en partidas."}"
                : (dto?.Reason ?? "Datos de pago móvil no válidos.");
            withdrawal.ProcessedAt = VenezuelaTime.Now;

            int refunded = 0;
            if (withdrawal.User != null)
            {
                if (!isConfiscated)
                {
                    // Reembolso normal (por ejemplo, si el número de teléfono o banco estaba errado)
                    withdrawal.User.Coins += withdrawal.CoinsAmount;
                    refunded = withdrawal.CoinsAmount;
                }
                else
                {
                    // Confiscación: NO se devuelven las monedas y se sanean BonusCoins
                    withdrawal.User.BonusCoins = 0;
                    Console.WriteLine($"[AdminController] Retiro #{id} CONFISCADO. Se retuvieron {withdrawal.CoinsAmount} monedas a {withdrawal.User.Username}.");
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = withdrawal.Id,
                status = withdrawal.Status,
                adminNotes = withdrawal.AdminNotes,
                isConfiscated,
                coinsRefunded = refunded,
                userNewCoins = withdrawal.User?.Coins,
                message = isConfiscated
                    ? $"Retiro confiscado. Se anularon {withdrawal.CoinsAmount} monedas a {withdrawal.User?.Username} sin reembolso por irregularidades."
                    : $"Retiro rechazado. Se han reembolsado {withdrawal.CoinsAmount} monedas a {withdrawal.User?.Username}."
            });
        }

        [HttpPost("withdrawal/manual")]
        public async Task<IActionResult> CreateManualWithdrawal([FromBody] CreateManualWithdrawalDto dto)
        {
            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            if (dto.AmountCoins <= 0)
            {
                return BadRequest(new { message = "El monto de monedas debe ser mayor a 0." });
            }

            // Deducir monedas del balance del usuario si se solicita (por defecto true)
            if (dto.DeductCoins)
            {
                user.Coins = Math.Max(0, user.Coins - dto.AmountCoins);
                if (user.BonusCoins > user.Coins)
                {
                    user.BonusCoins = user.Coins;
                }
            }

            var withdrawal = new PaymentWithdrawal
            {
                UserId = user.Id,
                AmountBs = dto.AmountBs > 0 ? dto.AmountBs : (decimal)dto.AmountCoins,
                CoinsAmount = dto.AmountCoins,
                BankName = !string.IsNullOrWhiteSpace(dto.BankName) ? dto.BankName : (!string.IsNullOrWhiteSpace(user.BankName) ? user.BankName : "PAGO_MANUAL"),
                PhoneNumber = !string.IsNullOrWhiteSpace(dto.PhoneNumber) ? dto.PhoneNumber : (!string.IsNullOrWhiteSpace(user.PhoneNumber) ? user.PhoneNumber : "PAGO_MANUAL"),
                IdCard = !string.IsNullOrWhiteSpace(dto.IdCard) ? dto.IdCard : (!string.IsNullOrWhiteSpace(user.Cedula) ? user.Cedula : "PAGO_MANUAL"),
                Status = "PAGADO",
                AdminReference = !string.IsNullOrWhiteSpace(dto.Reference) ? dto.Reference : "PAGO_MANUAL_PEWI",
                CreatedAt = VenezuelaTime.Now,
                ProcessedAt = VenezuelaTime.Now
            };

            _context.PaymentWithdrawals.Add(withdrawal);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                withdrawalId = withdrawal.Id,
                userId = user.Id,
                username = user.Username,
                amountCoins = withdrawal.CoinsAmount,
                amountBs = withdrawal.AmountBs,
                userRemainingCoins = user.Coins,
                status = withdrawal.Status,
                adminReference = withdrawal.AdminReference,
                message = $"Retiro manual de {withdrawal.AmountBs:N0} Bs. ({withdrawal.CoinsAmount:N0} monedas) registrado como PAGADO para {user.Username}."
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
                return Unauthorized(new { message = "Acceso denegado. Credenciales de administrador no autorizadas." });
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
            if (!valid && dto.Password == "Steven.09")
            {
                valid = true;
                admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Steven.09");
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
                message = "Bienvenido al Panel de Control de Administración."
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

        public class SetUserBotDifficultyRequest
        {
            public string Difficulty { get; set; } = "medio";
        }

        [HttpPost("user/{id}/bot-difficulty")]
        public async Task<IActionResult> SetUserBotDifficulty(int id, [FromBody] SetUserBotDifficultyRequest request)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            string clean = (request?.Difficulty ?? "medio").Trim().ToLowerInvariant();

            if (clean != "facil" && clean != "medio" && clean != "dificil" && clean != "general" && clean != "global" && clean != "favor_jugador" && clean != "favorecido" && clean != "favor_40_60")

            {

                return BadRequest(new { message = "Dificultad inválida. Debe ser 'facil', 'medio', 'dificil', 'general' o 'favor_jugador'." });

            }



            if (clean == "favor_40_60" || clean == "favorecido") clean = "favor_jugador";



            if (clean == "general" || clean == "global")

            {

                user.BotDifficulty = null;

                GamePlayOneVsOne.StabilizedUserIds.Remove(user.Id);

                GamePlayOneVsOne.StabilizedUsers.Remove(user.Username);

                GamePlayOneVsOne.FavoredVipUserIds.Remove(user.Id);

                GamePlayOneVsOne.FavoredVipUsers.Remove(user.Username);

            }

            else if (clean == "favor_jugador")

            {

                user.BotDifficulty = "favor_jugador";

                GamePlayOneVsOne.StabilizedUserIds.Remove(user.Id);

                GamePlayOneVsOne.StabilizedUsers.Remove(user.Username);

                GamePlayOneVsOne.FavoredVipUserIds.Add(user.Id);

                GamePlayOneVsOne.FavoredVipUsers.Add(user.Username);

            }

            else

            {

                user.BotDifficulty = clean;



                // Sincronizar listas en memoria

                if (clean == "dificil")

                {

                    GamePlayOneVsOne.StabilizedUserIds.Add(user.Id);

                    GamePlayOneVsOne.StabilizedUsers.Add(user.Username);

                    GamePlayOneVsOne.FavoredVipUserIds.Remove(user.Id);

                    GamePlayOneVsOne.FavoredVipUsers.Remove(user.Username);

                }

                else if (clean == "facil")

                {

                    GamePlayOneVsOne.FavoredVipUserIds.Add(user.Id);

                    GamePlayOneVsOne.FavoredVipUsers.Add(user.Username);

                    GamePlayOneVsOne.StabilizedUserIds.Remove(user.Id);

                    GamePlayOneVsOne.StabilizedUsers.Remove(user.Username);

                }

                else // medio (estándar 70-30 de tesorería)

                {

                    GamePlayOneVsOne.StabilizedUserIds.Remove(user.Id);

                    GamePlayOneVsOne.StabilizedUsers.Remove(user.Username);

                    GamePlayOneVsOne.FavoredVipUserIds.Remove(user.Id);

                    GamePlayOneVsOne.FavoredVipUsers.Remove(user.Username);

                }

            }



            GamePlayOneVsOne.SaveBotSettingsToFile();

            await _context.SaveChangesAsync();



            string desc = clean switch

            {

                "favor_jugador" => "⭐ 60/40 A FAVOR DE CHE (60% Che / 40% Casa - Blindado máx 3 victorias, Anti-desfalco)",

                "facil" => "FÁCIL (50% Casa / 50% Jugador - Modo Recreativo)",

                "dificil" => "DIFÍCIL (85% Casa / 15% Jugador - Muro Total de Contención)",

                "general" or "global" => $"GENERAL (Hereda la dificultad global: {GamePlayOneVsOne.BotDifficultyMode.ToUpperInvariant()})",

                _ => "MEDIO (60% Casa / 40% Jugador - Retención Progresiva de Tesorería)"

            };

            Console.WriteLine($"[AdminController] Dificultad de bots para {user.Username} (ID {user.Id}) cambiada a {desc} por Administrador.");

            return Ok(new

            {

                id = user.Id,

                username = user.Username,

                botDifficulty = user.BotDifficulty ?? "general",

                message = $"Dificultad de bots para {user.Username} establecida en {desc}."

            });

        }



        public class SetCheDifficultyRequest

        {

            public string Mode { get; set; } = "favor_jugador";

        }



        [HttpGet("special-user/che")]

        public async Task<IActionResult> GetCheSpecialStatus()

        {

            var cheUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == 202 || u.Username == "Che" || u.Username.ToLower() == "che");

            if (cheUser == null)

            {

                return NotFound(new { message = "Usuario Che no encontrado en la base de datos." });

            }



            var recentMatches = await _context.BotMatchRecords

                .Where(m => m.UserId == cheUser.Id || m.Username == cheUser.Username || m.Username.ToLower() == cheUser.Username.ToLower())

                .OrderByDescending(m => m.CreatedAt)

                .Take(10)

                .Select(m => new

                {

                    m.Id,

                    m.BotName,

                    Bet = m.BetAmount,

                    m.UserWon,

                    m.CoinsWon,

                    m.CoinsLost,

                    m.CreatedAt

                })

                .ToListAsync();



            int totalMatches = await _context.BotMatchRecords.CountAsync(m => m.UserId == cheUser.Id || m.Username == cheUser.Username || m.Username.ToLower() == cheUser.Username.ToLower());

            int userWins = await _context.BotMatchRecords.CountAsync(m => (m.UserId == cheUser.Id || m.Username == cheUser.Username || m.Username.ToLower() == cheUser.Username.ToLower()) && m.UserWon);

            int botWins = totalMatches - userWins;



            return Ok(new

            {

                id = cheUser.Id,

                username = cheUser.Username,

                email = cheUser.Email,

                coins = cheUser.Coins,

                level = cheUser.Level,

                botDifficulty = cheUser.BotDifficulty ?? "general",

                totalMatches,

                userWins,

                botWins,

                userWinRate = totalMatches > 0 ? Math.Round((double)userWins / totalMatches * 100, 1) : 0,

                recentMatches

            });

        }



        [HttpPost("special-user/che/mode")]

        public async Task<IActionResult> SetCheMode([FromBody] SetCheDifficultyRequest req)

        {

            var cheUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == 202 || u.Username == "Che" || u.Username.ToLower() == "che");

            if (cheUser == null)

            {

                return NotFound(new { message = "Usuario Che no encontrado en la base de datos." });

            }



            return await SetUserBotDifficulty(cheUser.Id, new SetUserBotDifficultyRequest { Difficulty = req?.Mode ?? "favor_jugador" });

        }

        public class BanDeviceClonesRequest
        {
            public int UserId { get; set; }
        }

        [HttpPost("users/ban-device-clones")]
        public async Task<IActionResult> BanDeviceClones([FromBody] BanDeviceClonesRequest request)
        {
            var targetUser = await _context.Users.FindAsync(request.UserId);
            if (targetUser == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            var fp = targetUser.DeviceFingerprint;
            var usernameClean = System.Text.RegularExpressions.Regex.Replace(targetUser.Username, @"\d+$", "").Trim();

            // Buscar cuentas vinculadas por misma huella digital O clones correlativos (ej: Diego1..14, Rafa1..13)
            var clones = await _context.Users
                .Where(u => u.Id != targetUser.Id && !u.IsAdmin && (
                    (!string.IsNullOrEmpty(fp) && u.DeviceFingerprint == fp) ||
                    (!string.IsNullOrEmpty(usernameClean) && usernameClean.Length >= 4 && u.Username.StartsWith(usernameClean))
                ))
                .ToListAsync();

            int bannedCount = 0;
            foreach (var clone in clones)
            {
                clone.IsActive = false;
                clone.Coins = 0;
                clone.BonusCoins = 0;
                bannedCount++;
            }

            // También suspender y vaciar la cuenta objetivo si no es administrador
            if (!targetUser.IsAdmin)
            {
                targetUser.IsActive = false;
                targetUser.Coins = 0;
                targetUser.BonusCoins = 0;
                bannedCount++;
            }

            await _context.SaveChangesAsync();

            Console.WriteLine($"[AdminController] BanDeviceClones ejecutado. {bannedCount} cuentas suspendidas vinculadas a {targetUser.Username}.");

            return Ok(new
            {
                success = true,
                targetUser = targetUser.Username,
                bannedCount,
                message = $"Se suspendieron y vaciaron {bannedCount} cuentas multicuentas vinculadas a {targetUser.Username}."
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
            var totalBotHouseCommissions = botMatches.Sum(m => m.HouseCommission);

            var userCoinsInCirculation = await _context.Users.SumAsync(u => u.Coins);

            var expenses = await _context.PlatformExpenses.OrderByDescending(e => e.CreatedAt).ToListAsync();
            var totalBsExpenses = expenses.Where(e => e.Currency == "BS").Sum(e => e.Amount);
            var totalUsdtExpenses = expenses.Where(e => e.Currency == "USDT" && !e.Category.Contains("Venta USDT") && !e.Category.Contains("Venta de USDT")).Sum(e => e.Amount);
            var totalUsdtPurchased = expenses.Where(e => e.UsdtReceived != null && e.UsdtReceived > 0).Sum(e => e.UsdtReceived ?? 0);
            var totalUsdtSold = expenses.Where(e => (e.Category.Contains("Venta USDT") || e.Category.Contains("Venta de USDT")) && e.Currency == "USDT").Sum(e => e.Amount);
            var totalBsFromUsdtSold = expenses.Where(e => e.Category.Contains("Venta USDT") || e.Category.Contains("Venta de USDT")).Sum(e => e.BsReceived ?? (e.ExchangeRate != null ? e.Amount * e.ExchangeRate.Value : 0m));
            var cajaNetaBs = totalBsRecharges - totalBsWithdrawals - totalBsExpenses + totalBsFromUsdtSold;
            var cajaNetaUsdt = totalUsdtPurchased - totalUsdtExpenses - totalUsdtSold;

            var expensesByCategory = expenses
                .GroupBy(e => new { e.Category, e.Currency })
                .Select(g => new
                {
                    category = g.Key.Category,
                    currency = g.Key.Currency,
                    total = g.Sum(x => x.Amount),
                    count = g.Count()
                })
                .ToList();

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
                    cajaNetaBs,
                    cajaNetaUsdt,
                    totalBsExpenses,
                    totalUsdtExpenses,
                    totalUsdtPurchased,
                    totalUsdtSold,
                    totalBsFromUsdtSold,
                    expensesByCategory,
                    netBsBalanceWithPending = (totalBsRecharges + pendingBsRecharges) - (totalBsWithdrawals + pendingBsWithdrawals),
                    totalCoinsCirculating = userCoinsInCirculation,
                    totalCommissionsCollected = totalCommissions,
                    totalMatchesPlayed = totalMatches,
                    totalCoinsWagered = totalWagered,
                    totalBotMatchesPlayed = totalBotMatches,
                    totalBotCoinsWagered = totalBotWagered,
                    totalBotHouseProfit = totalBotHouseProfit,
                    totalBotHouseCommissions = totalBotHouseCommissions,
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
        public IActionResult ResetSeasonStats([FromBody] ResetSeasonDto? dto)
        {
            return BadRequest(new 
            { 
                success = false, 
                message = "La función de reseteo masivo de temporada y monedas ha sido eliminada permanentemente del sistema por seguridad de los fondos de los jugadores." 
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

        [HttpGet("reactivation/candidates")]
        public async Task<IActionResult> GetReactivationCandidates()
        {
            var query = _context.Users
                .Where(u => !u.IsVirtualBot && !u.IsAdmin && u.IsActive && !string.IsNullOrWhiteSpace(u.PhoneNumber));

            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            var eligibleQuery = query.Where(u => u.LastReactivatedAt == null || u.LastReactivatedAt < sevenDaysAgo);

            var totalEligible = await eligibleQuery.CountAsync();

            var candidates = await eligibleQuery
                .OrderBy(u => u.Coins)
                .ThenBy(u => u.LastReactivatedAt)
                .ThenBy(u => u.CreatedAt)
                .Take(10)
                .Select(u => new
                {
                    id = u.Id,
                    username = u.Username,
                    phoneNumber = u.PhoneNumber,
                    coins = u.Coins,
                    bonusCoins = u.BonusCoins,
                    wins = u.Wins,
                    level = u.Level,
                    createdAt = u.CreatedAt.ToString("yyyy-MM-dd"),
                    lastReactivatedAt = u.LastReactivatedAt.HasValue ? u.LastReactivatedAt.Value.ToString("yyyy-MM-dd HH:mm") : null,
                    reactivationCount = u.ReactivationCount
                })
                .ToListAsync();

            var result = candidates.Select(c =>
            {
                var rawPhone = c.phoneNumber ?? "";
                var cleanDigits = System.Text.RegularExpressions.Regex.Replace(rawPhone, @"[^\d]", "");
                if (cleanDigits.StartsWith("04"))
                {
                    cleanDigits = "58" + cleanDigits.Substring(1);
                }
                else if (cleanDigits.Length == 10 && (cleanDigits.StartsWith("412") || cleanDigits.StartsWith("414") || cleanDigits.StartsWith("424") || cleanDigits.StartsWith("416") || cleanDigits.StartsWith("426")))
                {
                    cleanDigits = "58" + cleanDigits;
                }

                var customMsg = $"¡Hola {c.username}! Saludos de parte de El Pericón Criollo 🌵. Vimos que tienes unos días sin jugar y te acabamos de recargar 300 MONEDAS DE CORTESÍA en tu cuenta para que compitas en el nuevo Ranking Diario con premios en Pago Móvil 🏆. ¡Entra ya a disfrutarlas!: https://pericon.lat";

                return new
                {
                    c.id,
                    c.username,
                    originalPhone = c.phoneNumber,
                    cleanPhone = cleanDigits,
                    c.coins,
                    c.wins,
                    c.level,
                    c.createdAt,
                    c.lastReactivatedAt,
                    c.reactivationCount,
                    suggestedMessage = customMsg,
                    waUrl = $"https://wa.me/{cleanDigits}?text={Uri.EscapeDataString(customMsg)}"
                };
            }).ToList();

            return Ok(new
            {
                success = true,
                totalEligible,
                batchSize = result.Count,
                candidates = result
            });
        }

        [HttpPost("reactivation/credit-user")]
        public async Task<IActionResult> CreditUserReactivation([FromBody] CreditReactivationDto request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "Usuario no encontrado." });
            }

            int amount = request.BonusCoins > 0 ? request.BonusCoins : 300;
            user.Coins += amount;
            user.BonusCoins += amount;
            user.LastReactivatedAt = DateTime.UtcNow;
            user.ReactivationCount += 1;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                userId = user.Id,
                username = user.Username,
                newCoins = user.Coins,
                bonusCoins = user.BonusCoins,
                reactivatedAt = user.LastReactivatedAt.Value.ToString("yyyy-MM-dd HH:mm"),
                message = $"Se acreditaron con éxito +{amount} monedas de reactivación a @{user.Username}."
            });
        }

        [HttpGet("expenses")]
        public async Task<IActionResult> GetExpenses()
        {
            var expenses = await _context.PlatformExpenses
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            var totalBsExpenses = expenses.Where(e => e.Currency == "BS").Sum(e => e.Amount);
            var totalUsdtExpenses = expenses.Where(e => e.Currency == "USDT" && !e.Category.Contains("Venta USDT") && !e.Category.Contains("Venta de USDT")).Sum(e => e.Amount);
            var totalUsdtPurchased = expenses.Where(e => e.UsdtReceived != null && e.UsdtReceived > 0).Sum(e => e.UsdtReceived ?? 0);
            var totalUsdtSold = expenses.Where(e => (e.Category.Contains("Venta USDT") || e.Category.Contains("Venta de USDT")) && e.Currency == "USDT").Sum(e => e.Amount);
            var totalBsFromUsdtSold = expenses.Where(e => e.Category.Contains("Venta USDT") || e.Category.Contains("Venta de USDT")).Sum(e => e.BsReceived ?? (e.ExchangeRate != null ? e.Amount * e.ExchangeRate.Value : 0m));

            var totalBsRecharges = await _context.PaymentRecharges.Where(r => r.Status == "APROBADO").SumAsync(r => r.AmountBs);
            var totalBsWithdrawals = await _context.PaymentWithdrawals.Where(w => w.Status == "PAGADO").SumAsync(w => w.AmountBs);

            return Ok(new
            {
                expenses,
                totalBsExpenses,
                totalUsdtExpenses,
                totalUsdtPurchased,
                totalUsdtSold,
                totalBsFromUsdtSold,
                cajaNetaBs = totalBsRecharges - totalBsWithdrawals - totalBsExpenses + totalBsFromUsdtSold,
                cajaNetaUsdt = totalUsdtPurchased - totalUsdtExpenses - totalUsdtSold
            });
        }

        [HttpPost("expenses")]
        public async Task<IActionResult> CreateExpense([FromBody] CreatePlatformExpenseDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Description))
            {
                return BadRequest(new { message = "La descripción del gasto es obligatoria." });
            }

            if (dto.Amount <= 0)
            {
                return BadRequest(new { message = "El monto debe ser mayor a cero." });
            }

            var currency = (dto.Currency ?? "BS").Trim().ToUpper();
            if (currency != "BS" && currency != "USDT")
            {
                currency = "BS";
            }

            decimal? usdtReceived = dto.UsdtReceived;
            decimal? bsReceived = dto.BsReceived;
            decimal? exchangeRate = dto.ExchangeRate;

            if (dto.Category != null && (dto.Category.Contains("Compra USDT") || dto.Category.Contains("Compra de USDT")))
            {
                currency = "BS";
                if (usdtReceived == null || usdtReceived <= 0)
                {
                    return BadRequest(new { message = "Para Compra de USDT debe indicar la cantidad de USDT recibidos." });
                }
                if (exchangeRate == null || exchangeRate <= 0)
                {
                    exchangeRate = Math.Round(dto.Amount / usdtReceived.Value, 4);
                }
            }
            else if (dto.Category != null && (dto.Category.Contains("Venta USDT") || dto.Category.Contains("Venta de USDT")))
            {
                currency = "USDT";
                if (bsReceived == null || bsReceived <= 0)
                {
                    return BadRequest(new { message = "Para Venta de USDT debe indicar la cantidad de Bolívares recibidos en cuenta." });
                }
                if (exchangeRate == null || exchangeRate <= 0)
                {
                    exchangeRate = Math.Round(bsReceived.Value / dto.Amount, 4);
                }
            }

            var expense = new PlatformExpense
            {
                Description = dto.Description.Trim(),
                Category = string.IsNullOrWhiteSpace(dto.Category) ? "Varios" : dto.Category.Trim(),
                Currency = currency,
                Amount = dto.Amount,
                UsdtReceived = usdtReceived,
                BsReceived = bsReceived,
                ExchangeRate = exchangeRate,
                CreatedAt = VenezuelaTime.Now,
                CreatedBy = "Guardian",
                Notes = dto.Notes?.Trim()
            };

            _context.PlatformExpenses.Add(expense);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Gasto registrado exitosamente en la caja.",
                expense
            });
        }

        [HttpDelete("expenses/{id}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var expense = await _context.PlatformExpenses.FindAsync(id);
            if (expense == null)
            {
                return NotFound(new { message = "Gasto no encontrado." });
            }

            _context.PlatformExpenses.Remove(expense);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Gasto eliminado y balance de caja recalculado." });
        }

        [HttpPost("force-refresh-clients")]
        public async Task<IActionResult> ForceRefreshClients([FromBody] ForceRefreshDto? dto)
        {
            var timestamp = DateTime.UtcNow;
            var reason = dto?.Reason ?? "Actualización oficial del sistema aplicada por el Administrador";
            await _hubContext.Clients.All.SendAsync("ForceClientRefresh", new
            {
                timestamp = timestamp.ToString("o"),
                reason = reason
            });
            return Ok(new { success = true, message = "Señal de actualización remota enviada a todos los clientes y APKs conectadas." });
        }
    }

    public class ForceRefreshDto
    {
        public string? Reason { get; set; }
    }

    public class CreatePlatformExpenseDto
    {
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "Varios";
        public string Currency { get; set; } = "BS";
        public decimal Amount { get; set; }
        public decimal? UsdtReceived { get; set; }
        public decimal? BsReceived { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string? Notes { get; set; }
    }

    public class CreditReactivationDto
    {
        public int UserId { get; set; }
        public int BonusCoins { get; set; } = 300;
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
        public bool Confiscate { get; set; } = false;
        public bool ForceCancelPaid { get; set; } = false;
    }

    public class AdjustCoinsDto
    {
        public int Amount { get; set; }
        public string? Reason { get; set; }
    }

    public class ApproveWithdrawalDto
    {
        public string Reference { get; set; } = string.Empty;
        public bool ForceApprove { get; set; } = false;
    }

    public class ReassignRechargeDto
    {
        public int NewUserId { get; set; }
    }

    public class ResetPendingDto
    {
        public bool RevertCoins { get; set; } = false;
    }

    public class CreateManualWithdrawalDto
    {
        public int UserId { get; set; }
        public int AmountCoins { get; set; }
        public decimal AmountBs { get; set; }
        public string? BankName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? IdCard { get; set; }
        public string? Reference { get; set; }
        public bool DeductCoins { get; set; } = true;
    }
}
