namespace MinhaGaragem.Application.AI;

/// <summary>In-process daily limits; use provider project quotas for a durable billing ceiling.</summary>
public sealed class MaintenanceAiGate(MaintenanceAiOptions options, TimeProvider clock)
{
    private readonly object sync = new();
    private DateOnly day;
    private int daily,
        active;
    private readonly Dictionary<Guid, int> users = [];

    public IDisposable Enter(Guid user)
    {
        lock (sync)
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            if (today != day)
            {
                day = today;
                daily = 0;
                users.Clear();
            }
            users.TryGetValue(user, out var count);
            if (
                active >= 2
                || daily >= options.RequestsPerDay
                || count >= options.RequestsPerUserPerDay
            )
                throw new MaintenanceAiException(AiFailure.RateLimited);
            active++;
            daily++;
            users[user] = count + 1;
            return new Lease(this);
        }
    }

    private sealed class Lease(MaintenanceAiGate owner) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
                lock (owner.sync)
                    owner.active--;
        }
    }
}
