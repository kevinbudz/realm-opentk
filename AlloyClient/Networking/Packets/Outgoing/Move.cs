using AlloyClient.Networking.Structs.DataObjects;

namespace AlloyClient.Networking.Packets.Outgoing;

public class Move : OutgoingPacket<Move> {

    public int Time;
    public Position NewPosition;

    public override PacketId PacketId => PacketId.Move;

    public override void Reset() {
        Time = 0;
        NewPosition.Reset();
    }

    public override void Write(ref SpanWriter writer) {
        writer.Write(Time);
        NewPosition.Write(ref writer);
    }

    public override string ToString() {
        return $"Time: {Time}, NewPosition: {NewPosition}";
    }
}