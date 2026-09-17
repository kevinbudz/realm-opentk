namespace AlloyClient.Networking.Packets.Outgoing;

//Wire order must match the Flash client
//(realm-client/.../outgoing/PlayerHit.as) and the server's
//GameServer.PlayerHit reader: a single int bullet id.
public class PlayerHit : OutgoingPacket<PlayerHit> {
    public int BulletId;

    public override PacketId PacketId => PacketId.PlayerHit;

    public override void Reset() {
        BulletId = 0;
    }

    public override void Write(ref SpanWriter writer) {
        writer.Write(BulletId);
    }

    public override string ToString() {
        return $"BulletId: {BulletId}";
    }
}
