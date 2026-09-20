using System.Collections.Generic;
using AlloyClient.Game;

namespace Alloy.UiLib.Tests;

//The realm-server numbers conditions Nothing=0, Quiet=1, ... Speedy=14
//(matching the Flash ConditionEffect.as) and sends bit (index-1) in the
//Condition stat. This client numbers None=0, Dead=1, Quiet=2, ... Speedy=15.
//The intake must translate by effect, or every effect reads one slot off:
//a channeled Speedy (server bit 13) rendered as Invisible.
internal static class ConditionMaskTests {
    public static void Run() {
        ServerSpeedyReadsAsSpeedy();
        ServerInvisibleReadsAsInvisible();
        FullServerMaskRoundTripsByName();
        UnknownBitsAreDropped();
        IconCountMatchesDrawnQuads();
    }

    private static void ServerSpeedyReadsAsSpeedy() {
        //Server Speedy = index 14 -> bit 13.
        var mask = ConditionEffects.TranslateServerMask(1 << 13);
        Equal(1 << (int)ConditionEffect.Speedy, mask);
        Equal(0, mask & (1 << (int)ConditionEffect.Invisible));
    }

    private static void ServerInvisibleReadsAsInvisible() {
        //Server Invisible = index 12 -> bit 11.
        var mask = ConditionEffects.TranslateServerMask(1 << 11);
        Equal(1 << (int)ConditionEffect.Invisible, mask);
        Equal(0, mask & (1 << (int)ConditionEffect.Speedy));
    }

    private static void FullServerMaskRoundTripsByName() {
        //Server bit -> client enum, in server index order Quiet..Hexed.
        var expected = new ConditionEffect[] {
            ConditionEffect.Quiet, ConditionEffect.Weak, ConditionEffect.Slowed,
            ConditionEffect.Sick, ConditionEffect.Dazed, ConditionEffect.Stunned,
            ConditionEffect.Blind, ConditionEffect.Hallucinating, ConditionEffect.Drunk,
            ConditionEffect.Confused, ConditionEffect.StunImmune, ConditionEffect.Invisible,
            ConditionEffect.Paralyzed, ConditionEffect.Speedy, ConditionEffect.Bleeding,
            ConditionEffect.Healing, ConditionEffect.Damaging, ConditionEffect.Berserk,
            ConditionEffect.Stasis, ConditionEffect.StasisImmune, ConditionEffect.Invincible,
            ConditionEffect.Invulnerable, ConditionEffect.Armored, ConditionEffect.ArmorBroken,
            ConditionEffect.Hexed,
        };
        Equal(25, expected.Length);
        for (var b = 0; b < expected.Length; b++)
            Equal(1 << (int)expected[b], ConditionEffects.TranslateServerMask(1 << b));
    }

    private static void UnknownBitsAreDropped() {
        //The server never sends bits past index 25 (bit 24).
        Equal(0, ConditionEffects.TranslateServerMask(1 << 30));
        Equal(0, ConditionEffects.TranslateServerMask(0));
    }

    private static void IconCountMatchesDrawnQuads() {
        //TotalIcons sizes the GetEffectsData span, so it must count exactly
        //the bits that have an icon entry. Server Speedy draws one icon.
        Equal(1, ConditionEffects.CountIcons(0, ConditionEffects.TranslateServerMask(1 << 13)));
        //Iconless server effects (StunImmune bit 10, Invisible bit 11) draw none.
        Equal(0, ConditionEffects.CountIcons(0, ConditionEffects.TranslateServerMask((1 << 10) | (1 << 11))));
        //All 25 server effects draw 20 icons (StunImmune, Invisible, Stasis,
        //StasisImmune and Invincible are iconless, like Flash).
        Equal(20, ConditionEffects.CountIcons(0, ConditionEffects.TranslateServerMask((1 << 25) - 1)));
        //Client-only bits without icons (Dead, ArmorBrokenImmune) draw none.
        Equal(0, ConditionEffects.CountIcons(0, (1 << (int)ConditionEffect.Dead) | (1 << (int)ConditionEffect.ArmorBrokenImmune)));
        //Unknown high bits draw none.
        Equal(0, ConditionEffects.CountIcons(0, 1 << 30));
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected {expected}, got {actual}.");
    }
}
