using System.Linq;
using AlloyClient.Assets.XmlStructs;

namespace AlloyClient.Game.Objects.Util;

// Flash parity for Player.useAltWeapon (realm-client Player.as): space uses
// the slot-1 ability via UseItem, gated by Usable, MP, and cooldown.
public static class AbilityHelper {
    public const byte AbilitySlotId = 1;
    public const float DefaultCooldownSec = 0.2f;

    public static bool CanUse(ItemDesc ability, int currentMp, double nowMs, double nextAllowedMs) {
        if (ability == null || ability.ObjectType == 0)
            return false;
        if (!ability.Usable)
            return false;
        if (nowMs < nextAllowedMs)
            return false;
        if (ability.MpCost > currentMp)
            return false;
        return true;
    }

    public static double NextUseTime(ItemDesc ability, int itemData, double nowMs) {
        var baseCooldown = ability.HasCooldown ? ability.Cooldown : DefaultCooldownSec;
        var mod = ItemData.GetStat(itemData, ItemData.CooldownBit, ItemData.CooldownMultiplier);
        var cooldownSec = baseCooldown * (1.0 - mod);
        if (cooldownSec < 0)
            cooldownSec = 0;
        return nowMs + cooldownSec * 1000.0;
    }

    public static bool HasShootActivate(ItemDesc ability) {
        if (ability?.ActivateEffects == null)
            return false;
        return ability.ActivateEffects.Any(e => e.Effect == "Shoot");
    }
}
