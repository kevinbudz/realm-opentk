using AlloyClient.Networking.Structs.DataObjects;

namespace AlloyClient.Networking.Packets.Outgoing;

//Wire order must match the Flash client
//(realm-client/.../outgoing/PlayerShoot.as) and the server's
//GameServer.PlayerShoot reader: time, starting pos, angle, ability flag,
//with numShots appended only when != 1. Sending fewer fields used to throw
//EndOfStreamException out of the server's read path.
public class PlayerShoot : OutgoingPacket<PlayerShoot> {

    public int Time;
    public Position StartingPos;
    public float Angle;
    public bool Ability;
    public int NumShots;

    public override PacketId PacketId => PacketId.PlayerShoot;

    public override void Reset() {
        Time = 0;
        StartingPos.Reset();
        Angle = 0f;
        Ability = false;
        NumShots = 1;
    }

    public override void Write(ref SpanWriter writer) {
        writer.Write(Time);
        StartingPos.Write(ref writer);
        writer.Write(Angle);
        writer.Write(Ability);
        if (NumShots != 1)
            writer.Write((byte)NumShots);
    }

    public override string ToString() {
        return $"Time: {Time}, StartingPos: {StartingPos}, Angle: {Angle}, Ability: {Ability}, NumShots: {NumShots}";
    }
}
