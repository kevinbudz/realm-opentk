using AlloyClient.Game;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Networking.Structs.DataObjects;

namespace AlloyClient.Networking.Packets.Incoming;

public class Goto : IncomingPacket<Goto> {
    public int ObjectId;
    public Position Pos;

    public override PacketId PacketId => PacketId.Goto;

    public override void Reset() {
        ObjectId = 0;
        Pos.Reset();
    }

    public override void Read(ref SpanReader reader) {
        ObjectId = reader.ReadInt32();
        Pos.Read(ref reader);
    }

    public override void Handle() {
        if (Map.LocalPlayer == null || ObjectId != Map.LocalPlayer.ObjectId) {
            return;
        }

        Map.LocalPlayer.MoveTo(Pos.X, Pos.Y);
        Map.LocalPlayer.TickPosition.X = Pos.X;
        Map.LocalPlayer.TickPosition.Y = Pos.Y;
        Map.LocalPlayer.PositionAtTick.X = Pos.X;
        Map.LocalPlayer.PositionAtTick.Y = Pos.Y;

        //Ack with the live client clock, like the Flash client's
        //gotoAck(time): a 0 timestamp reads as time-travel once the
        //server's clock gate is established and gets rejected.
        var gt = GotoAck.CreatePacket();
        gt.Time = System.Environment.TickCount;
        Client.QueuePacket(gt);
    }

    public override string ToString() {
        return $"ObjectId: {ObjectId}, Pos: {Pos}";
    }
}