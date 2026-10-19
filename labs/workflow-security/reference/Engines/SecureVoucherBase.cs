// Instructor reference: validate and consume in the same critical section.
namespace VoucherLab;

public class SecureVoucherBase(IEnumerable<Voucher>? records = null) : VoucherStore(records)
{
    protected virtual string? FailureReason(Voucher voucher, string actor, int now)
    {
        if (voucher.State == VoucherStates.Used)
            return "already_used";
        if (voucher.State != VoucherStates.Active)
            return "invalid_state";
        if (now >= voucher.ExpiresAt)
            return "expired";
        return null;
    }

    public override Redemption Redeem(string code, string actor, int now, Action? checkpoint = null)
    {
        // Test synchronization must not wait for the second caller while holding
        // the lock. The definitive state check follows this seam under the lock.
        checkpoint?.Invoke();
        lock (Gate)
        {
            if (!Records.TryGetValue(code, out var voucher))
                return new Redemption(false, "unknown_voucher");
            var reason = FailureReason(voucher, actor, now);
            if (reason is not null)
            {
                if (reason == "expired")
                    voucher.State = VoucherStates.Expired;
                return new Redemption(false, reason);
            }
            return CommitRedemption(voucher, actor);
        }
    }
}
