using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PericonAPI.Models
{
    public class PlatformExpense
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Description { get; set; } = string.Empty;

        // Categoría: "Servidor / Hosting", "Desarrollo / Mejoras", "Compra USDT", "Venta USDT", "Marketing / Publicidad", "Administrativo / Nómina", "Comisiones Bancarias", "Varios"
        [Required]
        [MaxLength(80)]
        public string Category { get; set; } = "Varios";

        // Moneda del movimiento: "BS" o "USDT"
        [Required]
        [MaxLength(10)]
        public string Currency { get; set; } = "BS";

        // Monto gastado o vendido en la moneda correspondiente (Bs o USDT)
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        // Si es Compra de USDT: cantidad de USDT que ingresan a la caja de USDT
        [Column(TypeName = "decimal(18,2)")]
        public decimal? UsdtReceived { get; set; }

        // Si es Venta de USDT: cantidad de Bolívares que ingresan a la caja bancaria
        [Column(TypeName = "decimal(18,2)")]
        public decimal? BsReceived { get; set; }

        // Tasa de cambio implícita o registrada (Bs por USDT)
        [Column(TypeName = "decimal(18,4)")]
        public decimal? ExchangeRate { get; set; }

        public DateTime CreatedAt { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
