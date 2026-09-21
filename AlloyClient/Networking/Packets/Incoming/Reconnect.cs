namespace AlloyClient.Networking.Packets.Incoming;

public class Reconnect : IncomingPacket<Reconnect> {
    public int GameId;

    public override PacketId PacketId => PacketId.Reconnect;

    public override void Reset() {
        GameId = 0;
    }

    public override void Read(ref SpanReader reader) {
        GameId = reader.ReadInt32();
    }

    public override void Handle() {
        // Flash parity (GameSpriteMediator.onReconnect): this must open a new
        // socket handshake for GameId. Hello on the old Connected socket is
        // ignored by the server (Hello requires Handshaked state).
        Client.ReconnectTo(GameId);
    }

    public override string ToString() {
        return $"GameId: {GameId}";
    }
}