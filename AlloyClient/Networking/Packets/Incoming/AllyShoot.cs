using AlloyClient.Assets.Libraries;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Logging;
using Microsoft.Extensions.Logging;
using OpenTK.Mathematics;

namespace AlloyClient.Networking.Packets.Incoming;

public class AllyShoot : IncomingPacket<AllyShoot> {

    private readonly static ILogger Logger = ILogger.CreateLogger(nameof(AllyShoot));

    public int OwnerId;
    public short ContainerType;
    public float Angle;

    public override PacketId PacketId => PacketId.AllyShoot;

    public override void Reset() {
        OwnerId = 0;
        ContainerType = 0;
        Angle = 0;
    }

    //Wire: int ownerId, short containerType, float angle (no bullet id).
    public override void Read(ref SpanReader reader) {
        OwnerId = reader.ReadInt32();
        ContainerType = reader.ReadInt16();
        Angle = reader.ReadSingle();
    }

    public override void Handle() {
        // Flash parity (Parameters allyShots): ally shots are visuals only
        // and skipped entirely while the option is off.
        if (!Settings.AllyInfo || !Settings.AllyShots)
            return;

        //Other players' shots are visuals only, like the Flash client:
        //they fly with fake local ids and never report hits, since the
        //server only knows the shooter's own bullet ids.
        if (!Map.Entities.TryGetValue(OwnerId, out var owner))
            return;

        if (!ObjectLibrary.TypeToObjectProps.TryGetValue((ushort)ContainerType, out var weaponProps)) {
            Logger.Log(LogLevel.Error, $"AllyShoot: missing weapon {ContainerType} for owner {OwnerId}");
            return;
        }

        ProjectileProperties projProps = null;
        foreach (var kv in weaponProps.Projectiles) {
            projProps = kv.Value;
            break;
        }
        if (projProps == null)
            return;

        if (!ObjectLibrary.IdToObjectType.TryGetValue(projProps.ObjectId, out var objType))
            return;

        var objProps = ObjectLibrary.TypeToObjectProps[objType];
        var numShots = weaponProps.NumProjectiles;
        var arcGap = MathHelper.DegreesToRadians(weaponProps.ArcGap);
        var totalArc = arcGap * (numShots - 1);
        var angle = Angle - totalArc / 2;
        var startId = Map.NextFakeBulletId;
        Map.NextFakeBulletId += numShots;

        for (var i = 0; i < numShots; i++) {
            var proj = ObjectPools.Projectiles.Pop();
            proj.Reset(startId + i, 0, angle, owner, objProps, projProps, null, owner.Position);
            Map.AddProjectile(proj);
            angle += arcGap;
        }

        owner.SetAttack(ContainerType, Angle);
    }

    public override string ToString() {
        return $"OwnerId: {OwnerId}, ContainerType: {ContainerType}, Angle: {Angle}";
    }
}
