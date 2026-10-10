using System.Threading.RateLimiting;

namespace TeamNotes.Api;

/// <summary>
/// Fixed-window throttle for failed sign-ins, partitioned per client IP + email
/// (System.Threading.RateLimiting, the limiter behind ASP.NET Core's rate limiting middleware).
/// Only FAILED attempts consume a permit, so a user who types the right password is never slowed
/// down; after <see cref="MaxFailures"/> failures in <see cref="Window"/> the pair gets 429.
/// The middleware variant (AddRateLimiter/RequireRateLimiting) counts every request, including
/// successful logins, and cannot see the email in the form body, hence the explicit limiter here.
/// </summary>
public sealed class LoginThrottle : IDisposable
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly PartitionedRateLimiter<string> _failures = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = MaxFailures,
            Window = Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    public static string KeyFor(HttpContext ctx, string email) =>
        $"{ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"}|{email.Trim().ToLowerInvariant()}";

    public bool IsBlocked(string key) => (_failures.GetStatistics(key)?.CurrentAvailablePermits ?? MaxFailures) <= 0;

    public void RecordFailure(string key)
    {
        using RateLimitLease _ = _failures.AttemptAcquire(key);
    }

    public void Dispose() => _failures.Dispose();
}
