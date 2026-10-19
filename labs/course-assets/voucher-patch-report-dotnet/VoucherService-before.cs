// Intentionally vulnerable training engine. Only this file is an AI edit target.
namespace VoucherLab;

public sealed class VoucherService(IEnumerable<Voucher>? records = null) : VoucherStore(records)
{
    public override Redemption Redeem(string code, string actor, int now, Action? checkpoint = null)
    {
        // now is a trusted integer fixture clock, not user-supplied production time.
        Voucher? voucher;
        lock (Gate)
        {
            if (!Records.TryGetValue(code, out voucher))
                return new Redemption(false, "unknown_voucher");
            // Intentional gaps: same-actor replay, inactive states and expiry.
            if (voucher.State == VoucherStates.Used && voucher.RedeemedBy != actor)
                return new Redemption(false, "already_used");
        }

        // TEST-ONLY scheduling seam: both racing calls have checked the old state.
        // Do not delete the seam to make the concurrency test stop exercising overlap.
        checkpoint?.Invoke();

        lock (Gate)
        {
            // A write lock alone is not an atomic check-and-transition.
            return CommitRedemption(voucher, actor);
        }
    }
}
