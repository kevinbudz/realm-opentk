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
        // Flash parity (Parameters allyDamage): damage on other entities is
        // skipped while the option is off; the local player's own hits stay.
        if (!Settings.ShouldShowAllyDamage(TargetId, Map.LocalPlayerId))
            return;

        if (!Map.Entities.TryGetValue(TargetId, out var target))
            return;

        //Authoritative hit display, mirroring the Flash client's
        //target.damage(): the server already applied this; stats re-sync
        //wholesale through NewTick/Update a tick later.
        target.Hp = Math.Max(0, target.Hp - DamageAmount);
        // Flash parity (damage effects loop): server ids Nothing = 0 ..
        // Hexed = 25 only; betterskillys-only ids never arrive and are
        // dropped inside. Queued before the number, like Flash.
        CharacterStatusText.ApplyDamageEffects(target, Effects, EffectCount);
        var pierced = target.HasConditionEffect(ConditionEffect.ArmorBroken) || HasArmorBrokenEffect();
        Map.AddParticleEffect(new HitEffect(target, 0xFF0000));
        // Flash parity (damageAmount > 0): invulnerable zeroes show the
        // condition text but no number.
        if (DamageAmount > 0) {
            var color = pierced ? CharacterStatusText.PiercedColor : CharacterStatusText.NormalColor;
            NotificationLayer.AddStatusText(target, $"-{DamageAmount}", color, 1000, 0, true);
        }
    }

    // Flash parity (damage pierced check): the tick carrying armor break
    // can arrive with the damage itself, before the target's live state
    // re-syncs, so the packet's own effect list counts too.
    private bool HasArmorBrokenEffect() {
        for (var i = 0; i < EffectCount; i++)
            if (CharacterStatusText.IsArmorBrokenEffect(Effects[i]))
                return true;
        return false;
    }

    public override string ToString() {
        return $"TargetId: {TargetId}, DamageAmount: {DamageAmount}, Effects: {EffectCount}";
    }
}
