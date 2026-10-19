// Learner starter: TWO assertion-based tests and SIX explicitly unfinished tests.
using VoucherLab;
using Xunit;

namespace VoucherLab.Tests;

public class RedemptionContractTests
{
    private const int Now = Scenario.Now;
    private const int ExpiresAt = Scenario.ExpiresAt;

    private static VoucherService NewService(params Voucher[] seed) => new(seed);

    [Fact]
    public void ValidActiveVoucherSucceedsOnce()
    {
        var service = NewService();
        service.Issue("V-VALID", ExpiresAt);
        Assert.Equal("issued", service.Snapshot("V-VALID")!.State);
        Assert.True(service.Activate("V-VALID"));
        var result = service.Redeem("V-VALID", "alice", Now);
        Assert.True(result.Ok);
        Assert.NotNull(result.ReceiptId);
        Assert.Equal("used", service.Snapshot("V-VALID")!.State);
        Assert.Equal("alice", service.Snapshot("V-VALID")!.RedeemedBy);
        Assert.Single(service.Events());
    }

    [Fact]
    public void UnknownVoucherIsRejected()
    {
        var service = NewService();
        var result = service.Redeem("V-UNKNOWN", "alice", Now);
        Assert.False(result.Ok);
        Assert.Equal("unknown_voucher", result.Reason);
        Assert.Null(result.ReceiptId);
        Assert.Empty(service.Events());
    }

    [Fact(Skip = "TODO: learner must implement replay assertions and remove this Skip")]
    public void SameActorReplayIsRejected()
    {
        // Redeem once, repeat with the SAME actor, inspect the receipt and the event count.
        Assert.Fail("not implemented");
    }

    [Fact(Skip = "TODO: learner must implement issued-state assertions and remove this Skip")]
    public void IssuedVoucherIsRejected()
    {
        // Issued is not active; no new receipt/effect should occur.
        Assert.Fail("not implemented");
    }

    [Fact(Skip = "TODO: learner must implement cancellation assertions and remove this Skip")]
    public void CancelledVoucherIsRejected()
    {
        // Activate then cancel before attempting redemption.
        Assert.Fail("not implemented");
    }

    [Fact(Skip = "TODO: learner must implement expired-state assertions and remove this Skip")]
    public void ExpiredStateIsRejected()
    {
        // A seeded expired state with a future timestamp must still be rejected.
        Assert.Fail("not implemented");
    }

    [Fact(Skip = "TODO: learner must implement clock-boundary assertions and remove this Skip")]
    public void ExpiryDeadlineIsRejected()
    {
        // Use now == ExpiresAt, not Thread.Sleep or the real clock.
        Assert.Fail("not implemented");
    }

    [Fact(Skip = "TODO: learner must implement atomicity assertions and remove this Skip")]
    public void ConcurrentRedemptionHasOneSuccess()
    {
        // Scenario.ConcurrentObservation(() => NewService()) exposes both callers, CheckpointCalls,
        // Errors, Events and the final Snapshot. Assert one effect, not just State == "used".
        Assert.Fail("not implemented");
    }
}
