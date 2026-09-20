using System;
using AlloyClient.Game;
using AlloyClient.Networking.Enums;
using AlloyClient.Networking.Structs.DataObjects;
using AlloyClient.ParticleEffects;
using AlloyClient.Utils;
using OpenTK.Mathematics;

namespace AlloyClient.Networking.Packets.Incoming;

//Wire order must match the Flash client
//(realm-client/.../incoming/ShowEffect.as) and the server's
//GameServer.ShowEffect writer: byte effect, int target, int color, pos1,
//then pos2 only when it carries data. The layout is fixed regardless of
//effect type; branching the read per type desynchronized the stream.
public class ShowEffect : IncomingPacket<ShowEffect> {
    public EffectType EffectType;
    public int TargetObjectId;
    public int Color;
    public Position Pos1;
    public Position Pos2;
    public bool HasPos2;

    public override PacketId PacketId => PacketId.ShowEffect;

    public override void Reset() {
        EffectType = default;
        TargetObjectId = 0;
        Color = 0;
        Pos1.Reset();
        Pos2.Reset();
        HasPos2 = false;
    }

    public override void Read(ref SpanReader reader) {
        EffectType = (EffectType)reader.ReadByte();
        TargetObjectId = reader.ReadInt32();
        Color = reader.ReadInt32();
        Pos1.Read(ref reader);

        if (reader.Remaining > 0) {
            Pos2.Read(ref reader);
            HasPos2 = true;
        } else {
            Pos2.Reset();
            HasPos2 = false;
        }
    }

