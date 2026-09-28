using System;

namespace PericonAPI.Classes
{
    public static class VenezuelaTime
    {
        // Zona horaria de Venezuela: UTC-4 (sin cambio de horario de verano)
        public static readonly TimeSpan UtcOffset = TimeSpan.FromHours(-4);

        public static DateTime Now
        {
            get
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("Venezuela Standard Time");
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                }
                catch
                {
                    try
                    {
                        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Caracas");
                        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                    }
                    catch
                    {
                        return DateTime.UtcNow.AddHours(-4);
                    }
                }
            }
        }

        // Horario estipulado: 6:00 AM a 9:30 PM (21:30)
        public static readonly TimeSpan OpeningTime = new TimeSpan(6, 0, 0);   // 06:00:00 AM
        public static readonly TimeSpan ClosingTime = new TimeSpan(21, 30, 0);  // 09:30:00 PM

        public static bool IsWithinOperatingHours()
        {
            var nowTime = Now.TimeOfDay;
            return nowTime >= OpeningTime && nowTime <= ClosingTime;
        }

        public static string OperatingHoursMessage =>
            "Por políticas de la plataforma, el horario para recargas y retiros es de 6:00 AM a 9:30 PM (Hora de Venezuela). En este momento el servicio se encuentra fuera de horario. Por favor, intenta de nuevo dentro del horario estipulado.";
    }
}
