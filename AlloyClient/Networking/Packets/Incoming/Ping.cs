using System;
using AlloyClient.Networking.Packets.Outgoing;

namespace AlloyClient.Networking.Packets.Incoming;

public class Ping : IncomingPacket<Ping> {
    public int RTT;

    public override PacketId PacketId => PacketId.Ping;

    public override void Reset() {
        RTT = 0;
    }

    public override void Read(ref SpanReader reader) {
        RTT = reader.ReadInt32();
    }

    public override void Handle() {
        var pong = Pong.CreatePacket();
        pong.Serial = RTT;
        pong.Time = Environment.TickCount;
        Client.QueuePacket(pong);
    }

    public override string ToString() {
        return $"RTT: {RTT}";
    }
}