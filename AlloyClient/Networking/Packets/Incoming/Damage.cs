using System;
using AlloyClient.Game;
using AlloyClient.ParticleEffects;
using AlloyClient.Ui.Character;

namespace AlloyClient.Networking.Packets.Incoming;

//Wire order must match the Flash client
//(realm-client/.../incoming/Damage.as) and the server's GameServer.Damage
//writer: int target id, byte effect count + effect bytes, ushort damage.
//There are no kill/bullet/object fields on the wire; reading them
//desynchronized every Damage packet.
public class Damage : IncomingPacket<Damage> {
    public int TargetId;
    public byte[] Effects;
    public int EffectCount;
    public ushort DamageAmount;

    public override PacketId PacketId => PacketId.Damage;

    public override void Reset() {
        TargetId = 0;
        EffectCount = 0;
        DamageAmount = 0;
    }

    public override void Read(ref SpanReader reader) {
        TargetId = reader.ReadInt32();

        EffectCount = reader.ReadByte();
        if (Effects == null || Effects.Length < EffectCount)
            Effects = new byte[Math.Max(EffectCount, 1)];
        for (var i = 0; i < EffectCount; i++)
            Effects[i] = reader.ReadByte();

        DamageAmount = reader.ReadUInt16();
    }

    public override void Handle() {
        if (!Map.Entities.TryGetValue(TargetId, out var target))
            return;

        //Authoritative hit display, mirroring the Flash client's
        //target.damage(): the server already applied this; conditions
        //re-sync through NewTick/Update stats.
        target.Hp = Math.Max(0, target.Hp - DamageAmount);
        Map.AddParticleEffect(new HitEffect(target, 0xFF0000));
        NotificationLayer.AddStatusText(target, $"-{DamageAmount}", 0xFF0000, 1000, 0);
    }

    public override string ToString() {
        return $"TargetId: {TargetId}, DamageAmount: {DamageAmount}, Effects: {EffectCount}";
    }
}
