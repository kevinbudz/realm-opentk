using AlloyClient.Networking.Structs.DataObjects;

namespace AlloyClient.Networking.Packets.Outgoing;

//Wire order must match the Flash client
//(realm-client/.../outgoing/InvSwap.as) and the server's
//GameServer.InvSwap reader: time, player pos, then both slot objects.
//Omitting the time/pos prefix used to throw EndOfStreamException out of
//the server's read path.
public class InvSwap : OutgoingPacket<InvSwap> {
    public int Time;
    public Position NewPosition;
    public ObjectSlot SlotObj1;
    public ObjectSlot SlotObj2;

    public override PacketId PacketId => PacketId.InvSwap;

    public override void Reset() {
        Time = 0;
        NewPosition.Reset();
        SlotObj1.Reset();
        SlotObj2.Reset();
    }

    public override void Write(ref SpanWriter writer) {
        writer.Write(Time);
        NewPosition.Write(ref writer);
        SlotObj1.Write(ref writer);
        SlotObj2.Write(ref writer);
    }

    public override string ToString() {
        return $"Time: {Time}, NewPosition: {NewPosition}, SlotObj1: {SlotObj1}, SlotObj2: {SlotObj2}";
    }
}
