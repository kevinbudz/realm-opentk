using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AlloyClient.Assets.Libraries;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Game.Objects.Util;

namespace AlloyClient.Ui.Components.Tooltips;

// Inventory ownership, mirroring InventoryOwnerTypes.
public static class TooltipOwnerTypes {
    public const string CurrentPlayer = "CURRENT_PLAYER";
    public const string OtherPlayer = "OTHER_PLAYER";
    public const string Npc = "NPC";
}

// Shared tooltip colors copied from the Flash call sites.
public static class TooltipPalette {
    public const uint White = 16777215;
    public const uint Body = 11776947; // 0xB3B3B3 descriptions, effect names, restrictions
    public const uint EffectValue = 0xFFFF8F; // effect values
    public const uint Negative = 0xFF0000;
    public const uint TitleUnusable = 16549442;
    public const uint TierUntiered = 0x8A2BE2;
    public const uint TierSet = 0xFF9900;
}

// Minimal player snapshot for tooltip logic. ItemTile builds this from the
// live Player; tests construct it directly so the builder stays UI-free.
public sealed record PlayerTooltipContext(
    IReadOnlyList<int> SlotTypes,
    string ClassDisplayName,
    int MaxHp,
    int MaxMp,
    int Attack,
    int Defense,
    int Speed,
    int Vitality,
    int Wisdom,
    int Dexterity,
    int Level,
    bool IsInventoryFull);

// One value run inside an effect line; Flash wraps these in <font> tags.
public sealed record TooltipValueSegment(string Text, uint Color);

// An effect line: gray name (empty for value-only lines) plus value runs.
public sealed record TooltipEffect(string Name, TooltipValueSegment[] Value);

// A restriction line with its own color and bold flag.
public sealed record TooltipRestriction(string Text, uint Color, bool Bold);

// Pure port of EquipmentToolTip's effect/restriction builders. All rendering
// lives in EquipmentToolTip; this class is UI-free so it can be unit tested.
public static class EquipmentTooltipBuilder {
    // Mirrors TooltipHelper.getFormattedString: truncate to 3 decimals.
    public static string FormatValue(double value) {
        var truncated = (int)(value * 1000) / 1000.0;
        return truncated.ToString(CultureInfo.InvariantCulture);
    }

