namespace DEFRA.BankHolidays.Services
{
    /// <summary>Allows only one data refresh at a time within this instance.</summary>
    public sealed class RefreshGate
    {
        private readonly SemaphoreSlim semaphore = new(1, 1);

        /// <summary>Returns a lease to dispose when the refresh finishes, or null if a refresh is already running.</summary>
        public IDisposable TryAcquire()
        {
            return semaphore.Wait(0) ? new Lease(semaphore) : null;
        }

        private sealed class Lease : IDisposable
        {
            private SemaphoreSlim semaphore;

            public Lease(SemaphoreSlim semaphore)
            {
                this.semaphore = semaphore;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref semaphore, null)?.Release();
            }
        }
    }
}
