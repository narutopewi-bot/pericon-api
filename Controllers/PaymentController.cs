using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public PaymentController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpPost("report")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ReportPayment([FromForm] PaymentReportFormDto dto)
        {
            if (dto.AmountBs <= 0)
            {
                return BadRequest(new { message = "El monto a transferir debe ser mayor a 0 Bs." });
            }

            if (string.IsNullOrWhiteSpace(dto.Reference))
            {
                return BadRequest(new { message = "Debes ingresar el número de referencia del pago." });
            }

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            string receiptUrl = "";

            if (dto.ReceiptImage != null && dto.ReceiptImage.Length > 0)
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

            var recharge = new PaymentRecharge
            {
                UserId = dto.UserId,
                AmountBs = dto.AmountBs,
                CoinsAmount = (int)Math.Floor(dto.AmountBs), // 1 Bs = 1 moneda
                Reference = dto.Reference.Trim(),
                ReceiptImageUrl = receiptUrl,
                Status = "PENDIENTE",
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentRecharges.Add(recharge);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = recharge.Id,
                amountBs = recharge.AmountBs,
                coinsAmount = recharge.CoinsAmount,
                reference = recharge.Reference,
                status = recharge.Status,
                receiptUrl = recharge.ReceiptImageUrl,
                createdAt = recharge.CreatedAt,
                message = $"¡Comprobante de {recharge.CoinsAmount} monedas recibido! Tu pago está pendiente de aprobación por el administrador."
            });
        }

        [HttpPost("report-json")]
        public async Task<IActionResult> ReportPaymentJson([FromBody] PaymentReportJsonDto dto)
        {
            if (dto.AmountBs <= 0)
            {
                return BadRequest(new { message = "El monto a transferir debe ser mayor a 0 Bs." });
            }

            if (string.IsNullOrWhiteSpace(dto.Reference))
            {
                return BadRequest(new { message = "Debes ingresar el número de referencia del pago." });
            }

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado." });
            }

            string receiptUrl = "";

            if (!string.IsNullOrWhiteSpace(dto.Base64Image))
            {
                try
                {
                    var uploadsFolder = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "receipts");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var rawBase64 = dto.Base64Image;
                    var ext = ".jpg";
                    if (rawBase64.Contains(";base64,"))
                    {
                        var parts = rawBase64.Split(";base64,");
                        if (parts[0].Contains("png")) ext = ".png";
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
                    Console.WriteLine("Error saving base64 image: " + ex.Message);
                }
            }

            var recharge = new PaymentRecharge
            {
                UserId = dto.UserId,
                AmountBs = dto.AmountBs,
                CoinsAmount = (int)Math.Floor(dto.AmountBs),
                Reference = dto.Reference.Trim(),
                ReceiptImageUrl = receiptUrl,
                Status = "PENDIENTE",
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentRecharges.Add(recharge);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = recharge.Id,
                amountBs = recharge.AmountBs,
                coinsAmount = recharge.CoinsAmount,
                reference = recharge.Reference,
                status = recharge.Status,
                receiptUrl = recharge.ReceiptImageUrl,
                createdAt = recharge.CreatedAt,
                message = $"¡Comprobante de {recharge.CoinsAmount} monedas recibido! Tu pago está pendiente de aprobación por el administrador."
            });
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
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentWithdrawals.Add(withdrawal);
            await _context.SaveChangesAsync();

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
    }

    public class PaymentReportFormDto
    {
        public int UserId { get; set; }
        public decimal AmountBs { get; set; }
        public string Reference { get; set; } = string.Empty;
        public IFormFile? ReceiptImage { get; set; }
    }

    public class PaymentReportJsonDto
    {
        public int UserId { get; set; }
        public decimal AmountBs { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string? Base64Image { get; set; }
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
