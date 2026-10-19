// Training engine (patched). Only this file is an AI edit target.
namespace VoucherLab;

public sealed class VoucherService(IEnumerable<Voucher>? records = null) : VoucherStore(records)
{
    public override Redemption Redeem(string code, string actor, int now, Action? checkpoint = null)
    {
        // now is a trusted integer fixture clock, not user-supplied production time.
        lock (Gate)
        {
            // Advisory fast-fail only; the authoritative decision is re-made below.
            if (Rejection(code, now, out _) is { } early)
                return early;
        }

        // TEST-ONLY scheduling seam: both racing calls have checked the old state.
        // Do not delete the seam to make the concurrency test stop exercising overlap.
        // It must stay outside the lock (a waiting caller holding Gate would deadlock).
        checkpoint?.Invoke();

        lock (Gate)
        {
            // Atomic check-and-transition: re-validate state and time under the same
            // lock that records the redemption, so a concurrent winner is observed.
            if (Rejection(code, now, out var voucher) is { } rejected)
                return rejected;
            return CommitRedemption(voucher!, actor);
        }
    }

    /// <summary>Caller must hold Gate. Null means redeemable; rejections carry no receipt and no effect.</summary>
    private Redemption? Rejection(string code, int now, out Voucher? voucher)
    {
        if (!Records.TryGetValue(code, out voucher))
            return new Redemption(false, "unknown_voucher");
        if (voucher.State == VoucherStates.Used)
            return new Redemption(false, "already_used");      // every actor, including the redeemer
        if (voucher.State != VoucherStates.Active)
            return new Redemption(false, "voucher_not_active"); // issued / cancelled / expired
        if (now >= voucher.ExpiresAt)
            return new Redemption(false, "voucher_expired");    // now == expires_at rejects (INV5)
        return null;
    }
}
