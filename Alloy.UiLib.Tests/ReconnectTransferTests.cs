using AlloyClient.Game;
using AlloyClient.Networking;
using AlloyClient.Networking.Packets.Incoming;
using AlloyClient.Networking.Packets.Outgoing;

namespace Alloy.UiLib.Tests;

// Flash parity (GameSpriteMediator.onReconnect): a server Reconnect must open
// a fresh handshake for the target world. Hello on the old Connected socket
// is ignored by the server (Hello requires Handshaked state), so queueing it
// there always ends on the character list after the old socket closes.
internal static class ReconnectTransferTests {
    public static void Run() {
        ReconnectOpensFreshHandshake();
        StaleDisconnectKeepsSession();
        HandshakeMutesStaleGamePackets();
    }

    private static void ReconnectOpensFreshHandshake() {
        var previousReconnecting = Client.IsReconnecting;
        var previousState = Client.State;
        var previousSink = Client.ReconnectSink;
        var previousOutgoing = Client.OutgoingSink;
        Client.IsReconnecting = false;
        Client.State = ConnectionState.Connected;

        var requested = new List<int>();
        var hellos = 0;
        Client.ReconnectSink = requested.Add;
        Client.OutgoingSink = pkt => {
            if (pkt is Hello)
                hellos++;
        };
        try {
            var pkt = Reconnect.CreatePacket();
            pkt.GameId = 42;
            try {
                pkt.Handle();
            } finally {
                pkt.ReturnPacket();
            }

            Equal(true, Client.IsReconnecting);
            Equal(1, requested.Count);
            Equal(42, requested[0]);
            // The transfer must not leak a Hello onto the old socket: the
            // server drops it (State != Handshaked) and closes the socket.
            Equal(0, hellos);
            // The old session must tear down synchronously inside
            // ReconnectTo, BEFORE the old socket is closed: a pending
            // ReceiveAsync completed by Close() would otherwise observe a
            // live session and run Disconnect("Unknown"), wiping the fresh
            // handshake back to the character list. With State already
            // Disconnected, such a stale callback hits the guard in
            // StaleDisconnectKeepsSession instead.
            Equal(ConnectionState.Disconnected, Client.State);
        } finally {
            Client.ReconnectSink = previousSink;
            Client.OutgoingSink = previousOutgoing;
            Client.State = previousState;
            Client.IsReconnecting = previousReconnecting;
            Map.Entities.Clear();
            Map.EntityStorage.Clear();
        }
    }

    // A Disconnect that arrives with no live connection (stale socket
    // callback during a portal transfer, duplicate Failure) must not wipe
    // the map or yank the session back to the character list.
    private static void StaleDisconnectKeepsSession() {
        var previousState = Client.State;
        var previousName = Map.Name;
        var previousDisplay = Map.DisplayName;
        Client.State = ConnectionState.Disconnected;
        Map.Name = "Realm";
        Map.DisplayName = "Realm";
        try {
            Client.Disconnect("stale probe");
            Equal("Realm", Map.Name);
            Equal("Realm", Map.DisplayName);
            Equal(ConnectionState.Disconnected, Client.State);
        } finally {
            Client.State = previousState;
            Map.Name = previousName;
            Map.DisplayName = previousDisplay;
            Map.Entities.Clear();
            Map.EntityStorage.Clear();
        }
    }

    // Between Escape/ReconnectTo and MapInfo the old world's simulation
    // can still fire (stale projectiles, autofire). Those packets would
    // land on the new socket before Load, where the server has no Player
    // yet (server NRE -> nexus keybinds disconnecting instead of
    // transferring). Only the handshake itself may emit mid-transfer.
    private static void HandshakeMutesStaleGamePackets() {
        var previousReconnecting = Client.IsReconnecting;
        var previousOutgoing = Client.OutgoingSink;
        var seen = new List<string>();
        Client.OutgoingSink = pkt => seen.Add(pkt.GetType().Name);
        try {
            Client.IsReconnecting = true;
            Client.QueuePacket(PlayerHit.CreatePacket());
            Client.QueuePacket(PlayerShoot.CreatePacket());
            Client.QueuePacket(EnemyHit.CreatePacket());
            Client.QueuePacket(Move.CreatePacket());
            Client.QueuePacket(Hello.CreatePacket());
            Client.QueuePacket(Load.CreatePacket());
            Client.QueuePacket(Create.CreatePacket());
            Equal(3, seen.Count);
            Equal("Hello", seen[0]);
            Equal("Load", seen[1]);
            Equal("Create", seen[2]);

            Client.IsReconnecting = false;
            Client.QueuePacket(PlayerHit.CreatePacket());
            Client.QueuePacket(Move.CreatePacket());
            Equal(5, seen.Count);
        } finally {
            Client.OutgoingSink = previousOutgoing;
            Client.IsReconnecting = previousReconnecting;
        }
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }
}