    // Effect shapes mirror the Flash client's onShowEffect
    // (GameServerConnection.as): the same counts, lifetimes and
    // unknown-target skips, so ability visuals match the reference.
    public override void Handle() {
        var color = (uint)Color;
        Map.Entities.TryGetValue(TargetObjectId, out var target);

        switch (EffectType) {
            case EffectType.Heal: {
                // Flash HealEffect: 10 rising sparkles around the target.
                if (target == null)
                    break;
                for (var i = 0; i < 10; i++) {
                    var angle = 2 * MathF.PI * (i / 10f);
                    var size = (3 + Random.Shared.Next(5)) * 20;
                    var dist = 0.3f + 0.4f * Random.Shared.NextSingle();
                    Map.AddParticleEffect(new SparkEffect(size, color, 1000, 0.5f,
                        Random.Shared.NextSingle() * 0.2f - 0.1f, -0.5f - Random.Shared.NextSingle() * 0.5f,
                        target.Position.X + dist * MathF.Cos(angle), target.Position.Y + dist * MathF.Sin(angle)));
                }
                break;
            }
            case EffectType.Poison: {
                // Flash PoisonEffect: 10 short bursts on the target.
                if (target == null)
                    break;
                for (var i = 0; i < 10; i++) {
                    Map.AddParticleEffect(new SparkEffect(100, color, 400, 0.75f,
                        Random.Shared.PlusMinus(4f), Random.Shared.PlusMinus(4f),
                        target.Position.X, target.Position.Y));
                }
                break;
            }
            case EffectType.Flash:
                if (target != null)
                    Map.AddParticleEffect(new HitEffect(target, color));
                break;
            case EffectType.Teleport: {
                // Flash TeleportEffect ignores the packet color: 20 blue
                // rising dots scattered around the destination.
                for (var i = 0; i < 20; i++) {
                    var angle = 2 * MathF.PI * Random.Shared.NextSingle();
                    var dist = 0.7f * Random.Shared.NextSingle();
                    Map.AddParticleEffect(new SparkEffect(50, 0x0000FF, 500 + Random.Shared.Next(1000), 0.5f,
                        0f, -0.8f,
                        Pos1.X + dist * MathF.Cos(angle), Pos1.Y + dist * MathF.Sin(angle)));
                }
                break;
            }
            case EffectType.Nova: {
                // Flash NovaEffect, skipped for unknown targets (e.g. the
                // server-side poison placeholder): never fall back to pos1,
                // which carries the radius, not a position.
                if (target == null)
                    break;
                Map.AddParticleEffect(new NovaEffect(target.Position, Math.Max(Pos1.X, 0.5f), color));
                break;
            }
            case EffectType.Ring: {
                // Flash RingEffect: fixed 12-ray ring, skipped for unknown targets.
                if (target == null)
                    break;
                Map.AddParticleEffect(new RingEffect(target.Position, Math.Max(Pos1.X, 0.5f), color));
                break;
            }
            case EffectType.Throw: {
                // Flash ThrowEffect: one arcing head with a short trail.
                // The start fallback (pos2 when no duration rides along, else
                // pos1) and the 1500ms default with 100..10000ms clamping
                // match the reference exactly.
                Vector2 start;
                if (target != null)
                    start = target.Position;
                else if (!HasPos2)
                    start = new Vector2(Pos2.X, Pos2.Y);
                else
                    start = new Vector2(Pos1.X, Pos1.Y);
                var dest = new Vector2(Pos1.X, Pos1.Y);
                var duration = HasPos2 ? Math.Clamp((int)Pos2.X, 100, 10000) : 1500;
                Map.AddParticleEffect(new SparkerEffect(200, color, duration, 0.5f, start, dest, trailLifetime: 400));
                break;
            }
            case EffectType.Stream: {
                // Flash StreamEffect: 5 long-lived drifters from pos1 to pos2.
                var start = new Vector2(Pos1.X, Pos1.Y);
                var end = HasPos2 ? new Vector2(Pos2.X, Pos2.Y) : start;
                for (var i = 0; i < 5; i++) {
                    var size = (3 + Random.Shared.Next(5)) * 20;
                    Map.AddParticleEffect(new SparkEffect(size, color, 1500 + Random.Shared.Next(3000), 1.85f, start, end));
                }
                break;
            }
            case EffectType.Burst: {
                // Flash BurstEffect: 24 rays from the center to the rim.
                // The owner is unused but a missing one still skips.
                if (target == null)
                    break;
                var center = new Vector2(Pos1.X, Pos1.Y);
                var edge = HasPos2 ? new Vector2(Pos2.X, Pos2.Y) : center;
                var radius = Vector2.Distance(center, edge);
                for (var i = 0; i < 24; i++) {
                    var angle = i * 2 * MathF.PI / 24;
                    var p = new Vector2(center.X + radius * MathF.Cos(angle), center.Y + radius * MathF.Sin(angle));
                    Map.AddParticleEffect(new SparkerEffect(100, color, 100 + Random.Shared.Next(200), 0.5f, center, p));
                }
                break;
            }
            case EffectType.Collapse: {
                // Flash CollapseEffect: 24 rays imploding from the rim.
                if (target == null)
                    break;
                var center = new Vector2(Pos1.X, Pos1.Y);
                var edge = HasPos2 ? new Vector2(Pos2.X, Pos2.Y) : center;
                var radius = Vector2.Distance(center, edge);
                for (var i = 0; i < 24; i++) {
                    var angle = i * 2 * MathF.PI / 24;
                    var p = new Vector2(center.X + radius * MathF.Cos(angle), center.Y + radius * MathF.Sin(angle));
                    Map.AddParticleEffect(new SparkerEffect(300, color, 200, 0.5f, p, center));
                }
                break;
            }
            case EffectType.Line: {
                // Flash LineEffect: 30 dots along the caster-to-target beam.
                if (target == null)
                    break;
                const int num = 30;
                var start = target.Position;
                var end = new Vector2(Pos1.X, Pos1.Y);
                for (var i = 0; i < num; i++) {
                    var p = Vector2.Lerp(start, end, i / (float)num);
                    Map.AddParticleEffect(new SparkEffect(100, color, 700, 0.5f,
                        Random.Shared.PlusMinus(1f), Random.Shared.PlusMinus(1f), p.X, p.Y));
                }
                break;
            }
            case EffectType.Flow: {
                // Flash FlowEffect: 5 drifters arcing from pos1 to the target.
                if (target == null)
                    break;
                var start = new Vector2(Pos1.X, Pos1.Y);
                for (var i = 0; i < 5; i++) {
                    var size = (3 + Random.Shared.Next(5)) * 20;
                    Map.AddParticleEffect(new SparkEffect(size, color, 1000, 0.5f, start, target.Position));
                }
                break;
            }
            case EffectType.Lightning: {
                // Flash LightningEffect: distance-scaled jittered bolt whose
                // dots fade along its length; size rides in pos2.x.
                if (target == null)
                    break;
                var start = target.Position;
                var end = new Vector2(Pos1.X, Pos1.Y);
                var distance = Vector2.Distance(start, end);
                var num = (int)(distance * 3);
                var size = HasPos2 ? (int)Pos2.X : 100;
                for (var i = 0; i < num; i++) {
                    var t = i / (float)num;
                    var p = Vector2.Lerp(start, end, t);
                    var factor = Math.Min(i, num - i);
                    var jitter = distance / 200 * factor;
                    Map.AddParticleEffect(new SparkEffect(size, color, (int)(1000 - t * 900), 0.5f,
                        0f, 0f,
                        p.X + Random.Shared.PlusMinus(jitter), p.Y + Random.Shared.PlusMinus(jitter)));
                }
                break;
            }
            case EffectType.ConeBlast: {
                // Flash ConeBlastEffect: 7 short rays fanning toward pos1.
                if (target == null)
                    break;
                var start = target.Position;
                var aim = MathF.Atan2(Pos1.Y - start.Y, Pos1.X - start.X);
                var radius = HasPos2 ? Pos2.X : 3f;
                const int num = 7;
                const float arc = MathF.PI / 3;
                for (var i = 0; i < num; i++) {
                    var angle = aim - arc / 2 + i * arc / num;
                    var p = new Vector2(start.X + radius * MathF.Cos(angle), start.Y + radius * MathF.Sin(angle));
                    Map.AddParticleEffect(new SparkerEffect(200, color, 100, 0.5f, start, p));
                }
                break;
            }
            case EffectType.Jitter:
                Camera.StartJitter();
                break;
            case EffectType.ThrowProjectile: {
                // No sender on this server; Flash flies the item texture
                // itself (ThrowProjectileEffect). Approximate the flight with
                // a plain tracer until a thrown-item render exists.
                Map.AddParticleEffect(new SparkerEffect(100, 0xFFFFFF, 1500, 0.5f,
                    new Vector2(Pos2.X, Pos2.Y), new Vector2(Pos1.X, Pos1.Y)));
                break;
            }
            default:
                break;
        }
    }

    public override string ToString() {
        return $"EffectType: {EffectType}, TargetObjectId: {TargetObjectId}, Pos1: {Pos1}, Pos2: {Pos2}, Color: {Color}";
    }
}
