using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace PericonAPI.Classes
{
    /// <summary>
    /// Candado de concurrencia por usuario (Anti-TOCTOU / Anti-Doble Gasto) y limitador de intentos por clave/IP.
    /// Garantiza que peticiones simultáneas en el mismo milisegundo para un mismo UserId se ejecuten estrictamente en serie.
    /// </summary>
    public static class UserConcurrencyLock
    {
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _userLocks = new();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();
        private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowResetUtc)> _rateLimits = new();

        public static async Task<IDisposable> AcquireAsync(int userId, int timeoutMs = 15000)
        {
            int key = userId > 0 ? userId : 0;
            var sem = _userLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            bool acquired = await sem.WaitAsync(timeoutMs);
            return new Releaser(sem, acquired);
        }

        public static async Task<IDisposable> AcquireKeyAsync(string lockKey, int timeoutMs = 15000)
        {
            string normalized = (lockKey ?? "global").Trim().ToLowerInvariant();
            var sem = _keyLocks.GetOrAdd(normalized, _ => new SemaphoreSlim(1, 1));
            bool acquired = await sem.WaitAsync(timeoutMs);
            return new Releaser(sem, acquired);
        }

        public static bool CheckRateLimit(string bucketKey, int maxAttempts, TimeSpan window)
        {
            string key = (bucketKey ?? "anon").Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;

            var updated = _rateLimits.AddOrUpdate(
                key,
                _ => (1, now.Add(window)),
                (_, existing) =>
                {
                    if (now >= existing.WindowResetUtc)
                    {
                        return (1, now.Add(window));
                    }
                    return (existing.Count + 1, existing.WindowResetUtc);
                });

            return updated.Count <= maxAttempts;
        }

        private sealed class Releaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private bool _disposed;
            private readonly bool _acquired;

            public Releaser(SemaphoreSlim semaphore, bool acquired)
            {
                _semaphore = semaphore;
                _acquired = acquired;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    if (_acquired)
                    {
                        try { _semaphore.Release(); } catch { }
                    }
                }
            }
        }
    }
}
