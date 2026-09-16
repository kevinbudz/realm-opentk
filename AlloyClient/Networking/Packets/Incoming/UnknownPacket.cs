namespace AlloyClient.Networking.Packets.Incoming;

//Fallback for wire ids this build does not implement (or newer-than-client
//packets). The frame body is already bounded by SocketReceiveState, so Read
//intentionally consumes nothing and Handle drops the packet.
public class UnknownPacket : IncomingPacket<UnknownPacket> {
    public override PacketId PacketId => PacketId.Unknown;

    public override void Reset() {
    }

    public override void Read(ref SpanReader reader) {
    }

    public override void Handle() {
    }

    public override string ToString() {
        return "UnknownPacket";
    }
}
