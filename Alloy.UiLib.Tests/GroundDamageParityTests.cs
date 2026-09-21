using System.Xml.Linq;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Game.Objects;

namespace Alloy.UiLib.Tests;

/// <summary>Flash parity for Player.onMove's ground branch: damaging ground
/// (Lava, etc.) hurts unless invincible or covered by a
/// ProtectFromGroundDamage object; safe ground never does.</summary>
internal static class GroundDamageParityTests {
    internal static void Run() {
        DamagingGroundHurts();
        SafeGroundDoesNot();
        InvincibleIgnoresGround();
        CoverProtects();
        CoverFlagParses();
    }

    // Lava deals 30-60: an exposed, mortal player takes ground damage.
    private static void DamagingGroundHurts() {
        Equal(true, Player.TakesGroundDamage(30, 60, invincible: false, covered: false));
        Equal(true, Player.TakesGroundDamage(25, 40, invincible: false, covered: false));
    }

    // Ordinary ground has no damage range.
    private static void SafeGroundDoesNot() {
        Equal(false, Player.TakesGroundDamage(0, 0, invincible: false, covered: false));
    }

    // Flash skips ground damage while invincible.
    private static void InvincibleIgnoresGround() {
        Equal(false, Player.TakesGroundDamage(30, 60, invincible: true, covered: false));
    }

    // Flash skips ground damage under a ProtectFromGroundDamage object.
    private static void CoverProtects() {
        Equal(false, Player.TakesGroundDamage(30, 60, invincible: false, covered: true));
    }

    // The cover flag mirrors Flash ObjectProperties parsing.
    private static void CoverFlagParses() {
        var cover = new ObjectProperties(XElement.Parse(
            "<Object type=\"0x153\" id=\"Partial Red Floor\"><ProtectFromGroundDamage/></Object>"));
        Equal(true, cover.ProtectFromGroundDamage);

        var plain = new ObjectProperties(XElement.Parse(
            "<Object type=\"0x160\" id=\"Blue Wall\"/>"));
        Equal(false, plain.ProtectFromGroundDamage);
    }

    private static void Equal<T>(T expected, T actual) {
        if (!Equals(expected, actual)) {
            throw new Exception($"expected {expected}, got {actual}");
        }
    }
}
