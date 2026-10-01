using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PericonAPI.Classes;
using PericonAPI.Data;
using PericonAPI.Models;

namespace PericonAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notificationService;
        private readonly ITesoroPagosService _tesoroPagosService;

        public PaymentController(AppDbContext context, IWebHostEnvironment env, INotificationService notificationService, ITesoroPagosService tesoroPagosService)
        {
            _context = context;
            _env = env;
            _notificationService = notificationService;
            _tesoroPagosService = tesoroPagosService;
        }

        public const decimal MIN_RECHARGE_BS = 800m;

        [HttpPost("report")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ReportPayment([FromForm] PaymentReportFormDto dto)
        {
            if (!VenezuelaTime.IsWithinOperatingHours())
            {
                return BadRequest(new
                {
                    message = VenezuelaTime.OperatingHoursMessage,
                    isOutsideHours = true,
                    currentTimeVenezuela = VenezuelaTime.Now.ToString("hh:mm tt"),
                    operatingHours = "6:00 AM a 9:30 PM (Hora de Venezuela)"
                });
            }

            if (dto.AmountBs <= 0)
            {
                return BadRequest(new { message = "El monto de recarga debe ser un número positivo mayor a 0." });
            }

            if (dto.AmountBs < MIN_RECHARGE_BS)
            {
                return BadRequest(new { message = $"El monto mínimo de recarga es de {MIN_RECHARGE_BS:N0} Bs. ({MIN_RECHARGE_BS:N0} monedas)." });
            }

            var cleanRef = (dto.Reference ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cleanRef) || cleanRef.Length < 4)
            {
                return BadRequest(new { message = "Debes ingresar el número de referencia del pago (al menos 4 dígitos)." });
            }

            var duplicateRef = await _context.PaymentRecharges
                .FirstOrDefaultAsync(r => r.Reference == cleanRef && r.Status != "RECHAZADO");

            if (duplicateRef != null)
            {
                if (duplicateRef.Status == "APROBADO")
                {
                    return BadRequest(new { message = "Esta referencia de pago móvil ya fue aprobada y acreditada anteriormente. No se puede reutilizar." });
                }
                else
                {
                    return BadRequest(new { message = "Ya tienes una solicitud de recarga pendiente con esta misma referencia. Por favor espera su revisión por el administrador." });
                }
            }

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            if (!user.IsActive)
            {
                return BadRequest(new { message = "Esta cuenta de usuario se encuentra suspendida o inactiva." });
            }

            string receiptUrl = "";
            string? receiptBase64 = null;

            if (dto.ReceiptImage != null && dto.ReceiptImage.Length > 0)
            {
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        await dto.ReceiptImage.CopyToAsync(ms);
                        var bytes = ms.ToArray();
                        var mime = dto.ReceiptImage.ContentType ?? "image/jpeg";
                        receiptBase64 = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                    }
                }
                catch { }

                try
                {
                    var uploadsFolder = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "receipts");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var ext = Path.GetExtension(dto.ReceiptImage.FileName);
                    if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                    var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await dto.ReceiptImage.CopyToAsync(stream);
                    }

                    receiptUrl = $"/uploads/receipts/{uniqueFileName}";
                }
                catch { }
            }

            var recharge = new PaymentRecharge
            {
                UserId = dto.UserId,
                AmountBs = dto.AmountBs,
                CoinsAmount = (int)Math.Floor(dto.AmountBs), // 1 Bs = 1 moneda
                Reference = dto.Reference.Trim(),
                ReceiptImageUrl = receiptUrl,
                ReceiptBase64 = receiptBase64,
                Status = "PENDIENTE",
                CreatedAt = VenezuelaTime.Now
            };

            _context.PaymentRecharges.Add(recharge);
            await _context.SaveChangesAsync();

            // Asignar ruta de visualización directa y garantizada por la API
            recharge.ReceiptImageUrl = $"/api/payment/receipt/{recharge.Id}";
            await _context.SaveChangesAsync();

            // Intentar validación automática inmediata con Banco del Tesoro (Caja 03)
            bool isAutoApproved = false;
            try
            {
                var (valSuccess, valApproved, bankMsg) = await _tesoroPagosService.ValidatePaymentAsync(
                    recharge.AmountBs,
                    dto.OriginBank,
                    dto.OriginPhone,
                    recharge.Reference
                );

                if (valApproved)
                {
                    isAutoApproved = true;
                    recharge.Status = "APROBADO";
                    recharge.ProcessedAt = VenezuelaTime.Now;
                    recharge.AdminNotes = "Aprobado automáticamente por integración Tesoro Pagos (Caja 03)";
                    user.Coins += recharge.CoinsAmount;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Tesoro Auto-Validation Error] {ex.Message}");
            }

            _ = _notificationService.SendRechargeNotificationAsync(
                user.Username,
                recharge.AmountBs,
                recharge.CoinsAmount,
                recharge.Reference,
                recharge.ReceiptImageUrl
            );

            if (isAutoApproved)
            {
                return Ok(new
                {
                    id = recharge.Id,
                    amountBs = recharge.AmountBs,
                    coinsAmount = recharge.CoinsAmount,
                    reference = recharge.Reference,
                    status = recharge.Status,
                    isAutoApproved = true,
                    receiptUrl = recharge.ReceiptImageUrl,
                    createdAt = recharge.CreatedAt,
                    userNewCoins = user.Coins,
                    message = $"¡Pago verificado automáticamente por Banco del Tesoro! Se te han acreditado {recharge.CoinsAmount} monedas al instante."
                });
            }

            return Ok(new
            {
                id = recharge.Id,
                amountBs = recharge.AmountBs,
                coinsAmount = recharge.CoinsAmount,
                reference = recharge.Reference,
                status = recharge.Status,
                isAutoApproved = false,
                receiptUrl = recharge.ReceiptImageUrl,
                createdAt = recharge.CreatedAt,
                message = $"¡Comprobante de {recharge.CoinsAmount} monedas recibido! Tu pago está pendiente de aprobación por el administrador."
            });
        }

        [HttpPost("report-json")]
        public async Task<IActionResult> ReportPaymentJson([FromBody] PaymentReportJsonDto dto)
        {
            if (!VenezuelaTime.IsWithinOperatingHours())
            {
                return BadRequest(new
                {
                    message = VenezuelaTime.OperatingHoursMessage,
                    isOutsideHours = true,
                    currentTimeVenezuela = VenezuelaTime.Now.ToString("hh:mm tt"),
                    operatingHours = "6:00 AM a 9:30 PM (Hora de Venezuela)"
                });
            }

            if (dto.AmountBs <= 0)
            {
                return BadRequest(new { message = "El monto de recarga debe ser un número positivo mayor a 0." });
            }

            if (dto.AmountBs < MIN_RECHARGE_BS)
            {
                return BadRequest(new { message = $"El monto mínimo de recarga es de {MIN_RECHARGE_BS:N0} Bs. ({MIN_RECHARGE_BS:N0} monedas)." });
            }

            var cleanRef = (dto.Reference ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cleanRef) || cleanRef.Length < 4)
            {
                return BadRequest(new { message = "Debes ingresar el número de referencia del pago (al menos 4 dígitos)." });
            }

            var duplicateRef = await _context.PaymentRecharges
                .FirstOrDefaultAsync(r => r.Reference == cleanRef && r.Status != "RECHAZADO");

            if (duplicateRef != null)
            {
                if (duplicateRef.Status == "APROBADO")
                {
                    return BadRequest(new { message = "Esta referencia de pago móvil ya fue aprobada y acreditada anteriormente. No se puede reutilizar." });
                }
                else
                {
                    return BadRequest(new { message = "Ya tienes una solicitud de recarga pendiente con esta misma referencia. Por favor espera su revisión por el administrador." });
                }
            }

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            if (!user.IsActive)
            {
                return BadRequest(new { message = "Esta cuenta de usuario se encuentra suspendida o inactiva." });
            }

            string receiptUrl = "";
            string? receiptBase64 = null;

            if (!string.IsNullOrWhiteSpace(dto.Base64Image))
            {
                receiptBase64 = dto.Base64Image.Trim();
                try
                {
                    var uploadsFolder = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "receipts");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var rawBase64 = receiptBase64;
                    var ext = ".jpg";
                    if (rawBase64.Contains(";base64,"))
                    {
                        var parts = rawBase64.Split(";base64,");
                        if (parts[0].Contains("png")) ext = ".png";
                        else if (parts[0].Contains("webp")) ext = ".webp";
                        rawBase64 = parts[1];
                    }

                    var imageBytes = Convert.FromBase64String(rawBase64);
                    var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                    receiptUrl = $"/uploads/receipts/{uniqueFileName}";
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error saving backup file to disk: " + ex.Message);
                }
            }

            var recharge = new PaymentRecharge
            {
                UserId = dto.UserId,
                AmountBs = dto.AmountBs,
                CoinsAmount = (int)Math.Floor(dto.AmountBs),
                Reference = dto.Reference.Trim(),
                ReceiptImageUrl = receiptUrl,
                ReceiptBase64 = receiptBase64,
                Status = "PENDIENTE",
                CreatedAt = VenezuelaTime.Now
            };

            _context.PaymentRecharges.Add(recharge);
            await _context.SaveChangesAsync();

            // Asignar ruta de visualización directa garantizada por la API
            recharge.ReceiptImageUrl = $"/api/payment/receipt/{recharge.Id}";
            await _context.SaveChangesAsync();

            // Intentar validación automática inmediata con Banco del Tesoro (Caja 03)
            bool isAutoApproved = false;
            try
            {
                var (valSuccess, valApproved, bankMsg) = await _tesoroPagosService.ValidatePaymentAsync(
                    recharge.AmountBs,
                    dto.OriginBank,
                    dto.OriginPhone,
                    recharge.Reference
                );

                if (valApproved)
                {
                    isAutoApproved = true;
                    recharge.Status = "APROBADO";
                    recharge.ProcessedAt = VenezuelaTime.Now;
                    recharge.AdminNotes = "Aprobado automáticamente por integración Tesoro Pagos (Caja 03)";
                    user.Coins += recharge.CoinsAmount;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Tesoro Auto-Validation Error JSON] {ex.Message}");
            }

            _ = _notificationService.SendRechargeNotificationAsync(
                user.Username,
                recharge.AmountBs,
                recharge.CoinsAmount,
                recharge.Reference,
                recharge.ReceiptImageUrl
            );

            if (isAutoApproved)
            {
                return Ok(new
                {
                    id = recharge.Id,
                    amountBs = recharge.AmountBs,
                    coinsAmount = recharge.CoinsAmount,
                    reference = recharge.Reference,
                    status = recharge.Status,
                    isAutoApproved = true,
                    receiptUrl = recharge.ReceiptImageUrl,
                    createdAt = recharge.CreatedAt,
                    userNewCoins = user.Coins,
                    message = $"¡Pago verificado automáticamente por Banco del Tesoro! Se te han acreditado {recharge.CoinsAmount} monedas al instante."
                });
            }

            return Ok(new
            {
                id = recharge.Id,
                amountBs = recharge.AmountBs,
                coinsAmount = recharge.CoinsAmount,
                reference = recharge.Reference,
                status = recharge.Status,
                isAutoApproved = false,
                receiptUrl = recharge.ReceiptImageUrl,
                createdAt = recharge.CreatedAt,
                message = $"¡Comprobante de {recharge.CoinsAmount} monedas recibido! Tu pago está pendiente de aprobación por el administrador."
            });
        }

        [HttpGet("receipt/{id}")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> GetReceiptImage(int id)
        {
            var recharge = await _context.PaymentRecharges.FindAsync(id);
            if (recharge == null)
            {
                return NotFound(new { message = "Comprobante no encontrado." });
            }

            // 1. Servir desde Base64 guardado en base de datos (100% persistente y a prueba de reinicios en Railway)
            if (!string.IsNullOrWhiteSpace(recharge.ReceiptBase64))
            {
                try
                {
                    var raw = recharge.ReceiptBase64.Trim();
                    var contentType = "image/jpeg";
                    if (raw.StartsWith("data:"))
                    {
                        var semi = raw.IndexOf(';');
                        if (semi > 5)
                        {
                            contentType = raw.Substring(5, semi - 5);
                        }
                        var comma = raw.IndexOf(',');
                        if (comma >= 0)
                        {
                            raw = raw.Substring(comma + 1);
                        }
                    }
                    var bytes = Convert.FromBase64String(raw);
                    return File(bytes, contentType);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Receipt Error] recharge #{id}: {ex.Message}");
                }
            }

            // 2. Si es URL externa completa
            if (!string.IsNullOrWhiteSpace(recharge.ReceiptImageUrl) &&
                (recharge.ReceiptImageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 recharge.ReceiptImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                return Redirect(recharge.ReceiptImageUrl);
            }

            // 3. Si hay archivo en disco físico (respaldo local)
            if (!string.IsNullOrWhiteSpace(recharge.ReceiptImageUrl))
            {
                var clean = recharge.ReceiptImageUrl.TrimStart('/', '\\');
                var filePath = Path.Combine(_env.ContentRootPath, "wwwroot", clean);
                if (System.IO.File.Exists(filePath))
                {
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    var mime = ext == ".png" ? "image/png" : ext == ".webp" ? "image/webp" : "image/jpeg";
                    var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
                    return File(bytes, mime);
                }
            }

            return NotFound(new { message = "La imagen del comprobante no está disponible." });
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserRecharges(int userId)
        {
            var list = await _context.PaymentRecharges
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    id = r.Id,
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

        public const int MIN_WITHDRAWAL_COINS = 1500;

        [HttpPost("withdraw")]
        public async Task<IActionResult> RequestWithdrawal([FromBody] PaymentWithdrawDto dto)
        {
            if (!VenezuelaTime.IsWithinOperatingHours())
            {
                return BadRequest(new
                {
                    message = VenezuelaTime.OperatingHoursMessage,
                    isOutsideHours = true,
                    currentTimeVenezuela = VenezuelaTime.Now.ToString("hh:mm tt"),
                    operatingHours = "6:00 AM a 9:30 PM (Hora de Venezuela)"
                });
            }

            if (dto.CoinsAmount < MIN_WITHDRAWAL_COINS)
            {
                return BadRequest(new { message = $"El monto mínimo de retiro es de {MIN_WITHDRAWAL_COINS:N0} monedas." });
            }

            if (string.IsNullOrWhiteSpace(dto.BankName))
            {
                return BadRequest(new { message = "Debes seleccionar el banco receptor de tu Pago Móvil." });
            }

            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                return BadRequest(new { message = "Debes ingresar tu número de teléfono de Pago Móvil." });
            }

            if (string.IsNullOrWhiteSpace(dto.IdCard))
            {
                return BadRequest(new { message = "Debes ingresar tu número de cédula de identidad." });
            }

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            if (user.Coins < dto.CoinsAmount)
            {
                return BadRequest(new { message = $"Saldo insuficiente. Tienes {user.Coins} monedas y deseas retirar {dto.CoinsAmount}." });
            }

            // REGLA FINANCIERA 1: Diferenciación de Monedas de Cortesía vs Reales
            int retirableCoins = user.GetRetirableCoins();
            if (dto.CoinsAmount > retirableCoins && !user.IsAdmin && user.Username.ToLower() != "guardian")
            {
                return BadRequest(new
                {
                    message = $"Saldo retirable insuficiente. De tus {user.Coins:N0} monedas totales, {user.BonusCoins:N0} son de cortesía exclusivas para jugar. Tu saldo retirable disponible actualmente es de {retirableCoins:N0} monedas."
                });
            }

            // REGLA FINANCIERA 2: Cero retiros de moneda regalada.
            // Para poder solicitar un retiro, el usuario debe haber realizado al menos una recarga real de saldo que haya sido APROBADA.
            bool hasApprovedDeposit = await _context.PaymentRecharges
                .AnyAsync(r => r.UserId == dto.UserId && r.Status == "APROBADO");

            if (!hasApprovedDeposit && !user.IsAdmin && user.Username.ToLower() != "guardian")
            {
                return BadRequest(new
                {
                    message = "Por política de seguridad y protección financiera, para solicitar un retiro debes haber realizado al menos una recarga de saldo aprobada en la plataforma. Las monedas de bienvenida, bonos o cupones son exclusivas para jugar y no son retirables directamente sin un depósito previo."
                });
            }

            // REGLA FINANCIERA 3: Prevención de fraude multicuenta (Unicidad de Pago Móvil y Cédula)
            bool phoneUsedByOther = await _context.PaymentWithdrawals
                .AnyAsync(w => w.UserId != dto.UserId && w.PhoneNumber == dto.PhoneNumber && w.Status != "RECHAZADO");
            bool idUsedByOther = await _context.PaymentWithdrawals
                .AnyAsync(w => w.UserId != dto.UserId && w.IdCard == dto.IdCard && w.Status != "RECHAZADO");

            if ((phoneUsedByOther || idUsedByOther) && !user.IsAdmin && user.Username.ToLower() != "guardian")
            {
                return BadRequest(new
                {
                    message = "Por políticas de seguridad financiera y prevención de fraude multicuenta, este número de teléfono de Pago Móvil o cédula de identidad ya está registrado en otra cuenta para retiros. Cada cuenta debe corresponder a un titular bancario único."
                });
            }

            // Descontar monedas de inmediato para evitar doble gasto
            user.Coins -= dto.CoinsAmount;

            var withdrawal = new PaymentWithdrawal
            {
                UserId = dto.UserId,
                CoinsAmount = dto.CoinsAmount,
                AmountBs = (decimal)dto.CoinsAmount, // 1 Moneda = 1 Bs
                BankName = dto.BankName.Trim(),
                PhoneNumber = dto.PhoneNumber.Trim(),
                IdCard = dto.IdCard.Trim(),
                Status = "PENDIENTE",
                CreatedAt = VenezuelaTime.Now
            };

            _context.PaymentWithdrawals.Add(withdrawal);
            await _context.SaveChangesAsync();

            _ = _notificationService.SendWithdrawalNotificationAsync(
                user.Username,
                withdrawal.AmountBs,
                withdrawal.CoinsAmount,
                withdrawal.BankName,
                withdrawal.PhoneNumber,
                withdrawal.IdCard
            );

            return Ok(new
            {
                id = withdrawal.Id,
                coinsAmount = withdrawal.CoinsAmount,
                amountBs = withdrawal.AmountBs,
                bankName = withdrawal.BankName,
                phoneNumber = withdrawal.PhoneNumber,
                idCard = withdrawal.IdCard,
                status = withdrawal.Status,
                userNewCoins = user.Coins,
                createdAt = withdrawal.CreatedAt,
                message = $"¡Solicitud de retiro de {withdrawal.AmountBs} Bs. registrada con éxito! Tu saldo actual es de {user.Coins} monedas. El administrador realizará la transferencia a tu Pago Móvil."
            });
        }

        [HttpGet("user/{userId}/withdrawals")]
        public async Task<IActionResult> GetUserWithdrawals(int userId)
        {
            var list = await _context.PaymentWithdrawals
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new
                {
                    id = w.Id,
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

        [HttpGet("operating-hours")]
        public IActionResult GetOperatingHours()
        {
            var now = VenezuelaTime.Now;
            bool isOpen = VenezuelaTime.IsWithinOperatingHours();

            return Ok(new
            {
                isOpen,
                currentTimeVenezuela = now.ToString("yyyy-MM-dd HH:mm:ss"),
                currentTimeFormatted = now.ToString("hh:mm tt"),
                openTime = "06:00 AM",
                closeTime = "09:30 PM",
                operatingHours = "6:00 AM a 9:30 PM (Hora de Venezuela)",
                message = isOpen
                    ? "Servicio de recargas y retiros disponible (6:00 AM a 9:30 PM)."
                    : VenezuelaTime.OperatingHoursMessage
            });
        }
    }

    public class PaymentReportFormDto
    {
        public int UserId { get; set; }
        public decimal AmountBs { get; set; }
        public string Reference { get; set; } = string.Empty;
        public IFormFile? ReceiptImage { get; set; }
        public string? OriginBank { get; set; }
        public string? OriginPhone { get; set; }
    }

    public class PaymentReportJsonDto
    {
        public int UserId { get; set; }
        public decimal AmountBs { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string? Base64Image { get; set; }
        public string? OriginBank { get; set; }
        public string? OriginPhone { get; set; }
    }

    public class PaymentWithdrawDto
    {
        public int UserId { get; set; }
        public int CoinsAmount { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string IdCard { get; set; } = string.Empty;
    }
}
