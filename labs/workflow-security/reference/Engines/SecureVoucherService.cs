// Instructor reference engine: the secure policy with nothing weakened.
namespace VoucherLab;

public sealed class VoucherService(IEnumerable<Voucher>? records = null) : SecureVoucherBase(records);
