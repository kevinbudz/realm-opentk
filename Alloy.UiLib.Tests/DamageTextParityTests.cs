using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Ui.Character;

namespace Alloy.UiLib.Tests;

/// <summary>Flash parity for damage numbers (CharacterStatusText.as +
/// GameObject.damage): short upward drift, opaque until pop-out, and purple
/// for armor-piercing hits.</summary>
internal static class DamageTextParityTests {
    internal static void Run() {
        DriftMatchesFlash();
        StaysOpaqueThenPops();
        PierceColorsMatchFlash();
        GlowMatchesFlash();
        ResolveDamageColor();
        ArmorBrokenWireId();
        ServerEffectMapping();
        StunImmunityRule();
        EffectNames();
        ScaleTracksMapZoom();
        DefenseSubtracts();
        MinDamageFloor();
        PierceIgnoresDefense();
        ArmorBrokenIgnoresDefense();
        ArmoredDoublesDefense();
        InvulnerableZeroes();
        DefenseParsesFromXml();
    }

    // CharacterStatusText.MAX_DRIFT = 20: drift = dt / lifetime * 20.
    private static void DriftMatchesFlash() {
        Equal(0.0, CharacterStatusText.ComputeVerticalDrift(0.0), 1e-9);
        Equal(10.0, CharacterStatusText.ComputeVerticalDrift(0.5), 1e-9);
        Equal(20.0, CharacterStatusText.ComputeVerticalDrift(1.0), 1e-9);
    }

    // CharacterStatusText.draw: alpha = 1 / (drift / 15), so the number stays
    // fully opaque until 75% of its life, then pops out (15/20 = 0.75) at the
    // end instead of fading linearly.
    private static void StaysOpaqueThenPops() {
        Equal(1f, CharacterStatusText.ComputeAlpha(0.0), 1e-6f);
        Equal(1f, CharacterStatusText.ComputeAlpha(0.5), 1e-6f);
        Equal(1f, CharacterStatusText.ComputeAlpha(0.75), 1e-6f);
        Equal(0.75f, CharacterStatusText.ComputeAlpha(1.0), 1e-6f);
    }

    // GameObject.damage: pierced ? 0x9000FF : 0xFF0000.
    private static void PierceColorsMatchFlash() {
        Equal(0xFF0000u, CharacterStatusText.NormalColor);
        Equal(0x9000FFu, CharacterStatusText.PiercedColor);
    }

    // CharacterStatusText: t.filters = [new GlowFilter(0, 1, 4, 4, 2, 1)].
    private static void GlowMatchesFlash() {
        var glow = CharacterStatusText.DamageGlow;
        Equal(0f, glow.Distance, 1e-6f);
        Equal(0u, glow.Color);
        Equal(1f, glow.Alpha, 1e-6f);
        Equal(4f, glow.BlurX, 1e-6f);
        Equal(4f, glow.BlurY, 1e-6f);
        Equal(2f, glow.Strength, 1e-6f);
        Equal(1, glow.Quality);
    }

    private static void ResolveDamageColor() {
        var clean = new Entity();
        Equal(CharacterStatusText.NormalColor, CharacterStatusText.ResolveDamageColor(clean, false));
        Equal(CharacterStatusText.PiercedColor, CharacterStatusText.ResolveDamageColor(clean, true));

        var broken = new Entity();
        broken.EffectBuckets.SetBucket(0, 1 << (int)ConditionEffect.ArmorBroken);
        Equal(CharacterStatusText.PiercedColor, CharacterStatusText.ResolveDamageColor(broken, false));
    }

    // Damage/Aoe effect bytes carry the server's ConditionEffectIndex
    // (realm-server/Common/Descriptors.cs: ArmorBroken = 24), not this
    // client's Dead-shifted enum (ArmorBroken = 27).
    private static void ArmorBrokenWireId() {
        Equal(true, CharacterStatusText.IsArmorBrokenEffect(24));
        Equal(false, CharacterStatusText.IsArmorBrokenEffect(0));
        Equal(false, CharacterStatusText.IsArmorBrokenEffect(25));
        Equal(false, CharacterStatusText.IsArmorBrokenEffect(27));
        Equal(false, CharacterStatusText.IsArmorBrokenEffect(99));
    }

    // Map-scale parity (SpeechBubble.Update sets Scale = CameraZoom): the
    // number grows/shrinks with scroll zoom instead of staying fixed.
    private static void ScaleTracksMapZoom() {
        Equal(0.5f, CharacterStatusText.ComputeScale(0.5f), 1e-6f);
        Equal(1f, CharacterStatusText.ComputeScale(1f), 1e-6f);
        Equal(2f, CharacterStatusText.ComputeScale(2f), 1e-6f);
    }

    // GameObject.damageWithDefense + realm-server GetDefenseDamage: the
    // number above the player subtracts defense (100 - 20 = 80).
    private static void DefenseSubtracts() {
        Equal(80, Entity.DamageWithDefense(100, 20, false, false, false, false));
    }

