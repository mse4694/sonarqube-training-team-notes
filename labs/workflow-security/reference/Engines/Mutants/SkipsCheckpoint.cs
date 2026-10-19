// Mutant: one deliberately weakened control; never applied to the work file.
namespace VoucherLab;

public sealed class VoucherService(IEnumerable<Voucher>? records = null) : SecureVoucherBase(records)
{
    public override Redemption Redeem(string code, string actor, int now, Action? checkpoint = null) =>
        base.Redeem(code, actor, now, checkpoint: null);
}
