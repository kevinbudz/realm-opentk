namespace AlloyClient.Networking.Packets.Incoming;

public class AllyShoot : IncomingPacket<AllyShoot> {

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
    }

    public override string ToString() {
        return $"OwnerId: {OwnerId}, ContainerType: {ContainerType}, Angle: {Angle}";
    }
}