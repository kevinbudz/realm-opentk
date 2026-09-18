using System.Xml.Linq;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Game.Objects.Util;
using AlloyClient.Ui.Components.Tooltips;

namespace Alloy.UiLib.Tests;

internal static class EquipmentTooltipTests {
    private const string SwordXml = """
        <Object type="1000" id="Test Sword">
          <Class>Equipment</Class>
          <Item/>
          <SlotType>1</SlotType>
          <Description>A test sword.</Description>
          <Tier>5</Tier>
          <RateOfFire>1</RateOfFire>
          <NumProjectiles>3</NumProjectiles>
          <Projectile>
            <Speed>90</Speed>
            <LifetimeMS>1000</LifetimeMS>
            <MinDamage>40</MinDamage>
            <MaxDamage>55</MaxDamage>
          </Projectile>
          <Activate amount="30" range="3">HealNova</Activate>
          <ActivateOnEquip stat="20" amount="2">IncrementStat</ActivateOnEquip>
          <MpCost>100</MpCost>
          <FameBonus>4</FameBonus>
        </Object>
        """;

    private static ItemDesc Sword() => new(1000, XElement.Parse(SwordXml));

    private static PlayerTooltipContext Rogue(bool full = false) => new(
        [1], "Rogue", 100, 100, 10, 5, 10, 10, 10, 10, 1, full);

    public static void Run() {
        TierTags();
        TitleColors();
        EffectOrderAndText();
        ItemDataBonuses();
        CooldownDefault();
        ExtraTooltipFirst();
        Restrictions();
        Requirements();
        StatHelpers();
        CollapseWhitespace();
        TooltipLayout();
    }

    private static void TierTags() {
        var tag = EquipmentTooltipBuilder.GetTierTag(Sword());
        Equal("T5", tag.Value.Text);
        Equal(0xFFFFFFu, tag.Value.Color);

        var untiered = new ItemDesc(1, XElement.Parse(
            """<Object type="1" id="U"><Class>Equipment</Class><Item/><SlotType>1</SlotType></Object>"""));
        Equal("UT", EquipmentTooltipBuilder.GetTierTag(untiered).Value.Text);
        Equal(0x8A2BE2u, EquipmentTooltipBuilder.GetTierTag(untiered).Value.Color);

        var set = new ItemDesc(2, XElement.Parse(
            """<Object type="2" id="S" setType="0x0001"><Class>Equipment</Class><Item/><SlotType>1</SlotType></Object>"""));
        Equal("ST", EquipmentTooltipBuilder.GetTierTag(set).Value.Text);
        Equal(0xFF9900u, EquipmentTooltipBuilder.GetTierTag(set).Value.Color);

        var consumable = new ItemDesc(3, XElement.Parse(
            """<Object type="3" id="C"><Class>Equipment</Class><Item/><SlotType>1</SlotType><Consumable/></Object>"""));
        Equal(null, EquipmentTooltipBuilder.GetTierTag(consumable));

        var permaPet = new ItemDesc(4, XElement.Parse(
            """<Object type="4" id="P"><Class>Equipment</Class><Item/><SlotType>1</SlotType><Activate>PermaPet</Activate></Object>"""));
        Equal(null, EquipmentTooltipBuilder.GetTierTag(permaPet));
    }

    private static void TitleColors() {
        Equal(0xFFFFFFu, EquipmentTooltipBuilder.GetTitleColor(Sword(), -1, null));
        Equal(0xFFFFFFu, EquipmentTooltipBuilder.GetTitleColor(Sword(), -1, Rogue()));
        var warrior = Rogue() with { SlotTypes = [2], ClassDisplayName = "Warrior" };
        Equal(16549442u, EquipmentTooltipBuilder.GetTitleColor(Sword(), -1, warrior));
        // T0 rank bit tints the title blue.
        Equal(0x00a6ffu, EquipmentTooltipBuilder.GetTitleColor(Sword(), 1, warrior));
    }

    private static void EffectOrderAndText() {
        var effects = EquipmentTooltipBuilder.BuildEffects(Sword(), -1);
        var names = string.Join("|", effects.Select(e => e.Name));
        Equal("Shots|Damage|Range|Rate of Fire|Party Heal|On Equip||MP Cost|Fame Bonus", names);
        Equal("3", effects[0].Value[0].Text);
        Equal("40 - 55", effects[1].Value[0].Text);
        Equal(0xFFFF8Fu, effects[1].Value[0].Color);
        Equal("9", effects[2].Value[0].Text);
        Equal("100%", effects[3].Value[0].Text);
        Equal("30 HP at 3 sqrs", effects[4].Value[0].Text);
        Equal("+2 Attack", effects[6].Value[0].Text);
        Equal(0xFFFF8Fu, effects[6].Value[0].Color);
        Equal("100", effects[7].Value[0].Text);
        Equal("4%", effects[8].Value[0].Text);
    }

