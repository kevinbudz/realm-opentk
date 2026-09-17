namespace AlloyClient.Networking.Packets.Outgoing;

//Wire order must match the Flash client
//(realm-client/.../outgoing/ShootAck.as) and the server's
//GameServer.ShootAck reader: a single int client-clock time. The client
//sends one ack per enemy volley (EnemyShoot) and per own-ability volley
//received via ServerPlayerShoot; without them the server's
//AwaitingProjectiles queue starves and eventually disconnects.
public class ShootAck : OutgoingPacket<ShootAck> {
    public int Time;

    public override PacketId PacketId => PacketId.ShootAck;

    public override void Reset() {
        Time = 0;
    }

    public override void Write(ref SpanWriter writer) {
        writer.Write(Time);
    }

    public override string ToString() {
        return $"Time: {Time}";
    }
}
