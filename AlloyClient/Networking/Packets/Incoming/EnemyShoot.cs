using System;
using AlloyClient.Assets.Libraries;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Logging;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Networking.Structs.DataObjects;
using Microsoft.Extensions.Logging;
using OpenTK.Mathematics;

namespace AlloyClient.Networking.Packets.Incoming;

//Wire order must match the Flash client
//(realm-client/.../incoming/EnemyShoot.as) and the server's
//GameServer.EnemyShoot writer: int bullet id, int owner id, byte bullet
//type, start pos, float angle (radians), short damage, then byte numShots
//+ float angleInc only when numShots > 1.
public class EnemyShoot : IncomingPacket<EnemyShoot> {

    private readonly static ILogger Logger = ILogger.CreateLogger(nameof(EnemyShoot));

    public int BulletId;
    public int OwnerId;
    public byte ProjectileIndex;
    public Position StartPos;
    public float Angle;
    public short Damage;
    public byte NumShots;
    public float AngleInc;

    public override PacketId PacketId => PacketId.EnemyShoot;

    public override void Reset() {
        BulletId = 0;
        OwnerId = 0;
        ProjectileIndex = 0;
        Angle = 0;
        Damage = 0;
        StartPos.Reset();
        NumShots = 1;
        AngleInc = 0;
    }

    public override void Read(ref SpanReader reader) {
        BulletId = reader.ReadInt32();
        OwnerId = reader.ReadInt32();
        ProjectileIndex = reader.ReadByte();
        StartPos.Read(ref reader);
        Angle = reader.ReadSingle();
        Damage = reader.ReadInt16();
        if (reader.Remaining > 0) {
            NumShots = reader.ReadByte();
            AngleInc = reader.ReadSingle();
        } else {
            NumShots = 1;
            AngleInc = 0;
        }
    }

    public override void Handle() {
        //Ack first, like the Flash client: the server awaits this volley
        //even when the owner is unknown locally.
        var ack = ShootAck.CreatePacket();
        ack.Time = Environment.TickCount;
        Client.QueuePacket(ack);

        if (!Map.Entities.TryGetValue(OwnerId, out var en))
            return;

        var containerDesc = en.Properties;
        if (!containerDesc.Projectiles.TryGetValue(ProjectileIndex, out var projProps)) {
            Logger.Log(LogLevel.Error, $"Projectile '{ProjectileIndex}' not found for {en.Name}");
            return;
        }

        if (!ObjectLibrary.IdToObjectType.TryGetValue(projProps.ObjectId, out var objType)) {
            Logger.Log(LogLevel.Error, $"Projectile '{projProps.ObjectId}' not found in GameData.");
            return;
        }

        var objProps = ObjectLibrary.TypeToObjectProps[objType];
        for (var i = 0; i < NumShots; i++) {
            var proj = ObjectPools.Projectiles.Pop();
            proj.Reset(BulletId + i, Damage, Angle + AngleInc * i, en, objProps, projProps, null, new Vector2(StartPos.X, StartPos.Y));
            Map.AddProjectile(proj);
        }

        en.SetAttack(en.Type, Angle + AngleInc * (NumShots - 1) / 2);
    }

    public override string ToString() {
        return $"BulletId: {BulletId}, OwnerId: {OwnerId}, ProjectileIndex: {ProjectileIndex}, Angle: {Angle}, Damage: {Damage}, StartingPos: {StartPos}, NumShots: {NumShots}, AngleInc: {AngleInc}";
    }
}
