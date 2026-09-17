using System;
using System.Collections.Generic;
using System.Linq;
using AlloyClient.Assets.XmlStructs;

namespace AlloyClient.Assets.Libraries;

public static class ObjectLibrary {
    public readonly static Dictionary<ushort, ObjectProperties> TypeToObjectProps = new();
    public readonly static Dictionary<ushort, PlayerProperties> TypeToClassProps = new();
    public readonly static List<Tuple<ushort, ushort>> TypeToSkins = new();
    public readonly static Dictionary<ushort, TextureData> TypeToTextureData = new();

    public readonly static Dictionary<string, ushort> IdToObjectType = new();

    public readonly static Dictionary<ushort, ItemDesc> TypeToItem = new();

    public static ItemDesc GetItem(ushort type) => type == 0 ? null : TypeToItem[type];

    public static string GetDisplayName(ushort type) {
        if (TypeToObjectProps.TryGetValue(type, out var props) && !string.IsNullOrWhiteSpace(props.DisplayName))
            return props.DisplayName;
        if (TypeToItem.TryGetValue(type, out var item) && !string.IsNullOrWhiteSpace(item.DisplayName))
            return item.DisplayName;
        return type.ToString();
    }

    // Mirrors ObjectLibrary.isUsableByPlayer: potion slot is universal,
    // otherwise the item slot must appear in the player's slot list.
    public static bool IsUsableByPlayer(ItemDesc item, IReadOnlyList<int> playerSlotTypes) {
        if (item == null || !item.HasSlotType || playerSlotTypes == null)
            return false;
        if (item.SlotType == Ui.ItemConstants.PotionType)
            return true;
        for (var i = 0; i < playerSlotTypes.Count; i++) {
            if (playerSlotTypes[i] == item.SlotType)
                return true;
        }
        return false;
    }

    // Mirrors ObjectLibrary.usableBy: class names whose slot list contains
    // the item slot. Null when Flash shows no line (no slot, potion, ring).
    public static IReadOnlyList<string> GetUsableByNames(ItemDesc item) {
        if (item == null || !item.HasSlotType)
            return null;
        if (item.SlotType == Ui.ItemConstants.PotionType || item.SlotType == Ui.ItemConstants.RingType)
            return null;
        var usable = new List<string>();
        foreach (var type in TypeToClassProps.Keys.OrderBy(t => t)) {
            if (TypeToObjectProps.TryGetValue(type, out var props) && props.SlotTypes != null &&
                props.SlotTypes.Contains(item.SlotType))
                usable.Add(GetDisplayName(type));
        }
        return usable;
    }
}