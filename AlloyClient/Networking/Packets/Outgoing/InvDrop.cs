namespace AlloyClient.Networking.Packets.Outgoing;

//Wire order must match the Flash client
//(realm-client/.../outgoing/InvDrop.as) and the server's
//GameServer.InvDrop reader: a single slot-id byte. The old ObjectSlot
//payload (object id + slot) was misparsed as the slot id.
public class InvDrop : OutgoingPacket<InvDrop> {
    public byte SlotId;

    public override PacketId PacketId => PacketId.InvDrop;

    public override void Reset() {
        SlotId = 0;
    }

    public override void Write(ref SpanWriter writer) {
        writer.Write(SlotId);
    }

    public override string ToString() {
        return $"SlotId: {SlotId}";
    }
}
