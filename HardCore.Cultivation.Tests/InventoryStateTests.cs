using HardCore.Cultivation.Inventory;
using Xunit;

namespace HardCore.Cultivation.Tests;

public sealed class InventoryStateTests
{
    [Fact]
    public void SellSelected_RemovesOneAndReturnsConfiguredPrice()
    {
        var inventory = CreateState(("common", "COMMON", 3, 17));

        Assert.True(inventory.Select(0));
        Assert.Equal(17, inventory.SellSelected());
        Assert.Equal(2, inventory.Stacks[0].Quantity);
        Assert.Equal(2, inventory.UsedCapacity);
        Assert.Null(inventory.Selected);
    }

    [Fact]
    public void SellSelected_RemovesEmptyStack()
    {
        var inventory = CreateState(("single", "SINGLE", 1, 9));

        inventory.Select(0);
        inventory.SellSelected();

        Assert.Empty(inventory.Stacks);
        Assert.Equal(0, inventory.UsedCapacity);
    }

    [Fact]
    public void Sort_OrdersByQualityThenName()
    {
        var inventory = CreateState(
            ("common", "COMMON", 1, 1, "common"),
            ("rare-z", "ZETA", 1, 1, "rare"),
            ("rare-a", "ALPHA", 1, 1, "rare"));

        inventory.Sort();

        Assert.Equal(["ALPHA", "ZETA", "COMMON"], inventory.Stacks.Select(stack => stack.Item.Name));
    }

    [Fact]
    public void Constructor_RejectsUnknownItemReference()
    {
        var catalog = new ItemCatalogConfig();
        var config = new InventoryConfig
        {
            Stacks = [new InventoryEntryConfig { ItemId = "missing", Quantity = 1 }]
        };

        Assert.Throws<InvalidDataException>(() => new InventoryState(catalog, config));
    }

    private static InventoryState CreateState(
        params (string Id, string Name, int Quantity, int Price, string Quality)[] entries)
    {
        var catalog = new ItemCatalogConfig
        {
            Items = entries.Select(entry => new ItemDefinition
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = "TEST ITEM",
                Icon = "/test.atlas#item",
                Quality = entry.Quality,
                SellPrice = entry.Price,
                MaxStack = 99
            }).ToList()
        };
        var config = new InventoryConfig
        {
            Capacity = 100,
            Stacks = entries.Select(entry => new InventoryEntryConfig
            {
                ItemId = entry.Id,
                Quantity = entry.Quantity
            }).ToList()
        };
        return new InventoryState(catalog, config);
    }

    private static InventoryState CreateState(
        params (string Id, string Name, int Quantity, int Price)[] entries) =>
        CreateState(entries.Select(entry =>
            (entry.Id, entry.Name, entry.Quantity, entry.Price, "common")).ToArray());
}
