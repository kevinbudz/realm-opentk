namespace AlloyClient.Networking.Packets.Outgoing;

//Wire order must match the Flash client
//(realm-client/.../outgoing/SquareHit.as) and the server's
//GameServer.SquareHit reader: time, bullet id, both ints. Sent when an
//enemy bullet dies on a wall, the void, or blocking cover, mirroring
//Projectile.update in the Flash client (only enemy bullets report).
public class SquareHit : OutgoingPacket<SquareHit> {
    public int Time;
    public int BulletId;

    public override PacketId PacketId => PacketId.SquareHit;

    public override void Reset() {
        Time = 0;
        BulletId = 0;
    }

    public override void Write(ref SpanWriter writer) {
        writer.Write(Time);
        writer.Write(BulletId);
    }

    public override string ToString() {
        return $"Time: {Time}, BulletId: {BulletId}";
    }
}
