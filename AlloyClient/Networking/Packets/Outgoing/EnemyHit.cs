namespace AlloyClient.Networking.Packets.Outgoing;

//Wire order must match the Flash client
//(realm-client/.../outgoing/EnemyHit.as) and the server's
//GameServer.EnemyHit reader: time, bullet id, target id, all ints.
public class EnemyHit : OutgoingPacket<EnemyHit> {

    public int Time;
    public int BulletId;
    public int TargetId;

    public override PacketId PacketId => PacketId.EnemyHit;

    public override void Reset() {
        Time = 0;
        BulletId = 0;
        TargetId = 0;
    }

    public override void Write(ref SpanWriter writer) {
        writer.Write(Time);
        writer.Write(BulletId);
        writer.Write(TargetId);
    }

    public override string ToString() {
        return $"Time: {Time}, BulletId: {BulletId}, TargetId: {TargetId}";
    }
}
