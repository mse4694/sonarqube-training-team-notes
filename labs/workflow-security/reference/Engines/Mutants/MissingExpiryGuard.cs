// Mutant: one deliberately weakened control; never applied to the work file.
namespace VoucherLab;

public sealed class VoucherService(IEnumerable<Voucher>? records = null) : SecureVoucherBase(records)
{
    protected override string? FailureReason(Voucher voucher, string actor, int now)
    {
        if (voucher.State == VoucherStates.Used)
            return "already_used";
        if (voucher.State != VoucherStates.Active)
            return "invalid_state";
        return null;
    }
}
