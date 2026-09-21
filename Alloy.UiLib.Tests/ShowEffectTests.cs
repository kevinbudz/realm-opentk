using Alloy.Engine;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Networking;
using AlloyClient.Networking.Enums;
using AlloyClient.Networking.Packets.Incoming;
using AlloyClient.Networking.Structs.DataObjects;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

// Flash parity for ShowEffect visuals (realm-client GameServerConnection
// onShowEffect): unknown targets skip entity-bound effects, Throw flies one
// arcing head, and burst/poison/line shapes match the reference counts.
internal static class ShowEffectTests {
    public static void Run() {
        ThrowAddsSingleGenerator();
        ThrowStaysBoundedAtHighFps();
        ThrowDotsMatchFlashAt60Fps();
        NovaWithUnknownTargetShowsNothing();
        NovaWithKnownTargetBurstsByRadius();
        BurstSpawns24Rays();
        PoisonSpawns10Bursts();
        LineNeedsItsTarget();
        HitEffectLivesFullDuration();
    }

    private static void ThrowAddsSingleGenerator() {
        Map.Reset();
        Map.Entities[7] = new Player { ObjectId = 7, Position = new Vector2(10, 20) };
        try {
            var fx = ShowEffect.CreatePacket();
            fx.EffectType = EffectType.Throw;
            fx.TargetObjectId = 7;
            fx.Color = unchecked((int)0xffddff00);
            fx.Pos1 = new Position { X = 14, Y = 21 };
            fx.HasPos2 = false;
            fx.Handle();
            fx.ReturnPacket();
            Equal(1, Map.ParticleGenCount);
        } finally {
            Map.Reset();
        }
    }

    // Uncapped framerates must not multiply per-frame emitters: driving one
    // 1500ms throw head at 250fps spawns ~90 trail children (one per ~16.7ms
    // of life), not one per update (375).
    private static void ThrowStaysBoundedAtHighFps() {
        Map.Reset();
        try {
            var gen = new AlloyClient.ParticleEffects.SparkerEffect(200, 0xffddff00, 1500, 0.5f,
                new Vector2(10, 20), new Vector2(14, 21), trailLifetime: 400);
            Map.AddParticleEffect(gen);
            // Update only the head; children pile up un-updated so the
            // generator count is exactly the number of children spawned.
            for (var f = 0; f < 400 && Map.ParticleGenCount > 0; f++) {
                var gens = Map.ParticleGenerators;
                var head = gens[0];
                if (!head.Update(f * 4.0, 4.0)) {
                    Map.ParticleGenCount--;
                    break;
                }
            }
            var spawned = Map.ParticleGenCount;
            if (spawned > 120)
                throw new Exception($"expected at most 120 trail children at 250fps, got {spawned}");
        } finally {
            Map.Reset();
        }
    }

    // End to end at 60fps through the real Map loop: one throw emits roughly
    // the Flash total (90 trail dots x ~400ms), far below the old 600ms-trail
    // total, and every generator drains.
    private static void ThrowDotsMatchFlashAt60Fps() {
        Map.Reset();
        Map.Entities[7] = new Player { ObjectId = 7, Position = new Vector2(10, 20) };
        try {
            var fx = ShowEffect.CreatePacket();
            fx.EffectType = EffectType.Throw;
            fx.TargetObjectId = 7;
            fx.Color = unchecked((int)0xffddff00);
            fx.Pos1 = new Position { X = 14, Y = 21 };
            fx.HasPos2 = false;
            fx.Handle();
            fx.ReturnPacket();

            long totalDots = 0;
            var dt = 1000.0 / 60.0;
            for (var f = 0; f < 300 && Map.ParticleGenCount > 0; f++)
                totalDots += UpdateFrame(f * dt, dt);
            Equal(0, Map.ParticleGenCount);
            if (totalDots > 2600)
                throw new Exception($"expected at most 2600 dots for one throw at 60fps, got {totalDots}");
        } finally {
            Map.Reset();
        }
    }

    // Server-side placeholders (e.g. the poison landing) are unknown here:
    // Flash skips the nova instead of drawing a ring at the radius-as-point.
    private static void NovaWithUnknownTargetShowsNothing() {
        Map.Reset();
        try {
            var fx = ShowEffect.CreatePacket();
            fx.EffectType = EffectType.Nova;
            fx.TargetObjectId = 999;
            fx.Color = unchecked((int)0xffddff00);
            fx.Pos1 = new Position { X = 2.5f, Y = 0 };
            fx.HasPos2 = false;
            fx.Handle();
            fx.ReturnPacket();
            Equal(0, Map.ParticleGenCount);
        } finally {
            Map.Reset();
        }
    }

