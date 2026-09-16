using AlloyClient.Game.Objects;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;

namespace AlloyClient.Game.Components.Hud.Inventory;

public sealed class InventoryGrid : Sprite {

    private const int NumSlots = 8;
    private const int TileSize = 40;
    private const int TileGap = 4;

    private readonly static CutEdges[] Cuts = [CutEdges.TopLeft, CutEdges.None, CutEdges.None, CutEdges.TopRight, CutEdges.BottomLeft, CutEdges.None, CutEdges.None, CutEdges.BottomRight];
    private readonly ItemTile[] _tiles = new ItemTile[NumSlots];

    private readonly Entity _owner;

    private readonly int _offset;

    private readonly bool _interactive;

    private readonly PotionInventory _potionInventory;


    public InventoryGrid(Entity owner, int offset, bool oneWay = false, bool isBackpack = false) {
        _owner = owner;
        _offset = offset;
        _interactive = owner == Map.LocalPlayer || owner.Properties.Container;

        _owner.InventoryUpdate.Add(OnInventoryChange);

        for (var i = 0; i < NumSlots; i++)
        {
            var slot = new ItemTile(owner, (byte)(i + offset), _interactive, Cuts[i], oneWay, tileSize: TileSize);
            slot.SetTileNumber(i + 1);
            slot.X = i % 4 * (TileSize + TileGap);
            slot.Y = i / 4 * (TileSize + TileGap);
            AddChild(slot);
            _tiles[i] = slot;
        }

        if (owner == Map.LocalPlayer && owner is Player player)
        {
            _potionInventory = new PotionInventory(player) { Y = 88 };
            AddChild(_potionInventory);
        }
    }

    public void RefreshPotionCounts() => _potionInventory?.Refresh();
    
    private void OnInventoryChange(int slot) 
    {
        if (!Visible) //Unsure how reliable this is, its to stop issues with the Backpack & Inventory trying to update when hidden
        {
            return;
        }

        if (slot < _offset || slot >= _offset + NumSlots) return;
        _tiles[slot - _offset].SetItem(_owner.Equipment[slot]);
    }
    
}