    private static void ItemDataBonuses() {
        // T0 rank + damage bit: damage grows and the bonus run is blue.
        var effects = EquipmentTooltipBuilder.BuildEffects(Sword(), 1 | (1 << 17));
        Equal("42 - 57", effects[1].Value[0].Text);
        Equal(2, effects[1].Value.Length);
        Equal(" (+5%)", effects[1].Value[1].Text);
        Equal(0x00a6ffu, effects[1].Value[1].Color);

        // T1 rank + attack bit: XML +2 plus +2 data bonus.
        var onEquip = EquipmentTooltipBuilder.BuildEffects(Sword(), 2 | (1 << 10));
        var line = onEquip.First(e => e.Name == "" && e.Value[0].Text.Contains("Attack"));
        Equal("+4 (+2) Attack", line.Value[0].Text);
        Equal(0x7300ffu, line.Value[0].Color);
    }

    private static void CooldownDefault() {
        var item = new ItemDesc(5, XElement.Parse(
            """<Object type="5" id="W"><Class>Equipment</Class><Item/><SlotType>8</SlotType><Usable/><Cooldown>0.5</Cooldown></Object>"""));
        var effects = EquipmentTooltipBuilder.BuildEffects(item, -1);
        Equal("Cooldown", effects[0].Name);
        Equal("0.5s", effects[0].Value[0].Text);

        // No Cooldown element but a cooldown bonus: Flash falls back to 0.2s.
        var noCd = new ItemDesc(6, XElement.Parse(
            """<Object type="6" id="W2"><Class>Equipment</Class><Item/><SlotType>8</SlotType><Usable/></Object>"""));
        var modded = EquipmentTooltipBuilder.BuildEffects(noCd, 1 | (1 << 18));
        Equal("0.19s", modded[0].Value[0].Text);
        Equal(" (-5%)", modded[0].Value[1].Text);
    }

    private static void ExtraTooltipFirst() {
        var item = new ItemDesc(7, XElement.Parse(
            """<Object type="7" id="Acc"><Class>Equipment</Class><Item/><SlotType>10</SlotType><Consumable/><ExtraTooltipData><EffectInfo description="1 of 8" name="Snowman accessory"/></ExtraTooltipData><NumProjectiles>1</NumProjectiles></Object>"""));
        var effects = EquipmentTooltipBuilder.BuildEffects(item, -1);
        Equal("Snowman accessory", effects[0].Name);
        Equal("1 of 8", effects[0].Value[0].Text);
        Equal("Shots", effects[1].Name);
    }

    private static void Restrictions() {
        // Usable equipment on the current player.
        var r = EquipmentTooltipBuilder.BuildRestrictions(Sword(), -1, Rogue(),
            TooltipOwnerTypes.CurrentPlayer, null, "Space");
        Equal("Must be equipped to use|Double-Click to equip", string.Join("|", r.Select(x => x.Text)));

        // Same item viewed in another player's trade: take, not equip.
        r = EquipmentTooltipBuilder.BuildRestrictions(Sword(), -1, Rogue(),
            TooltipOwnerTypes.OtherPlayer, null, "Space");
        Equal("Double-Click to take", r[1].Text);

        // Ability item shows the special key plus equipment lines.
        var ability = new ItemDesc(8, XElement.Parse(
            """<Object type="8" id="Cloak"><Class>Equipment</Class><Item/><SlotType>13</SlotType><Usable/></Object>"""));
        var cloakUser = Rogue() with { SlotTypes = [13] };
        r = EquipmentTooltipBuilder.BuildRestrictions(ability, -1, cloakUser,
            TooltipOwnerTypes.CurrentPlayer, null, "Space");
        Equal("Press [Space] in world to use|Must be equipped to use|Double-Click to equip",
            string.Join("|", r.Select(x => x.Text)));
        Equal(16777215u, r[0].Color);

        // Consumable phrasing depends on inventory ownership.
        var potion = new ItemDesc(9, XElement.Parse(
            """<Object type="9" id="Pot"><Class>Equipment</Class><Item/><SlotType>10</SlotType><Consumable/></Object>"""));
        r = EquipmentTooltipBuilder.BuildRestrictions(potion, -1, Rogue(),
            TooltipOwnerTypes.CurrentPlayer, null, "Space");
        Equal("Consumed with use|Double-Click or Shift-Click on item to use",
            string.Join("|", r.Select(x => x.Text)));
        r = EquipmentTooltipBuilder.BuildRestrictions(potion, -1, Rogue(),
            TooltipOwnerTypes.Npc, null, "Space");
        Equal("Double-Click to take & Shift-Click to use", r[1].Text);

        // Wrong class: red bold line, no usage lines.
        var warrior = Rogue() with { SlotTypes = [2], ClassDisplayName = "Warrior" };
        r = EquipmentTooltipBuilder.BuildRestrictions(Sword(), -1, warrior,
            TooltipOwnerTypes.CurrentPlayer, null, "Space");
        Equal(1, r.Count);
        Equal("Not usable by Warrior", r[0].Text);
        Equal(16549442u, r[0].Color);
        Equal(true, r[0].Bold);

        // No player: no restrictions at all.
        r = EquipmentTooltipBuilder.BuildRestrictions(Sword(), -1, null,
            TooltipOwnerTypes.Npc, null, "Space");
        Equal(0, r.Count);

        // Usable-by roster line.
        r = EquipmentTooltipBuilder.BuildRestrictions(Sword(), -1, Rogue(),
            TooltipOwnerTypes.CurrentPlayer, ["Rogue", "Archer"], "Space");
        Equal("Usable by: Rogue, Archer", r[^1].Text);
    }