    // Damage/Aoe effect bytes are realm-server ConditionEffectIndex
    // (Nothing = 0 .. Hexed = 25). Nothing and betterskillys-only ids past
    // Hexed (GroundDamage/Exposed/Curse/...) map to nothing and are dropped,
    // since this server never sends them.
    private static void ServerEffectMapping() {
        Equal(false, ConditionEffects.TryMapServerEffect(0, out _));
        Equal(true, ConditionEffects.TryMapServerEffect(1, out var quiet));
        Equal(ConditionEffect.Quiet, quiet);
        Equal(true, ConditionEffects.TryMapServerEffect(6, out var stunned));
        Equal(ConditionEffect.Stunned, stunned);
        Equal(true, ConditionEffects.TryMapServerEffect(22, out var invuln));
        Equal(ConditionEffect.Invulnerable, invuln);
        Equal(true, ConditionEffects.TryMapServerEffect(23, out var armored));
        Equal(ConditionEffect.Armored, armored);
        Equal(true, ConditionEffects.TryMapServerEffect(24, out var broken));
        Equal(ConditionEffect.ArmorBroken, broken);
        Equal(true, ConditionEffects.TryMapServerEffect(25, out var hexed));
        Equal(ConditionEffect.Hexed, hexed);
        Equal(false, ConditionEffects.TryMapServerEffect(26, out _));
        Equal(false, ConditionEffects.TryMapServerEffect(99, out _));
    }

    // The only immunity this server enforces (ApplyConditionEffect) is
    // Stunned blocked by StunImmune. Betterskillys-only immunities
    // (Slowed/ArmorBroken/Dazed/Paralyze/Petrified/Curse) have no server
    // counterpart and never block.
    private static void StunImmunityRule() {
        Equal(true, CharacterStatusText.IsImmuneBlocked(ConditionEffect.Stunned, true));
        Equal(false, CharacterStatusText.IsImmuneBlocked(ConditionEffect.Stunned, false));
        Equal(false, CharacterStatusText.IsImmuneBlocked(ConditionEffect.ArmorBroken, true));
        Equal(false, CharacterStatusText.IsImmuneBlocked(ConditionEffect.Slowed, true));
        Equal(false, CharacterStatusText.IsImmuneBlocked(ConditionEffect.Paralyzed, true));
    }

    // Red condition-name texts use the EffectTable names (GameObject.damage
    // ce.name_).
    private static void EffectNames() {
        Equal("Armor Broken", ConditionEffects.GetEffectName(ConditionEffect.ArmorBroken));
        Equal("Stunned", ConditionEffects.GetEffectName(ConditionEffect.Stunned));
        Equal("Quiet", ConditionEffects.GetEffectName(ConditionEffect.Quiet));
    }

    // min = orig * 3 / 20: 100 damage vs 200 defense floors at 15, not 0.
    // Server-scoped on purpose: realm-server GetDefenseDamage uses 3/20 and
    // Armored x2, so predictions match the authoritative Damage packets.
    // Betterskillys' 2/20, Armored x1.5, Exposed -20, Petrified x0.9 and
    // Curse x1.25 are omitted: no such effects exist server-side.
    private static void MinDamageFloor() {
        Equal(15, Entity.DamageWithDefense(100, 200, false, false, false, false));
        Equal(15, Entity.DamageWithDefense(100, 85, false, false, false, false));
        Equal(16, Entity.DamageWithDefense(100, 84, false, false, false, false));
    }

    // Armor-piercing shots ignore defense entirely.
    private static void PierceIgnoresDefense() {
        Equal(100, Entity.DamageWithDefense(100, 50, true, false, false, false));
    }

    // ArmorBroken forces pierce, like Player/Enemy.Damage does.
    private static void ArmorBrokenIgnoresDefense() {
        Equal(100, Entity.DamageWithDefense(100, 50, false, true, false, false));
    }

    // Armored doubles defense: 100 - 2*20 = 60.
    private static void ArmoredDoublesDefense() {
        Equal(60, Entity.DamageWithDefense(100, 20, false, false, true, false));
    }

    // Invulnerable zeroes even a piercing hit (Flash shows no text for 0).
    private static void InvulnerableZeroes() {
        Equal(0, Entity.DamageWithDefense(100, 20, false, false, false, true));
        Equal(0, Entity.DamageWithDefense(100, 20, true, false, false, true));
    }

    // Enemies keep their XML defense (Flash GameObject defense_ init), so
    // player->enemy numbers subtract it too.
    private static void DefenseParsesFromXml() {
        var props = new AlloyClient.Assets.XmlStructs.ObjectProperties(
            System.Xml.Linq.XElement.Parse("<Object type=\"0x11\" id=\"Test\"><Defense>20</Defense></Object>"));
        Equal(20, props.Defense);
    }

    private static void Equal(double expected, double actual, double tolerance) {
        if (Math.Abs(expected - actual) > tolerance) {
            throw new Exception($"expected {expected}, got {actual}");
        }
    }

    private static void Equal(float expected, float actual, float tolerance) {
        if (MathF.Abs(expected - actual) > tolerance) {
            throw new Exception($"expected {expected}, got {actual}");
        }
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
            throw new Exception($"expected {expected}, got {actual}");
        }
    }
}
