using System;
using System.Collections.Generic;
using static PKHeX.Core.InventoryType;

namespace PKHeX.Core;

/// <summary>
/// Team Rocket Edition bag: eight pockets plus the PC, at the offsets and sizes the game sets up in
/// SetBagItemsPointers (read from the ROM). Quantities are XORed with the save's security key, as in Emerald.
/// </summary>
public sealed class PlayerBagTRE : PlayerBag, IPlayerBag3
{
    // ROM pocket IDs (ItemInfo.pocket) -> PKHeX pouch types
    private static readonly InventoryType[] PocketTypes = [Items, Medicine, TMHMs, KeyItems, Balls, BattleItems, Berries, MegaStones];

    public override IReadOnlyList<InventoryPouch3> Pouches { get; } =
    [
        new(0x4C0, 210, 999, ItemStorageTRE.Instance, Items),
        new(0xE98, 065, 999, ItemStorageTRE.Instance, Medicine),
        new(0x920, 028, 999, ItemStorageTRE.Instance, Balls),
        new(0x990, 254, 001, ItemStorageTRE.Instance, TMHMs),
        new(0xD88, 068, 999, ItemStorageTRE.Instance, Berries),
        new(0xF9C, 210, 999, ItemStorageTRE.Instance, BattleItems), // held items
        new(0x130C, 150, 001, ItemStorageTRE.Instance, MegaStones),
        new(0x808, 070, 001, ItemStorageTRE.Instance, KeyItems),
        new(0x498, 010, 999, ItemStorageTRE.Instance, PCItems),
    ];

    public override IItemStorage Info => ItemStorageTRE.Instance;

    public PlayerBagTRE(SAV3TRE sav)
    {
        UpdateSecurityKey(sav.SecurityKey);
        Pouches.LoadAll(sav.Large);
    }

    public override void CopyTo(SaveFile sav) => Pouches.SaveAll(((SAV3TRE)sav).Large);

    public void UpdateSecurityKey(uint securityKey)
    {
        foreach (var pouch in Pouches)
            pouch.SecurityKey = pouch.Type == PCItems ? 0 : securityKey;
    }

    internal static InventoryType GetPouchType(int item)
    {
        var pockets = TREData.ItemPockets;
        if ((uint)item >= pockets.Length)
            return None;
        var p = pockets[item];
        return p < PocketTypes.Length ? PocketTypes[p] : None;
    }
}

/// <summary>
/// Which items can go in each <see cref="PlayerBagTRE"/> pouch, from the pocket each item declares in the ROM.
/// </summary>
public sealed class ItemStorageTRE : IItemStorage
{
    public static readonly ItemStorageTRE Instance = new();

    private readonly Dictionary<InventoryType, ushort[]> _items = [];

    private ItemStorageTRE()
    {
        var names = TREData.GetItemDisplayNames();
        var lists = new Dictionary<InventoryType, List<ushort>>();
        var pc = new List<ushort>();
        for (ushort i = 1; i < names.Length; i++)
        {
            if (names[i].Length == 0)
                continue;
            var type = PlayerBagTRE.GetPouchType(i);
            if (type == InventoryType.None)
                continue;
            if (!lists.TryGetValue(type, out var list))
                lists[type] = list = [];
            list.Add(i);
            if (type != InventoryType.KeyItems)
                pc.Add(i);
        }
        foreach (var (type, list) in lists)
            _items[type] = [.. list];
        _items[InventoryType.PCItems] = [.. pc];
    }

    public bool IsLegal(InventoryType type, int itemIndex, int itemCount) => true;

    public ReadOnlySpan<ushort> GetItems(InventoryType type) => _items.TryGetValue(type, out var list) ? list : [];
}
