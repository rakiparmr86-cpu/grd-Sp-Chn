using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.Inventory.Application.Stock.GetStockLedger;

namespace GRD.SpChn.UnitTests;

public sealed class StockLedgerTests
{
    private static readonly Guid Plant = Guid.NewGuid();
    private static readonly Guid Maize = Guid.Parse("30000000-0000-0000-0000-000000000004");
    private static readonly Guid Rice = Guid.Parse("30000000-0000-0000-0000-000000000005");
    private static readonly DateTime From = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private static StockMovementRecord Release(Guid productId, decimal quantity, int day, string grn) => new(
        Guid.NewGuid(),
        From.AddDays(day),
        Plant,
        productId,
        "QualityRelease",
        quantity,
        "QualityInspection",
        Guid.NewGuid(),
        grn,
        Guid.NewGuid(),
        "PO-1",
        Guid.NewGuid());

    [Fact]
    public void Opening_balance_carries_into_the_running_balance_per_material()
    {
        var ledger = StockLedgerBuilder.Build(
            Plant,
            From,
            To,
            [new StockMovementTotal(Maize, "QualityRelease", 100)],
            [
                Release(Maize, 30, 2, "GRN-A"),
                Release(Rice, 200, 3, "GRN-B"),
                Release(Maize, 20, 5, "GRN-C"),
            ],
            [new LocationOnHand(Maize, 150), new LocationOnHand(Rice, 200)]);

        Assert.Equal([130m, 200m, 150m], ledger.Entries.Select(entry => entry.BalanceQuantity));
        var maize = Assert.Single(ledger.Products, product => product.ProductId == Maize);
        Assert.Equal(100m, maize.OpeningQuantity);
        Assert.Equal(50m, maize.InQuantity);
        Assert.Equal(0m, maize.OutQuantity);
        Assert.Equal(150m, maize.ClosingQuantity);
        Assert.Equal(maize.OnHandQuantity, maize.ClosingQuantity);
    }

    [Fact]
    public void Entries_are_ordered_by_time_even_when_read_out_of_order()
    {
        var later = Release(Maize, 20, 9, "GRN-LATE");
        var earlier = Release(Maize, 30, 1, "GRN-EARLY");

        var ledger = StockLedgerBuilder.Build(Plant, From, To, [], [later, earlier], []);

        Assert.Equal(["GRN-EARLY", "GRN-LATE"], ledger.Entries.Select(entry => entry.GoodsReceiptNumber));
        Assert.Equal([30m, 50m], ledger.Entries.Select(entry => entry.BalanceQuantity));
    }

    [Fact]
    public void Material_without_movements_in_the_period_still_shows_its_opening_balance()
    {
        var ledger = StockLedgerBuilder.Build(
            Plant,
            From,
            To,
            [new StockMovementTotal(Rice, "QualityRelease", 80)],
            [],
            [new LocationOnHand(Rice, 80)]);

        var rice = Assert.Single(ledger.Products);
        Assert.Equal(80m, rice.OpeningQuantity);
        Assert.Equal(80m, rice.ClosingQuantity);
        Assert.Empty(ledger.Entries);
    }

    [Fact]
    public void Location_scope_includes_every_unit_below_the_selected_one_only()
    {
        var branch = new StockLocation(Guid.NewGuid(), Guid.NewGuid(), "BR", "Branch");
        var plant = new StockLocation(Guid.NewGuid(), branch.Id, "PLANT", "Plant");
        var warehouse = new StockLocation(Guid.NewGuid(), branch.Id, "WH", "Warehouse");
        var plantStore = new StockLocation(Guid.NewGuid(), plant.Id, "PLANT-STORE", "Plant store");
        StockLocation[] tree = [branch, plant, warehouse, plantStore];

        Assert.Equal(
            new HashSet<Guid> { branch.Id, plant.Id, warehouse.Id, plantStore.Id },
            StockLocationScope.Subtree(tree, branch.Id).ToHashSet());
        Assert.Equal(
            new HashSet<Guid> { plant.Id, plantStore.Id },
            StockLocationScope.Subtree(tree, plant.Id).ToHashSet());
        Assert.Equal([warehouse.Id], StockLocationScope.Subtree(tree, warehouse.Id));
    }

    [Fact]
    public void Unknown_movement_type_is_rejected_instead_of_guessing_its_direction()
    {
        var movement = Release(Maize, 10, 1, "GRN-X") with { MovementType = "Mystery" };

        Assert.Throws<InvalidOperationException>(() =>
            StockLedgerBuilder.Build(Plant, From, To, [], [movement], []));
    }
}