    private static void NovaWithKnownTargetBurstsByRadius() {
        Map.Reset();
        Map.Entities[7] = new Player { ObjectId = 7, Position = new Vector2(10, 20) };
        try {
            var fx = ShowEffect.CreatePacket();
            fx.EffectType = EffectType.Nova;
            fx.TargetObjectId = 7;
            fx.Color = unchecked((int)0xffeba134);
            fx.Pos1 = new Position { X = 2.5f, Y = 0 };
            fx.HasPos2 = false;
            fx.Handle();
            fx.ReturnPacket();
            // One NovaEffect generator; it fans out 4 + 2*2.5 = 9 rays.
            Equal(1, Map.ParticleGenCount);
            UpdateFrame(0, 1000.0 / 60.0);
            Equal(9, Map.ParticleGenCount);
        } finally {
            Map.Reset();
        }
    }

    // Flash BurstEffect: 24 rays from the center to the rim, not one line.
    private static void BurstSpawns24Rays() {
        Map.Reset();
        Map.Entities[7] = new Player { ObjectId = 7, Position = new Vector2(10, 20) };
        try {
            var fx = ShowEffect.CreatePacket();
            fx.EffectType = EffectType.Burst;
            fx.TargetObjectId = 7;
            fx.Color = unchecked((int)0xFFFF0000);
            fx.Pos1 = new Position { X = 12, Y = 20 };
            fx.Pos2 = new Position { X = 15, Y = 20 };
            fx.HasPos2 = true;
            fx.Handle();
            fx.ReturnPacket();
            Equal(24, Map.ParticleGenCount);
        } finally {
            Map.Reset();
        }
    }

    // Flash PoisonEffect: 10 short bursts on the target, not one hit flash.
    private static void PoisonSpawns10Bursts() {
        Map.Reset();
        Map.Entities[7] = new Player { ObjectId = 7, Position = new Vector2(10, 20) };
        try {
            var fx = ShowEffect.CreatePacket();
            fx.EffectType = EffectType.Poison;
            fx.TargetObjectId = 7;
            fx.Color = unchecked((int)0xffddff00);
            fx.Handle();
            fx.ReturnPacket();
            Equal(10, Map.ParticleGenCount);
        } finally {
            Map.Reset();
        }
    }

    private static void LineNeedsItsTarget() {
        Map.Reset();
        try {
            var fx = ShowEffect.CreatePacket();
            fx.EffectType = EffectType.Line;
            fx.TargetObjectId = 999;
            fx.Color = unchecked((int)0xffff0088);
            fx.Pos1 = new Position { X = 14, Y = 21 };
            fx.HasPos2 = false;
            fx.Handle();
            fx.ReturnPacket();
            Equal(0, Map.ParticleGenCount);
        } finally {
            Map.Reset();
        }
    }

    // Flash HitEffect is a one-shot burst: all 10 particles spawn at once,
    // each living 200 + random * 100ms. The flash must outlive the 200ms
    // minimum (no per-particle overcharging) and drain past the 300ms max.
    private static void HitEffectLivesFullDuration() {
        Map.Reset();
        Map.Entities[7] = new Player { ObjectId = 7, Position = new Vector2(10, 20) };
        try {
            Map.AddParticleEffect(new AlloyClient.ParticleEffects.HitEffect(Map.Entities[7], 0xFF0000));
            for (var f = 0; f < 10; f++)
                UpdateFrame(f * 16.0, 16.0);
            Equal(1, Map.ParticleGenCount);
            for (var f = 10; f < 40; f++)
                UpdateFrame(f * 16.0, 16.0);
            Equal(0, Map.ParticleGenCount);
        } finally {
            Map.Reset();
        }
    }

    // Drives the live generator list exactly like Map.Update's generator
    // pass, without touching entities (which need render state headless).
    // Returns the dots emitted this frame.
    private static int UpdateFrame(double totalMs, double elapsedMs) {
        ParticleCountField.SetValue(null, 0);
        var gens = Map.ParticleGenerators;
        for (var i = Map.ParticleGenCount - 1; i >= 0; i--) {
            var gen = gens[i];
            if (gen.Update(totalMs, elapsedMs))
                continue;
            Map.ParticleGenCount--;
            gens[i] = gens[Map.ParticleGenCount];
            gens[Map.ParticleGenCount] = null!;
        }
        return (int)ParticleCountField.GetValue(null)!;
    }

    private static readonly System.Reflection.FieldInfo ParticleCountField =
        typeof(Map).GetField("_particleCount",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }
}
