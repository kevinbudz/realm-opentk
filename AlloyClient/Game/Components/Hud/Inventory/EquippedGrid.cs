using System.Collections.Generic;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Game.Objects;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;

namespace AlloyClient.Game.Components.Hud.Inventory;

public sealed class EquippedGrid : Sprite {

    private const byte NumSlots = 4;
    private const int TileSize = 40;
    private const int TileGap = 4;

    private readonly static CutEdges[] Cuts = [CutEdges.Left, CutEdges.None, CutEdges.None, CutEdges.Right];
    private readonly ItemTile[] _tileSlots = new ItemTile[4];

    private readonly Player _owner;
    private bool _isSubscribed;

    public EquippedGrid(Player owner) {
        _owner = owner;
        
        var bg = new CutEdgeRect(new CutEdgeConfig { X = -3, Y = -3, Width = 178, Height = 46, CutX = 6, CutY = 6, Cuts = CutEdges.All, Color = 0x676767 });
        AddChild(bg);
        
        for (byte i = 0; i < NumSlots; i++) {
            var slot = new ItemTile(_owner, i, true, Cuts[i], false, (byte)_owner.Properties.SlotTypes[i], tileSize: TileSize, bgcolor: 0x454545);
            slot.X = i % 4 * (TileSize + TileGap);
            slot.Y = i / 4 * (TileSize + TileGap);
            AddChild(slot);
            _tileSlots[i] = slot;
        }

        // Own this grid's subscription. TabStrip keeps its child grids alive while
        // switching tabs, so there is no reason to clear the owner's signal here.
        AddEventListener(Event.Added, OnAdded);
        AddEventListener(Event.Removed, OnRemoved);
        Subscribe();
    }
    
    public EquippedGrid(ItemDesc[] items, List<int> slotTypes) {
        var bg = new CutEdgeRect(new CutEdgeConfig { X = -3, Y = -3, Width = 178, Height = 46, CutX = 6, CutY = 6, Cuts = CutEdges.All, Color = 0x676767 });
        AddChild(bg);

        for (byte i = 0; i < NumSlots; i++) {
            var slot = new ItemTile(null, i, false, Cuts[i], false, (byte)slotTypes[i], tileSize: TileSize, bgcolor: 0x454545);
            slot.X = i % 4 * (TileSize + TileGap);
            slot.Y = i / 4 * (TileSize + TileGap);
            slot.SetItem(items[i]);
            AddChild(slot);
            _tileSlots[i] = slot;
        }
    }

    public void UpdateAbilitySlot() {
        var slot = _tileSlots[1];

        if (slot.ItemDesc == null || slot.ItemDesc.ObjectType == 0)
            return;

        var noMana = Map.LocalPlayer.Mp < slot.ItemDesc.MpCost;
        //todo: silence check
        
        slot.SetDim(noMana);
    }

    private void OnInventoryChange(int slot) {
        if (!_isSubscribed || slot < 0 || slot >= NumSlots) return;
        _tileSlots[slot].SetItem(_owner.Equipment[slot]);
    }

    private void OnAdded() => Subscribe();

    private void OnRemoved(Event @event) {
        // Removed bubbles from item tiles; keep the owner subscription while this
        // grid remains attached and only release it for the grid's own removal.
        if (@event.Target == this)
            Unsubscribe();
    }

    private void Subscribe() {
        if (_isSubscribed) return;

        _owner.InventoryUpdate.Add(OnInventoryChange);
        _isSubscribed = true;
        RefreshFromOwner();
    }

    private void Unsubscribe() {
        if (!_isSubscribed) return;

        _owner.InventoryUpdate.Remove(OnInventoryChange);
        _isSubscribed = false;
    }

    private void RefreshFromOwner() {
        for (var i = 0; i < NumSlots; i++) {
            _tileSlots[i].SetItem(_owner.Equipment[i]);
        }
    }
}
