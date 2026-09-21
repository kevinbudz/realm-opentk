using System;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Networking.Structs.DataObjects;
using AlloyClient.ParticleEffects;
using AlloyClient.Ui.Character;

namespace AlloyClient.Networking.Packets.Incoming;

//Wire order must match the Flash client
//(realm-client/.../incoming/Aoe.as) and the server's GameServer.Aoe
//writer: pos, float radius, ushort damage, byte effect, int color.
public class Aoe : IncomingPacket<Aoe> {
    public Position Pos;
    public float Radius;
    public ushort Damage;
    public byte Effect;
    public int Color;

    public override PacketId PacketId => PacketId.Aoe;

    public override void Reset() {
        Pos.Reset();
        Radius = 0;
        Damage = 0;
        Effect = 0;
        Color = 0;
    }

    public override void Read(ref SpanReader reader) {
        Pos.Read(ref reader);
        Radius = reader.ReadSingle();
        Damage = reader.ReadUInt16();
        Effect = reader.ReadByte();
        Color = reader.ReadInt32();
    }

    public override void Handle() {
        var player = Map.LocalPlayer;
        if (player != null && !player.HasConditionEffect(ConditionEffect.Invincible)) {
            var dx = player.Position.X - Pos.X;
            var dy = player.Position.Y - Pos.Y;
            if (dx * dx + dy * dy < Radius * Radius) {
                // Flash parity (GameServerConnection.onAoe +
                // GameObject.damageWithDefense): AoE numbers subtract defense
                // too (never armor-piercing). Zero shows no text.
                var predicted = Entity.DamageWithDefense(Damage, player.Defense, false, player);
                // Flash parity (onAoe effects): the lone effect byte applies
                // even when defense floors the number to zero.
                CharacterStatusText.ApplyDamageEffect(player, Effect);
                Map.AddParticleEffect(new HitEffect(player, (uint)Color));
                if (predicted > 0) {
                    var pierced = player.HasConditionEffect(ConditionEffect.ArmorBroken)
                        || CharacterStatusText.IsArmorBrokenEffect(Effect);
                    var color = pierced ? CharacterStatusText.PiercedColor : CharacterStatusText.NormalColor;
                    NotificationLayer.AddStatusText(player, $"-{predicted}", color, 1000, 0, true);
                }
            }
        }

        //Always ack, like the Flash client: the server applies the hit
        //itself against the reported position when this ack never arrives.
        var ack = AoeAck.CreatePacket();
        ack.Time = Environment.TickCount;
        ack.Pos = player != null
            ? new Position { X = player.Position.X, Y = player.Position.Y }
            : new Position { X = Pos.X, Y = Pos.Y };
        Client.QueuePacket(ack);
    }

    public override string ToString() {
        return
            $"Pos: {Pos}, Radius: {Radius}, Damage: {Damage}, Effect: {Effect}, Color: {Color}";
    }
}
