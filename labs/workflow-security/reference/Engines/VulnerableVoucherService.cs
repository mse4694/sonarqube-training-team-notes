// Frozen original engine for test-quality comparisons after Voucher/VoucherService.cs is patched.
namespace VoucherLab;

public sealed class VoucherService(IEnumerable<Voucher>? records = null) : VoucherStore(records)
{
    public override Redemption Redeem(string code, string actor, int now, Action? checkpoint = null)
    {
        Voucher? voucher;
        lock (Gate)
        {
            if (!Records.TryGetValue(code, out voucher))
                return new Redemption(false, "unknown_voucher");
            if (voucher.State == VoucherStates.Used && voucher.RedeemedBy != actor)
                return new Redemption(false, "already_used");
        }
        checkpoint?.Invoke();
        lock (Gate)
        {
            return CommitRedemption(voucher, actor);
        }
    }
}
