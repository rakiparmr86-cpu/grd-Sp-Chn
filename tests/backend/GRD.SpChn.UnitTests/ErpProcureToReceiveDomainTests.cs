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
}
