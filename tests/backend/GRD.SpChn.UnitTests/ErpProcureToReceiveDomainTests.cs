using GRD.SpChn.Organization.Domain;
using GRD.SpChn.Procurement.Domain;
using GRD.SpChn.Warehouse.Domain;

namespace GRD.SpChn.UnitTests;

public sealed class ErpProcureToReceiveDomainTests
{
    [Fact]
    public void Organization_hierarchy_accepts_branch_and_plant_structure()
    {
        var branchId = Guid.NewGuid();
        var plant = OrganizationUnit.Create(
            branchId,
            "DELHI-PLANT-2",
            "Delhi Manufacturing Plant 2",
            OrganizationUnitType.ManufacturingPlant,
            OrganizationUnitType.Branch);

        Assert.Equal(branchId, plant.ParentId);
        Assert.Equal(OrganizationUnitType.ManufacturingPlant, plant.Type);
    }

    [Fact]
    public void Organization_hierarchy_rejects_plant_directly_below_head_office()
    {
        Assert.Throws<ArgumentException>(() => OrganizationUnit.Create(
            Guid.NewGuid(),
            "INVALID-PLANT",
            "Invalid Plant",
            OrganizationUnitType.ManufacturingPlant,
            OrganizationUnitType.HeadOffice));
    }

    [Fact]
    public void Approved_material_request_can_issue_one_matching_purchase_order()
    {
        var productId = Guid.NewGuid();
        var request = MaterialRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Raw material for production",
            [MaterialRequestItem.Create(productId, 25, "KG")]);
        request.Approve(Guid.NewGuid());

        var purchaseOrder = PurchaseOrder.Issue(
            request,
            Guid.NewGuid(),
            "INR",
            new Dictionary<Guid, decimal> { [productId] = 120.50m });
        request.AttachPurchaseOrder(purchaseOrder.Id);

        Assert.Equal(MaterialRequestStatus.PurchaseOrderIssued, request.Status);
        Assert.Equal(PurchaseOrderStatus.Issued, purchaseOrder.Status);
        Assert.Equal(25, purchaseOrder.Items.Single().Quantity);
        Assert.Equal(3012.50m, purchaseOrder.TotalAmount);
    }

    [Fact]
    public void Purchase_order_cannot_be_issued_before_approval()
    {
        var productId = Guid.NewGuid();
        var request = MaterialRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Raw material",
            [MaterialRequestItem.Create(productId, 10, "KG")]);

        Assert.Throws<InvalidOperationException>(() => PurchaseOrder.Issue(
            request,
            Guid.NewGuid(),
            "INR",
            new Dictionary<Guid, decimal> { [productId] = 10 }));
    }

    [Fact]
    public void Issued_purchase_order_records_material_dispatch_before_receipt()
    {
        var productId = Guid.NewGuid();
        var request = MaterialRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Packing bags for production",
            [MaterialRequestItem.Create(productId, 100, "BAG")]);
        request.Approve(Guid.NewGuid());
        var supplierId = Guid.NewGuid();
        var purchaseOrder = PurchaseOrder.Issue(
            request,
            supplierId,
            "INR",
            new Dictionary<Guid, decimal> { [productId] = 35 });

        var dispatchedOnUtc = new DateTime(2026, 8, 31, 6, 30, 0, DateTimeKind.Utc);
        var recordedOnUtc = dispatchedOnUtc.AddHours(1);
        var dispatch = PurchaseOrderDispatch.Record(
            purchaseOrder,
            Guid.NewGuid(),
            "VENDOR-DSP-1001",
            "CH-1001",
            "GRD Test Transport",
            "DL 01 AB 1234",
            dispatchedOnUtc,
            dispatchedOnUtc.AddDays(1),
            "Full order dispatched",
            recordedOnUtc);
        purchaseOrder.MarkDispatched(dispatch);

        Assert.Equal(PurchaseOrderStatus.Dispatched, purchaseOrder.Status);
        Assert.Equal(dispatchedOnUtc, purchaseOrder.DispatchedOnUtc);
        Assert.Equal(supplierId, dispatch.SupplierId);
        Assert.Equal("VENDOR-DSP-1001", dispatch.VendorDispatchReference);
        purchaseOrder.MarkReceived(dispatchedOnUtc.AddDays(1));
        Assert.Equal(PurchaseOrderStatus.Received, purchaseOrder.Status);
    }

    [Fact]
    public void Warehouse_receipt_must_match_PO_and_receiving_location()
    {
        var productId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var expected = ExpectedPurchaseOrder.Register(
            Guid.NewGuid(),
            "PO-001",
            Guid.NewGuid(),
            locationId,
            [new ExpectedPurchaseOrderItem(productId, 50, "KG")],
            DateTime.UtcNow);

        Assert.Throws<UnauthorizedAccessException>(() => expected.Receive(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new ReceivedItem(productId, 50, "KG")]));

        var receipt = expected.Receive(
            locationId,
            Guid.NewGuid(),
            [new ReceivedItem(productId, 50, "KG")]);

        Assert.Equal(ExpectedPurchaseOrderStatus.Received, expected.Status);
        Assert.Equal(locationId, receipt.DestinationOrganizationUnitId);
    }

    [Fact]
    public void Received_material_can_be_released_only_by_quality_at_the_same_location()
    {
        var locationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var expected = ExpectedPurchaseOrder.Register(
            Guid.NewGuid(),
            "PO-QUALITY-001",
            Guid.NewGuid(),
            locationId,
            [new ExpectedPurchaseOrderItem(productId, 25, "KG")],
            DateTime.UtcNow);
        var receipt = expected.Receive(
            locationId,
            Guid.NewGuid(),
            [new ReceivedItem(productId, 25, "KG")]);

        Assert.Throws<UnauthorizedAccessException>(() => QualityInspection.Complete(
            receipt,
            Guid.NewGuid(),
            Guid.NewGuid(),
            QualityInspectionResult.Passed,
            null));

        var inspection = QualityInspection.Complete(
            receipt,
            locationId,
            Guid.NewGuid(),
            QualityInspectionResult.Passed,
            "Moisture and packaging within specification");

        Assert.Equal(QualityInspectionResult.Passed, inspection.Result);
        Assert.Equal(receipt.Id, inspection.GoodsReceiptId);
    }

    [Fact]
    public void Rejected_quality_inspection_requires_a_reason()
    {
        var locationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var expected = ExpectedPurchaseOrder.Register(
            Guid.NewGuid(),
            "PO-QUALITY-002",
            Guid.NewGuid(),
            locationId,
            [new ExpectedPurchaseOrderItem(productId, 10, "BAG")],
            DateTime.UtcNow);
        var receipt = expected.Receive(
            locationId,
            Guid.NewGuid(),
            [new ReceivedItem(productId, 10, "BAG")]);

        Assert.Throws<ArgumentException>(() => QualityInspection.Complete(
            receipt,
            locationId,
            Guid.NewGuid(),
            QualityInspectionResult.Rejected,
            null));
    }
}
