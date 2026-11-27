using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Tests.Integration;

internal sealed class ConcurrentAuditSaveInterceptor
    : SaveChangesInterceptor
{
    private readonly TaskCompletionSource<bool> _gate =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _arrivals;

    public int Arrivals =>
        Volatile.Read(ref _arrivals);

    public override async ValueTask<InterceptionResult<int>>
        SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
    {
        bool isAuditInsert =
            eventData.Context?.ChangeTracker
                .Entries<AuditRun>()
                .Any(x => x.State == EntityState.Added)
            == true;

        if (!isAuditInsert)
        {
            return result;
        }

        if (Interlocked.Increment(ref _arrivals) == 2)
        {
            _gate.TrySetResult(true);
        }

        await _gate.Task.WaitAsync(
            TimeSpan.FromSeconds(15),
            cancellationToken);

        return result;
    }
}
