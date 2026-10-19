// Read-only fixture model and ordinary lifecycle operations; no persistence.
namespace VoucherLab;

public static class VoucherStates
{
    public const string Issued = "issued";
    public const string Active = "active";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
    public const string Used = "used";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { Issued, Active, Cancelled, Expired, Used };
}

public sealed class Voucher(string code, int expiresAt, string state = VoucherStates.Issued, string? redeemedBy = null)
{
    public string Code { get; } = code;
    public int ExpiresAt { get; } = expiresAt;
    public string State { get; set; } = state;
    public string? RedeemedBy { get; set; } = redeemedBy;

    internal Voucher Copy() => new(Code, ExpiresAt, State, RedeemedBy);
}

public sealed record Redemption(bool Ok, string Reason, string? ReceiptId = null, string? Actor = null);

public sealed record RedemptionEvent(string Code, string Actor, string ReceiptId);

public sealed record VoucherSnapshot(string Code, int ExpiresAt, string State, string? RedeemedBy, int RedemptionCount);

/// <summary>Shared plumbing, not the redemption policy being repaired.</summary>
public abstract class VoucherStore
{
    protected readonly Lock Gate = new();
    protected readonly Dictionary<string, Voucher> Records = new(StringComparer.Ordinal);
    private readonly List<RedemptionEvent> _events = [];

    protected VoucherStore(IEnumerable<Voucher>? records = null)
    {
        foreach (var record in records ?? [])
        {
            if (string.IsNullOrEmpty(record.Code) || !VoucherStates.All.Contains(record.State))
                throw new ArgumentException("invalid fixture voucher");
            if (Records.ContainsKey(record.Code))
                throw new ArgumentException("duplicate fixture code");
            Records[record.Code] = record.Copy();
        }
    }

    /// <summary>
    /// <paramref name="now"/> is a trusted integer fixture clock, not user-supplied production time.
    /// <paramref name="checkpoint"/> is a TEST-ONLY scheduling seam used by the race observation.
    /// </summary>
    public abstract Redemption Redeem(string code, string actor, int now, Action? checkpoint = null);

    public void Issue(string code, int expiresAt)
    {
        lock (Gate)
        {
            if (string.IsNullOrEmpty(code) || Records.ContainsKey(code))
                throw new ArgumentException("invalid or duplicate fixture voucher");
            Records[code] = new Voucher(code, expiresAt);
        }
    }

    public bool Activate(string code)
    {
        lock (Gate)
        {
            if (!Records.TryGetValue(code, out var voucher) || voucher.State != VoucherStates.Issued)
                return false;
            voucher.State = VoucherStates.Active;
            return true;
        }
    }

    public bool Cancel(string code)
    {
        lock (Gate)
        {
            if (!Records.TryGetValue(code, out var voucher)
                || (voucher.State != VoucherStates.Issued && voucher.State != VoucherStates.Active))
                return false;
            voucher.State = VoucherStates.Cancelled;
            return true;
        }
    }

    public VoucherSnapshot? Snapshot(string code)
    {
        lock (Gate)
        {
            if (!Records.TryGetValue(code, out var voucher))
                return null;
            var count = _events.Count(e => e.Code == code);
            return new VoucherSnapshot(voucher.Code, voucher.ExpiresAt, voucher.State, voucher.RedeemedBy, count);
        }
    }

    public IReadOnlyList<RedemptionEvent> Events()
    {
        lock (Gate)
        {
            return [.. _events];
        }
    }

    /// <summary>Caller must hold <see cref="Gate"/>. Each call creates a NEW business effect.</summary>
    protected Redemption CommitRedemption(Voucher voucher, string actor)
    {
        var receiptId = $"receipt-{_events.Count + 1:D4}";
        voucher.State = VoucherStates.Used;
        voucher.RedeemedBy = actor;
        _events.Add(new RedemptionEvent(voucher.Code, actor, receiptId));
        return new Redemption(true, "redeemed", receiptId, actor);
    }
}
