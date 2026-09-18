using GRD.SpChn.Accounting.Domain;

namespace GRD.SpChn.UnitTests.Accounting;

public sealed class VendorPayableTests
{
    [Fact]
    public void Invoice_requires_separate_creator_and_approver_before_payment()
    {
        var creator = Guid.NewGuid();
        var payable = CreatePayable(creator);

        var selfApproval = Assert.Throws<InvalidOperationException>(() =>
            payable.Approve(creator));
        Assert.Contains("creator cannot approve", selfApproval.Message);

        var approver = Guid.NewGuid();
        payable.Approve(approver, new DateTime(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc));
        payable.RecordPayment(
            approver,
            "UTR-20260918-001",
            DateTime.UtcNow.AddMinutes(-1));

        Assert.Equal(VendorPayableStatus.Paid, payable.Status);
        Assert.Equal("UTR-20260918-001", payable.BankReference);
        Assert.Equal(1180m, payable.TotalAmount);
    }

    [Fact]
    public void Payment_cannot_be_recorded_before_approval()
    {
        var payable = CreatePayable(Guid.NewGuid());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            payable.RecordPayment(Guid.NewGuid(), "UTR-001", DateTime.UtcNow));

        Assert.Contains("cannot be paid", exception.Message);
    }

    [Fact]
    public void Journal_entry_requires_equal_debits_and_credits()
    {
        Assert.Throws<InvalidOperationException>(() => JournalEntry.Create(
            "VendorPayment",
            "AccountingPayment",
            Guid.NewGuid(),
            "INR",
            "Unbalanced test",
            [
                JournalLine.DebitLine("VENDOR_PAYABLE", 1000),
                JournalLine.CreditLine("BANK", 900)
            ]));
    }

    private static VendorPayable CreatePayable(Guid creator) => VendorPayable.Create(
        "SUP-INV-001",
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "INR",
        180,
        creator,
        new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc),
        [new VendorInvoiceLine(Guid.NewGuid(), 10, "BAG", 100)],
        new DateTime(2026, 9, 18, 9, 0, 0, DateTimeKind.Utc));
}
