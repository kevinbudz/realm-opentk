using System;
using AlloyClient.Game;
using AlloyClient.Networking.Enums;
using AlloyClient.Networking.Structs.DataObjects;
using AlloyClient.ParticleEffects;
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

    public override void Handle() {
        var color = (uint)Color;
        Map.Entities.TryGetValue(TargetObjectId, out var target);

        switch (EffectType) {
            case EffectType.Heal:
            case EffectType.Poison:
            case EffectType.Flash:
                if (target != null)
                    Map.AddParticleEffect(new HitEffect(target, color));
                break;
            case EffectType.Teleport:
                Map.AddParticleEffect(new RingEffect(new Vector2(Pos1.X, Pos1.Y), 1f, color));
                break;
            case EffectType.Nova:
            case EffectType.Ring:
                var center = target != null ? target.Position : new Vector2(Pos1.X, Pos1.Y);
                Map.AddParticleEffect(new RingEffect(center, Math.Max(Pos1.X, 0.5f), color));
                break;
            case EffectType.Throw:
                var start = target != null ? target.Position : new Vector2(Pos2.X, Pos2.Y);
                var dest = new Vector2(Pos1.X, Pos1.Y);
                var duration = HasPos2 ? Math.Max((int)Pos2.X, 1) : 1500;
                Map.AddParticleEffect(new SparkerEffect(100, color, duration, 0.5f, start, dest));
                break;
            case EffectType.Stream:
            case EffectType.Burst:
            case EffectType.Collapse:
            case EffectType.ThrowProjectile:
                Map.AddParticleEffect(new SparkerEffect(100, color, 600, 0.5f,
                    new Vector2(Pos1.X, Pos1.Y),
                    HasPos2 ? new Vector2(Pos2.X, Pos2.Y) : new Vector2(Pos1.X, Pos1.Y)));
                break;
            case EffectType.Line:
            case EffectType.Flow:
            case EffectType.Lightning:
            case EffectType.ConeBlast:
                if (target != null)
                    Map.AddParticleEffect(new HitEffect(target, color));
                Map.AddParticleEffect(new SparkEffect(100, color, 600, 0.5f,
                    new Vector2(Pos1.X, Pos1.Y), new Vector2(Pos1.X, Pos1.Y)));
                break;
            case EffectType.Jitter:
                break;
            default:
                break;
        }
    }

    public override string ToString() {
        return $"EffectType: {EffectType}, TargetObjectId: {TargetObjectId}, Pos1: {Pos1}, Pos2: {Pos2}, Color: {Color}";
    }
}
