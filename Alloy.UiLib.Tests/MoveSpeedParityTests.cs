using AlloyClient.Game.Objects;

namespace Alloy.UiLib.Tests;

/// <summary>Flash parity for Player.getMoveSpeed: the Speed stat must scale
/// movement between MinMoveSpeed (0) and MaxMoveSpeed (75), with Slowed
/// pinning to the minimum and Speedy multiplying by 1.5.</summary>
internal static class MoveSpeedParityTests {
    internal static void Run() {
        BaseScalesWithStat();
        MidStatGivesMidSpeed();
        SlowedPinsToMinimum();
        SpeedyMultiplies();
        GroundMultiplierApplies();
    }

    // Speed 0 walks at the minimum, 75 at the maximum.
    private static void BaseScalesWithStat() {
        Equal(0.004f, Player.ComputeMoveSpeed(0, false, false, 1f), 1e-6f);
        Equal(0.0096f, Player.ComputeMoveSpeed(75, false, false, 1f), 1e-6f);
    }

    // A mid-range stat must give a mid-range speed (integer division made
    // every stat below 75 collapse to the minimum).
    private static void MidStatGivesMidSpeed() {
        var mid = Player.ComputeMoveSpeed(37, false, false, 1f);
        if (mid <= 0.004f || mid >= 0.0096f) {
            throw new Exception($"speed 37 should scale, got {mid}");
        }
        Equal(0.004f + 37f / 75f * (0.0096f - 0.004f), mid, 1e-6f);
    }

    // Slowed pins to the minimum regardless of the stat.
    private static void SlowedPinsToMinimum() {
        Equal(0.004f, Player.ComputeMoveSpeed(75, true, false, 1f), 1e-6f);
        Equal(0.004f, Player.ComputeMoveSpeed(50, true, false, 1f), 1e-6f);
    }

    // Speedy multiplies the scaled speed by 1.5.
    private static void SpeedyMultiplies() {
        var plain = Player.ComputeMoveSpeed(50, false, false, 1f);
        Equal(plain * 1.5f, Player.ComputeMoveSpeed(50, false, true, 1f), 1e-6f);
    }

    // The ground multiplier (Sink/slow tiles) scales the result.
    private static void GroundMultiplierApplies() {
        var full = Player.ComputeMoveSpeed(50, false, false, 1f);
        Equal(full * 0.666f, Player.ComputeMoveSpeed(50, false, false, 0.666f), 1e-6f);
    }

    private static void Equal(float expected, float actual, float tolerance) {
        if (MathF.Abs(expected - actual) > tolerance) {
            throw new Exception($"expected {expected}, got {actual}");
        }
    }
}
