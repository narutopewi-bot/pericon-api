using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PericonAPI.Data;
using PericonAPI.Models;

namespace PericonAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var totalUsers = await _context.Users.CountAsync();
            var pendingRecharges = await _context.PaymentRecharges.CountAsync(r => r.Status == "PENDIENTE");
            var approvedRecharges = await _context.PaymentRecharges
                .Where(r => r.Status == "APROBADO")
                .ToListAsync();

            var totalBsApproved = approvedRecharges.Sum(r => r.AmountBs);
            var totalCoinsApproved = approvedRecharges.Sum(r => r.CoinsAmount);

            var pendingWithdrawals = await _context.PaymentWithdrawals.CountAsync(w => w.Status == "PENDIENTE");
            var paidWithdrawals = await _context.PaymentWithdrawals
                .Where(w => w.Status == "PAGADO")
                .ToListAsync();

            var totalBsWithdrawn = paidWithdrawals.Sum(w => w.AmountBs);

            var matchRecords = await _context.MatchBetRecords.ToListAsync();
            var totalHouseCommissions = matchRecords.Sum(m => m.HouseCommission);
            var totalMatchesFinished = matchRecords.Count;
            var totalCoinsWagered = matchRecords.Sum(m => m.TotalPot);

            return Ok(new
            {
                totalUsers,
                pendingRecharges,
                totalApprovedCount = approvedRecharges.Count,
                totalBsApproved,
                totalCoinsApproved,
                pendingWithdrawals,
                totalPaidWithdrawalsCount = paidWithdrawals.Count,
                totalBsWithdrawn,
                totalHouseCommissions,
                totalMatchesFinished,
                totalCoinsWagered
            });
        }

        [HttpGet("matches")]
        public async Task<IActionResult> GetMatches()
        {
            var matches = await _context.MatchBetRecords
                .OrderByDescending(m => m.CreatedAt)
                .Take(100)
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
                    createdAt = m.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync();

            return Ok(matches);
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
                    userCoins = r.User != null ? r.User.Coins : 0,
                    amountBs = r.AmountBs,
                    coinsAmount = r.CoinsAmount,
                    reference = r.Reference,
                    receiptImageUrl = r.ReceiptImageUrl,
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
            recharge.ProcessedAt = DateTime.UtcNow;

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
            recharge.ProcessedAt = DateTime.UtcNow;

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
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new
                {
                    id = u.Id,
                    username = u.Username,
                    email = u.Email,
                    coins = u.Coins,
                    wins = u.Wins,
                    losses = u.Losses,
                    totalMatches = u.Wins + u.Losses,
                    winRate = (u.Wins + u.Losses) > 0 ? Math.Round((double)u.Wins / (u.Wins + u.Losses) * 100, 1) : 0,
                    level = u.GetCalculatedLevel(),
                    createdAt = u.CreatedAt,
                    avatarUrl = u.AvatarUrl,
                    isActive = u.IsActive,
                    isAdmin = u.IsAdmin
                })
                .ToListAsync();

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
            withdrawal.ProcessedAt = DateTime.UtcNow;

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
            withdrawal.ProcessedAt = DateTime.UtcNow;

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
                token = "guardian_session_" + Guid.NewGuid().ToString("N"),
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

            var withdrawals = await _context.PaymentWithdrawals.ToListAsync();
            var totalBsWithdrawals = withdrawals.Where(w => w.Status == "PAGADO").Sum(w => w.AmountBs);
            var totalCoinsWithdrawals = withdrawals.Where(w => w.Status == "PAGADO").Sum(w => w.CoinsAmount);
            var pendingWithdrawalsCount = withdrawals.Count(w => w.Status == "PENDIENTE");

            var matches = await _context.MatchBetRecords.ToListAsync();
            var totalCommissions = matches.Sum(m => m.HouseCommission);
            var totalWagered = matches.Sum(m => m.TotalPot);
            var totalMatches = matches.Count;

            var userCoinsInCirculation = await _context.Users.SumAsync(u => u.Coins);

            return Ok(new
            {
                users = new { total = totalUsers, active = activeUsers, banned = bannedUsers },
                financial = new
                {
                    totalBsDeposited = totalBsRecharges,
                    totalBsPaid = totalBsWithdrawals,
                    netBsBalance = totalBsRecharges - totalBsWithdrawals,
                    totalCoinsCirculating = userCoinsInCirculation,
                    totalCommissionsCollected = totalCommissions,
                    totalMatchesPlayed = totalMatches,
                    totalCoinsWagered = totalWagered,
                    pendingRechargesCount,
                    pendingWithdrawalsCount
                }
            });
        }
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
