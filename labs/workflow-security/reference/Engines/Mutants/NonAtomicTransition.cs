// Mutant: one deliberately weakened control; never applied to the work file.
namespace VoucherLab;

public sealed class VoucherService(IEnumerable<Voucher>? records = null) : SecureVoucherBase(records)
{
    public override Redemption Redeem(string code, string actor, int now, Action? checkpoint = null)
    {
        Voucher? voucher;
        lock (Gate)
        {
            if (!Records.TryGetValue(code, out voucher))
                return new Redemption(false, "unknown_voucher");
            var reason = FailureReason(voucher, actor, now);
            if (reason is not null)
                return new Redemption(false, reason);
        }
        checkpoint?.Invoke();
        lock (Gate)
        {
            return CommitRedemption(voucher, actor);
        }
    }
}