    // Game XML pretty-prints long descriptions across source lines. The raw
    // newlines and indentation would render as phantom breaks plus a trailing
    // blank line (one per source line), so collapse all whitespace runs.
    public static string CollapseWhitespace(string text) {
        if (string.IsNullOrEmpty(text))
            return text;
        return string.Join(" ", text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool PlayerCanUse(ItemDesc item, PlayerTooltipContext ctx) {
        return ObjectLibrary.IsUsableByPlayer(item, ctx.SlotTypes);
    }

    // Title color: rank color wins, else white, else red when another class's item.
    public static uint GetTitleColor(ItemDesc item, int itemData, PlayerTooltipContext ctx) {
        var color = ItemData.GetColor(itemData);
        if (color != -1)
            return (uint)color;
        var usable = ctx == null || PlayerCanUse(item, ctx);
        return usable ? TooltipPalette.White : TooltipPalette.TitleUnusable;
    }

    // Mirrors TierUtil.getTierTag: null means no tag is shown.
    public static (string Text, uint Color)? GetTierTag(ItemDesc item) {
        if (item.Consumable || item.IsPermaPet || item.Treasure || item.PetFood || item.NoTierTag)
            return null;
        if (item.HasTier)
            return ($"T{item.Tier}", TooltipPalette.White);
        if (item.IsSet)
            return ("ST", TooltipPalette.TierSet);
        return ("UT", TooltipPalette.TierUntiered);
    }

    public static List<TooltipEffect> BuildEffects(ItemDesc item, int itemData) {
        var effects = new List<TooltipEffect>();
        AddNumProjectiles(item, effects);
        AddProjectile(item, itemData, effects);
        AddActivate(item, effects);
        AddActivateOnEquip(item, itemData, effects);
        AddCooldown(item, itemData, effects);
        AddDoses(item, effects);
        AddMpCost(item, effects);
        AddFameBonus(item, itemData, effects);

        if (item.ExtraTooltipData.Length != 0) {
            var unique = new List<TooltipEffect>();
            foreach (var info in item.ExtraTooltipData)
                unique.Add(new TooltipEffect(info.Name, [Value(info.Description)]));
            unique.AddRange(effects);
            return unique;
        }
        return effects;
    }

    public static List<TooltipRestriction> BuildRestrictions(ItemDesc item, int itemData,
        PlayerTooltipContext player, string ownerType, IReadOnlyList<string> usableBy,
        string specialKeyLabel) {
        var restrictions = new List<TooltipRestriction>();
        var canUse = player != null && PlayerCanUse(item, player);
        var inventoryFull = player?.IsInventoryFull ?? false;

        if (canUse) {
            if (item.Usable) {
                restrictions.Add(new TooltipRestriction(
                    $"Press [{specialKeyLabel}] in world to use", TooltipPalette.White, false));
                AddEquipmentRestrictions(restrictions, inventoryFull, ownerType);
            }
            else if (item.Consumable) {
                restrictions.Add(new TooltipRestriction("Consumed with use", TooltipPalette.Body, false));
                restrictions.Add(new TooltipRestriction(
                    inventoryFull || ownerType == TooltipOwnerTypes.CurrentPlayer
                        ? "Double-Click or Shift-Click on item to use"
                        : "Double-Click to take & Shift-Click to use",
                    TooltipPalette.White, false));
            }
            else if (item.InvUse) {
                restrictions.Add(new TooltipRestriction("Can be used multiple times", TooltipPalette.Body, false));
                restrictions.Add(new TooltipRestriction("Double-Click or Shift-Click on item to use",
                    TooltipPalette.White, false));
            }
            else {
                AddEquipmentRestrictions(restrictions, inventoryFull, ownerType);
            }
        }
        else if (player != null) {
            restrictions.Add(new TooltipRestriction($"Not usable by {player.ClassDisplayName}",
                TooltipPalette.TitleUnusable, true));
        }

        if (usableBy != null)
            restrictions.Add(new TooltipRestriction($"Usable by: {string.Join(", ", usableBy)}",
                TooltipPalette.Body, false));

        foreach (var req in item.EquipRequirements) {
            if (req.Kind != "Stat")
                continue;
            var met = player != null && StatsUtil.MeetsRequirement(req.Stat, req.Value,
                player.MaxHp, player.MaxMp, player.Level, player.Attack, player.Defense,
                player.Speed, player.Vitality, player.Wisdom, player.Dexterity);
            restrictions.Add(new TooltipRestriction(
                $"Requires {StatsUtil.RequirementStatToName(req.Stat)} of {req.Value}",
                met ? TooltipPalette.Body : TooltipPalette.TitleUnusable, !met));
        }

        return restrictions;
    }

    private static void AddEquipmentRestrictions(List<TooltipRestriction> restrictions,
        bool inventoryFull, string ownerType) {
        restrictions.Add(new TooltipRestriction("Must be equipped to use", TooltipPalette.Body, false));
        restrictions.Add(new TooltipRestriction(
            inventoryFull || ownerType == TooltipOwnerTypes.CurrentPlayer
                ? "Double-Click to equip"
                : "Double-Click to take",
            TooltipPalette.Body, false));
    }

    private static TooltipValueSegment Value(string text) {
        return new TooltipValueSegment(text, TooltipPalette.EffectValue);
    }

    private static TooltipValueSegment Bonus(string text, int itemData) {
        var color = ItemData.GetColor(itemData);
        return new TooltipValueSegment(text,
            color == -1 ? TooltipPalette.EffectValue : (uint)color);
    }

    private static void AddNumProjectiles(ItemDesc item, List<TooltipEffect> effects) {
        if (item.HasNumProjectiles)
            effects.Add(new TooltipEffect("Shots", [Value(item.NumProjectiles.ToString())]));
    }

    private static void AddProjectile(ItemDesc item, int itemData, List<TooltipEffect> effects) {
        var proj = item.Projectiles;
        if (proj == null)
            return;

        var dmgMod = ItemData.GetStat(itemData, ItemData.DamageBit, ItemData.DamageMultiplier);
        var minDmg = proj.MinDamage + (int)(proj.MinDamage * dmgMod);
        var maxDmg = proj.MaxDamage + (int)(proj.MaxDamage * dmgMod);
        var dmgText = minDmg == maxDmg ? minDmg.ToString() : $"{minDmg} - {maxDmg}";
        if (dmgMod != 0)
            effects.Add(new TooltipEffect("Damage",
                [Value(dmgText), Bonus($" (+{(int)(dmgMod * 100)}%)", itemData)]));
        else
            effects.Add(new TooltipEffect("Damage", [Value(dmgText)]));

        var range = proj.Speed * proj.LifetimeMS / 10000.0;
        effects.Add(new TooltipEffect("Range", [Value(FormatValue(range))]));

        if (proj.MultiHit)
            effects.Add(new TooltipEffect("", [Value("Shots hit multiple targets")]));
        if (proj.PassesCover)
            effects.Add(new TooltipEffect("", [Value("Shots pass through obstacles")]));

        var rateOfFire = item.RateOfFire;
        var rofData = ItemData.GetStat(itemData, ItemData.RateOfFireBit,
            ItemData.RateOfFireMultiplier * rateOfFire);
        var rofText = $"{(int)(rateOfFire * 100) + (int)(rofData * 100)}%";
        if (rofData != 0)
            effects.Add(new TooltipEffect("Rate of Fire",
                [Value(rofText), Bonus($" (+{(int)(rofData * 100)}%)", itemData)]));
        else
            effects.Add(new TooltipEffect("Rate of Fire", [Value(rofText)]));

        foreach (var cond in proj.Effects)
            effects.Add(new TooltipEffect("Shot Effect",
                [Value($"{cond.EffectName} for {FormatValue(cond.DurationMS / 1000.0)} secs")]));
    }

    private static void AddActivate(ItemDesc item, List<TooltipEffect> effects) {
        foreach (var activate in item.ActivateEffects) {
            switch (activate.Effect) {
                case "Dye":
                    effects.Add(new TooltipEffect("", [Value("Changes texture of your character")]));
                    break;
                case "ConditionEffectAura":
                    effects.Add(new TooltipEffect("Party Effect",
                        [Value($"Within {FormatValue(activate.Range)} sqrs")]));
                    effects.Add(new TooltipEffect("",
                        [Value($"  {activate.ConditionEffect} for {FormatValue(activate.DurationSec)} secs")]));
                    break;
                case "ConditionEffectSelf":
                    effects.Add(new TooltipEffect("Effect on Self", [Value("")]));
                    effects.Add(new TooltipEffect("",
                        [Value($"  {activate.ConditionEffect} for {FormatValue(activate.DurationSec)} secs")]));
                    break;
                case "Heal":
                    effects.Add(new TooltipEffect("", [Value($"+{activate.Amount} HP")]));
                    break;
                case "HealNova":
                    effects.Add(new TooltipEffect("Party Heal",
                        [Value($"{activate.Amount} HP at {FormatValue(activate.Range)} sqrs")]));
                    break;
                case "Magic":
                    effects.Add(new TooltipEffect("", [Value($"+{activate.Amount} MP")]));
                    break;
                case "MagicNova":
                    effects.Add(new TooltipEffect("Fill Party Magic",
                        [Value($"{activate.Amount} MP at {FormatValue(activate.Range)} sqrs")]));
                    break;
                case "Teleport":
                    effects.Add(new TooltipEffect("", [Value("Teleport to Target")]));
                    break;
                case "VampireBlast":
                    effects.Add(new TooltipEffect("Steal",
                        [Value($"{activate.TotalDamage} HP within {FormatValue(activate.Radius)} sqrs")]));
                    break;
                case "Trap":
                    effects.Add(new TooltipEffect("Trap",
                        [Value($"{activate.TotalDamage} HP within {FormatValue(activate.Radius)} sqrs")]));
                    effects.Add(new TooltipEffect("",
                        [Value($"  {activate.ConditionEffect} for {FormatValue(activate.DurationSec)} secs")]));
                    break;
                case "StasisBlast":
                    effects.Add(new TooltipEffect("Stasis on Group",
                        [Value($"{FormatValue(activate.DurationSec)} secs")]));
                    break;
                case "Decoy":
                    effects.Add(new TooltipEffect("Decoy",
                        [Value($"{FormatValue(activate.DurationSec)} secs")]));
                    break;
                case "Lightning":
                    effects.Add(new TooltipEffect("Lightning", [Value("")]));
                    effects.Add(new TooltipEffect("",
                        [Value($" {activate.TotalDamage} to {activate.MaxTargets} targets")]));
                    break;
                case "PoisonGrenade":
                    effects.Add(new TooltipEffect("Poison Grenade", [Value("")]));
                    effects.Add(new TooltipEffect("",
                        [Value($" {activate.TotalDamage} HP over {FormatValue(activate.DurationSec)} secs within {FormatValue(activate.Radius)} sqrs")]));
                    break;
                case "RemoveNegativeConditions":
                case "RemoveNegativeConditionsSelf":
                    effects.Add(new TooltipEffect("", [Value("Removes negative conditions")]));
                    break;
                case "BulletNova":
                    effects.Add(new TooltipEffect("Shots", [Value("20")]));
                    break;
                case "Shuriken":
                    effects.Add(new TooltipEffect("Shots", [Value(activate.Amount.ToString())]));
                    effects.Add(new TooltipEffect("", [Value("Stars seek nearby enemies")]));
                    effects.Add(new TooltipEffect("", [Value("Dazes nearby enemies")]));
                    break;
                case "IncrementStat": {
                    var stat = StatsUtil.StatToBoostIndex(activate.Stats);
                    if (stat == -1)
                        break;
                    var val = stat != 0 && stat != 1
                        ? $"Permanently increases {StatsUtil.BoostSlotToName(stat)}"
                        : $"+{activate.Amount} {StatsUtil.BoostSlotToName(stat)}";
                    effects.Add(new TooltipEffect("", [Value(val)]));
                    break;
                }
            }
        }
    }

    private static void AddActivateOnEquip(ItemDesc item, int itemData, List<TooltipEffect> effects) {
        var stats = new Dictionary<int, int>();
        var datas = new Dictionary<int, int>();

        foreach (var boost in item.StatBoosts) {
            var stat = StatsUtil.StatToBoostIndex(boost.Stat);
            if (stat == -1)
                continue;
            stats[stat] = stats.TryGetValue(stat, out var cur) ? cur + boost.Amount : boost.Amount;
        }

        if (itemData != -1) {
            AddItemDataStat(itemData, ItemData.MaxHpBit, 5, 0, stats, datas);
            AddItemDataStat(itemData, ItemData.MaxMpBit, 5, 1, stats, datas);
            AddItemDataStat(itemData, ItemData.AttackBit, 1, 2, stats, datas);
            AddItemDataStat(itemData, ItemData.DefenseBit, 1, 3, stats, datas);
            AddItemDataStat(itemData, ItemData.SpeedBit, 1, 4, stats, datas);
            AddItemDataStat(itemData, ItemData.DexterityBit, 1, 5, stats, datas);
            AddItemDataStat(itemData, ItemData.VitalityBit, 1, 6, stats, datas);
            AddItemDataStat(itemData, ItemData.WisdomBit, 1, 7, stats, datas);
        }

        if (stats.Count == 0)
            return;

        effects.Add(new TooltipEffect("On Equip", [Value("")]));
        foreach (var slot in stats.Keys.OrderBy(s => s)) {
            var data = datas.TryGetValue(slot, out var d) ? d : 0;
            effects.Add(new TooltipEffect("", [IncrementStatTag(slot, stats[slot], data, itemData)]));
        }
    }

    private static void AddItemDataStat(int itemData, uint bit, double multiplier, int slot,
        Dictionary<int, int> stats, Dictionary<int, int> datas) {
        var k = (int)ItemData.GetStat(itemData, bit, multiplier);
        if (k == 0)
            return;
        stats[slot] = stats.TryGetValue(slot, out var cur) ? cur + k : k;
        datas[slot] = datas.TryGetValue(slot, out var curData) ? curData + k : k;
    }

    private static TooltipValueSegment IncrementStatTag(int stat, int amount, int data, int itemData) {
        var amountString = amount > -1 ? $"+{amount}" : amount.ToString();
        var textColor = TooltipPalette.EffectValue;
        if (amount <= -1)
            textColor = TooltipPalette.Negative;
        var dataString = "";
        if (data > 0) {
            dataString = $" (+{data})";
            var bonus = ItemData.GetColor(itemData);
            textColor = bonus == -1 ? TooltipPalette.EffectValue : (uint)bonus;
        }
        return new TooltipValueSegment(
            $"{amountString}{dataString} {StatsUtil.BoostSlotToName(stat)}", textColor);
    }

    private static void AddCooldown(ItemDesc item, int itemData, List<TooltipEffect> effects) {
        var mod = ItemData.GetStat(itemData, ItemData.CooldownBit, ItemData.CooldownMultiplier);
        if (!item.HasCooldown && mod == 0)
            return;
        var cooldown = item.HasCooldown ? item.Cooldown : 0.2f;
        var text = $"{FormatValue(cooldown - cooldown * mod)}s";
        if (mod != 0)
            effects.Add(new TooltipEffect("Cooldown",
                [Value(text), Bonus($" (-{(int)(mod * 100)}%)", itemData)]));
        else
            effects.Add(new TooltipEffect("Cooldown", [Value(text)]));
    }

    private static void AddDoses(ItemDesc item, List<TooltipEffect> effects) {
        if (item.HasDoses)
            effects.Add(new TooltipEffect("Doses", [Value(item.Doses.ToString())]));
    }

    private static void AddMpCost(ItemDesc item, List<TooltipEffect> effects) {
        if (item.HasMpCost)
            effects.Add(new TooltipEffect("MP Cost", [Value(item.MpCost.ToString())]));
    }

    private static void AddFameBonus(ItemDesc item, int itemData, List<TooltipEffect> effects) {
        var mod = ItemData.GetStat(itemData, ItemData.FameBonusBit, 1);
        if (!item.HasFameBonus && mod == 0)
            return;
        var fameBonus = item.HasFameBonus ? item.FameBonus : 0;
        var text = $"{fameBonus + (int)mod}%";
        if (mod != 0)
            effects.Add(new TooltipEffect("Fame Bonus",
                [Value(text), Bonus($" (+{(int)mod}%)", itemData)]));
        else
            effects.Add(new TooltipEffect("Fame Bonus", [Value(text)]));
    }
}
