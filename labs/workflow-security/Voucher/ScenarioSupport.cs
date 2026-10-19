// Read-only observation helpers. They do not decide whether a result is secure.
using System.Diagnostics;

namespace VoucherLab;

/// <summary>An incomplete observation is inconclusive, never a secure result.</summary>
public sealed class ObservationHarnessException(string message) : Exception(message);

/// <summary>Raised inside a racing call when the other caller never reached the checkpoint.</summary>
public sealed class BrokenBarrierException() : Exception("the other caller did not reach the checkpoint within 2 seconds");

public sealed record RaceRequest(string Actor, Redemption? Result, string? Error);

public sealed record RaceObservation(
    IReadOnlyList<RaceRequest> Requests,
    int SuccessCount,
    IReadOnlyList<string> Errors,
    int CheckpointCalls,
    int EventCount,
    IReadOnlyList<RedemptionEvent> Events,
    VoucherSnapshot? Snapshot);

public sealed record StateObservation(Redemption Result, int EventCount, VoucherSnapshot? Snapshot);

public static class Scenario
{
    public const int Now = 10;
    public const int ExpiresAt = 100;

    private static readonly TimeSpan BarrierTimeout = TimeSpan.FromSeconds(2);

    public static TService ActiveService<TService>(Func<TService> create, string code = "V-ACTIVE")
        where TService : VoucherStore
    {
        var service = create();
        service.Issue(code, ExpiresAt);
        service.Activate(code);
        return service;
    }

    /// <summary>
    /// Two callers redeem ONE active voucher. Both wait at the checkpoint seam until the other
    /// arrives, so the vulnerable "check, wait, then commit" overlap happens on every run.
    /// The whole observation has a deadline: a call that never returns is reported as an
    /// inconclusive harness error, never as a result; so is an error while setting up or reading
    /// the observation. A thread that never returns keeps running until the process exits.
    /// </summary>
    public static RaceObservation ConcurrentObservation(Func<VoucherStore> create, int deadlineSeconds = 5)
    {
        VoucherStore service;
        try
        {
            service = ActiveService(create, "V-RACE");
        }
        catch (Exception error)
        {
            throw new ObservationHarnessException("setup_error: " + error.GetType().Name);
        }
        using var barrier = new Barrier(2);
        var checkpointCalls = 0;
        var broken = 0;

        void Checkpoint()
        {
            Interlocked.Increment(ref checkpointCalls);
            // Like a broken barrier: once one caller has timed out, the other fails at once
            // instead of waiting out a second timeout.
            if (Volatile.Read(ref broken) == 1 || !barrier.SignalAndWait(BarrierTimeout))
            {
                Volatile.Write(ref broken, 1);
                throw new BrokenBarrierException();
            }
        }

        string[] actors = ["alice", "bob"];
        var requests = new RaceRequest?[actors.Length];
        var threads = actors.Select((actor, i) => new Thread(() =>
        {
            try
            {
                requests[i] = new RaceRequest(actor, service.Redeem("V-RACE", actor, Now, Checkpoint), null);
            }
            catch (Exception error)
            {
                requests[i] = new RaceRequest(actor, null, error.GetType().Name);
            }
        })
        { IsBackground = true, Name = $"race-{actor}" }).ToArray();

        foreach (var thread in threads)
            thread.Start();
        var clock = Stopwatch.StartNew();
        foreach (var thread in threads)
        {
            var remaining = TimeSpan.FromSeconds(deadlineSeconds) - clock.Elapsed;
            if (remaining < TimeSpan.Zero || !thread.Join(remaining))
                // Do not read state here: a stuck caller may still hold the lock.
                throw new ObservationHarnessException("deadline_exceeded: race observation inconclusive");
        }

        var rows = requests.Select(r => r ?? throw new ObservationHarnessException("worker_exited_without_result")).ToList();
        IReadOnlyList<RedemptionEvent> events;
        VoucherSnapshot? snapshot;
        try
        {
            events = service.Events();
            snapshot = service.Snapshot("V-RACE");
        }
        catch (Exception error)
        {
            throw new ObservationHarnessException("read_error: " + error.GetType().Name);
        }
        return new RaceObservation(
            rows,
            rows.Count(r => r.Result is { Ok: true }),
            [.. rows.Where(r => r.Error is not null).Select(r => r.Error!)],
            Volatile.Read(ref checkpointCalls),
            events.Count,
            events,
            snapshot);
    }

    public static StateObservation RedemptionObservation(
        Func<IEnumerable<Voucher>, VoucherStore> create, string state, int now = Now)
    {
        var service = create([new Voucher("V-STATE", ExpiresAt, state)]);
        var result = service.Redeem("V-STATE", "alice", now);
        return new StateObservation(result, service.Events().Count, service.Snapshot("V-STATE"));
    }
}
