// PROVIDED instructor contract example, not learner-authored work.
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

    [Fact]
    public void SameActorReplayIsRejected()
    {
        var service = Scenario.ActiveService(() => NewService());
        var first = service.Redeem("V-ACTIVE", "alice", Now);
        var second = service.Redeem("V-ACTIVE", "alice", Now + 1);
        Assert.True(first.Ok);
        Assert.False(second.Ok);
        Assert.Null(second.ReceiptId);
        Assert.Single(service.Events());
    }

    [Fact]
    public void IssuedVoucherIsRejected()
    {
        var service = NewService(new Voucher("V-ISSUED", ExpiresAt));
        var result = service.Redeem("V-ISSUED", "alice", Now);
        Assert.False(result.Ok);
        Assert.Null(result.ReceiptId);
        Assert.Equal("issued", service.Snapshot("V-ISSUED")!.State);
        Assert.Empty(service.Events());
    }

    [Fact]
    public void CancelledVoucherIsRejected()
    {
        var service = Scenario.ActiveService(() => NewService(), "V-CANCELLED");
        Assert.True(service.Cancel("V-CANCELLED"));
        var result = service.Redeem("V-CANCELLED", "alice", Now);
        Assert.False(result.Ok);
        Assert.Equal("cancelled", service.Snapshot("V-CANCELLED")!.State);
        Assert.Empty(service.Events());
    }

    [Fact]
    public void ExpiredStateIsRejected()
    {
        // An explicit expired state matters even when its timestamp is in the future.
        var service = NewService(new Voucher("V-EXPIRED", ExpiresAt, VoucherStates.Expired));
        var result = service.Redeem("V-EXPIRED", "alice", Now);
        Assert.False(result.Ok);
        Assert.Equal("expired", service.Snapshot("V-EXPIRED")!.State);
        Assert.Empty(service.Events());
    }

    [Fact]
    public void ExpiryDeadlineIsRejected()
    {
        var service = Scenario.ActiveService(() => NewService());
        var result = service.Redeem("V-ACTIVE", "alice", ExpiresAt);
        Assert.False(result.Ok); // boundary is now >= expires_at
        Assert.Null(result.ReceiptId);
        Assert.Empty(service.Events());
    }

    [Fact]
    public void ConcurrentRedemptionHasOneSuccess()
    {
        var observed = Scenario.ConcurrentObservation(() => NewService());
        Assert.Equal(2, observed.CheckpointCalls); // do not bypass the scheduling seam
        Assert.Empty(observed.Errors);             // a deadlock/barrier error is not a secure result
        Assert.Equal(1, observed.SuccessCount);
        Assert.Equal(1, observed.EventCount);
        Assert.Equal("used", observed.Snapshot!.State);
        var winner = observed.Requests.Single(r => r.Result is { Ok: true });
        Assert.Equal(winner.Actor, observed.Snapshot.RedeemedBy);
        Assert.Equal(winner.Actor, observed.Events[0].Actor);
    }
}