    private static void Requirements() {
        var item = new ItemDesc(10, XElement.Parse(
            """<Object type="10" id="Req"><Class>Equipment</Class><Item/><SlotType>1</SlotType><EquipRequirement stat="20" value="10">Stat</EquipRequirement></Object>"""));
        // Attack 10 meets the requirement: gray, not bold.
        var r = EquipmentTooltipBuilder.BuildRestrictions(item, -1, Rogue(),
            TooltipOwnerTypes.CurrentPlayer, null, "Space");
        Equal("Requires Attack of 10", r[^1].Text);
        Equal(11776947u, r[^1].Color);
        Equal(false, r[^1].Bold);

        // Attack 10 vs requirement 50: red and bold.
        var hard = new ItemDesc(11, XElement.Parse(
            """<Object type="11" id="Req2"><Class>Equipment</Class><Item/><SlotType>1</SlotType><EquipRequirement stat="20" value="50">Stat</EquipRequirement></Object>"""));
        r = EquipmentTooltipBuilder.BuildRestrictions(hard, -1, Rogue(),
            TooltipOwnerTypes.CurrentPlayer, null, "Space");
        Equal(16549442u, r[^1].Color);
        Equal(true, r[^1].Bold);
    }

    private static void StatHelpers() {
        Equal(2, StatsUtil.StatToBoostIndex(20));
        Equal(5, StatsUtil.StatToBoostIndex(28));
        Equal(6, StatsUtil.StatToBoostIndex(26));
        Equal(-1, StatsUtil.StatToBoostIndex(99));
        Equal("Attack", StatsUtil.BoostSlotToName(2));
        Equal("0.3", EquipmentTooltipBuilder.FormatValue(0.1 + 0.2));
        Equal("10", EquipmentTooltipBuilder.FormatValue(10));
        Equal(8, ItemData.GetRank((1 << 7) | (1 << 17)));
        Equal(0, ItemData.GetStat(-1, ItemData.DamageBit, 0.05));
    }

    private static void CollapseWhitespace() {
        // Pretty-printed game XML breaks descriptions across source lines;
        // the raw newlines/indentation must not reach the renderer.
        Equal("of the universe.",
            EquipmentTooltipBuilder.CollapseWhitespace("of the\n            universe.\n        "));
        Equal("A golden staff of transcendent understanding, made from crystals present at the formation of the universe.",
            EquipmentTooltipBuilder.CollapseWhitespace("A golden staff of transcendent understanding, made from crystals present at the formation of the\n            universe.\n        "));
        Equal("a b c", EquipmentTooltipBuilder.CollapseWhitespace("a  b\tc"));
        Equal("a b", EquipmentTooltipBuilder.CollapseWhitespace("  a b  "));
        Equal("", EquipmentTooltipBuilder.CollapseWhitespace(""));
        Equal(null, EquipmentTooltipBuilder.CollapseWhitespace(null));
    }

    private static void TooltipLayout() {
        // Title starts right of the 40px icon at (5,5) with Flash's 4px gap.
        Equal(49, EquipmentTooltipLayout.TitleX());
        // Tier tag right edge insets 6px from the 230px tooltip edge...
        Equal(224, EquipmentTooltipLayout.TierRightX());
        // ...so the title wraps before the measured tag (T12 is 27px wide).
        Equal(144, EquipmentTooltipLayout.TitleWidth(49, EquipmentTooltipLayout.TitleRight(224, 27)));
        // Without a tag the title may use the padded edge.
        Equal(169, EquipmentTooltipLayout.TitleWidth(49, EquipmentTooltipLayout.TitleRight(null, null)));
        // Title centers on the icon middle whatever its height...
        Equal(25, EquipmentTooltipLayout.TitleMiddleY());
        // ...and the tier centers there too, independent of either height.
        Equal(25, EquipmentTooltipLayout.TierMiddleY(25));
        // Single- and double-line titles keep the description tucked at 48...
        Equal(48, EquipmentTooltipLayout.DescY(25, 20));
        Equal(48, EquipmentTooltipLayout.DescY(25, 39));
        // ...while a taller title pushes it below its own bottom.
        Equal(58, EquipmentTooltipLayout.DescY(25, 58));
        // Effect label carries Flash's single trailing space, no extra pixels.
        Equal("Shots: ", EquipmentTooltipLayout.EffectLabel("Shots"));
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected {expected}, got {actual}.");
    }
}
