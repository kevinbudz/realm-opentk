using System;
using AlloyClient.Assets.Libraries;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Logging;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Networking.Structs.DataObjects;
using Microsoft.Extensions.Logging;
using OpenTK.Mathematics;

namespace AlloyClient.Networking.Packets.Incoming;

//Wire order must match the Flash client
//(realm-client/.../incoming/ServerPlayerShoot.as) and the server's
//GameServer.ServerPlayerShoot writer: int bullet id, int owner id, short
//container type, start pos, float angle (radians), float angleInc, byte
//damage count + short per-bullet damage. Carries the server's own ability
//volleys (poison nova, Steiner...); the client must ShootAck them when it
//owns them, exactly like EnemyShoot.
public class ServerPlayerShoot : IncomingPacket<ServerPlayerShoot> {

    private readonly static ILogger Logger = ILogger.CreateLogger(nameof(ServerPlayerShoot));

    public int BulletId;
    public int OwnerId;
    public short ContainerType;
    public Position StartingPos;
    public float Angle;
    public float AngleInc;
    public short[] Damages;
    public int DamageCount;

    public override PacketId PacketId => PacketId.ServerPlayerShoot;

    public override void Reset() {
        BulletId = 0;
        OwnerId = 0;
        ContainerType = 0;
        StartingPos.Reset();
        Angle = 0;
        AngleInc = 0;
        DamageCount = 0;
    }

    public override void Read(ref SpanReader reader) {
        BulletId = reader.ReadInt32();
        OwnerId = reader.ReadInt32();
        ContainerType = reader.ReadInt16();
        StartingPos.Read(ref reader);
        Angle = reader.ReadSingle();
        AngleInc = reader.ReadSingle();

        DamageCount = reader.ReadByte();
        if (Damages == null || Damages.Length < DamageCount)
            Damages = new short[Math.Max(DamageCount, 1)];
        for (var i = 0; i < DamageCount; i++)
            Damages[i] = reader.ReadInt16();
    }

    public override void Handle() {
        var owned = OwnerId == Map.LocalPlayerId;
        if (owned) {
            var ack = ShootAck.CreatePacket();
            ack.Time = Environment.TickCount;
            Client.QueuePacket(ack);
        }

        if (!Map.Entities.TryGetValue(OwnerId, out var owner))
            return;

        if (!ObjectLibrary.TypeToObjectProps.TryGetValue((ushort)ContainerType, out var containerProps)) {
            Logger.Log(LogLevel.Error, $"Container '{ContainerType}' not found for ServerPlayerShoot.");
            return;
        }

        ProjectileProperties projProps = null;
        foreach (var kv in containerProps.Projectiles) {
            projProps = kv.Value;
            break;
        }
        if (projProps == null) {
            Logger.Log(LogLevel.Error, $"Container '{ContainerType}' has no projectile.");
            return;
        }

        if (!ObjectLibrary.IdToObjectType.TryGetValue(projProps.ObjectId, out var objType)) {
            Logger.Log(LogLevel.Error, $"Projectile '{projProps.ObjectId}' not found in GameData.");
            return;
        }

        var objProps = ObjectLibrary.TypeToObjectProps[objType];
        for (var i = 0; i < DamageCount; i++) {
            //Foreign volleys get fake local ids, like the Flash client:
            //only the owner's client reports hits for real bullet ids.
            var id = owned ? BulletId + i : Map.NextFakeBulletId++;
            var proj = ObjectPools.Projectiles.Pop();
            proj.Reset(id, Damages[i], Angle + AngleInc * i, owner, objProps, projProps, null, new Vector2(StartingPos.X, StartingPos.Y));
            Map.AddProjectile(proj);
        }

        owner.SetAttack(ContainerType, Angle);
    }

    public override string ToString() {
        return $"BulletId: {BulletId}, OwnerId: {OwnerId}, ContainerType: {ContainerType}, StartingPos: {StartingPos}, Angle: {Angle}, AngleInc: {AngleInc}, Damages: {DamageCount}";
    }
}
