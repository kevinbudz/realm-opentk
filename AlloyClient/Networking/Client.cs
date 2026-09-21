using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AlloyClient.Data;
using AlloyClient.Display;
using AlloyClient.Game;
using AlloyClient.Logging;
using AlloyClient.Networking.Packets;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Screens;
using Microsoft.Extensions.Logging;

namespace AlloyClient.Networking;

public enum ConnectionState {
    Disconnected,
    Connected
}

public static class Client {
    public const int RECV_BUFFER_SIZE = 0x40000;
    public const int SEND_BUFFER_SIZE = 0x10000;

    public readonly static ILogger Logger = ILogger.CreateLogger(nameof(Client));

    private readonly static ConcurrentQueue<IIncomingPacket> IncomingQueue = new();

    public static ConnectionState State;

    public static bool IsReconnecting;

    private static SocketAsyncEventArgs _receiveSAEA;
    private readonly static SocketReceiveState _receiveState;
    private static SocketAsyncEventArgs _sendSAEA;
    private readonly static SocketSendState _sendState;

    private static Socket _socket;
    private static TcpClient _tcp;

    private static string _lastHost;
    private static ushort _lastPort;
    private static int _epoch;

    // Headless-test hook: when set, ReconnectTo reports the target game id
    // here instead of opening a real socket, so portal-transfer coverage can
    // run without network I/O.
    internal static Action<int>? ReconnectSink;

    // Headless-test hook: when set, QueuePacket delivers to the sink instead
    // of the socket buffer so tests can observe outgoing packets. The sink
    // must snapshot fields synchronously; the packet is returned to its pool
    // once the sink returns.
    internal static Action<IOutgoingPacket>? OutgoingSink;

    static Client() {
        _sendState = new SocketSendState();
        _receiveState = new SocketReceiveState();

        _sendSAEA = CreateSendSAEA(0);
        _receiveSAEA = CreateReceiveSAEA(0);
    }

    private static SocketAsyncEventArgs CreateSendSAEA(int epoch) {
        var args = new SocketAsyncEventArgs();
        args.UserToken = epoch;
        args.Completed += ProcessSend;
        return args;
    }

    private static SocketAsyncEventArgs CreateReceiveSAEA(int epoch) {
        var args = new SocketAsyncEventArgs();
        args.UserToken = epoch;
        args.Completed += ProcessReceive;
        return args;
    }

    private static void Reset() {
        _sendState.Reset();
        _receiveState.Reset();
    }

    public static async void Connect(string ip, ushort port, int gameId = -1) {
        _lastHost = ip;
        _lastPort = port;

        // Every connection gets its own epoch and SAEA pair. The previous
        // connection's pending ReceiveAsync still references its own SAEA
        // instance, so closing the old socket cannot corrupt the new one;
        // stale completions observe an old epoch and exit silently instead
        // of disconnecting the new connection.
        int epoch = Interlocked.Increment(ref _epoch);
        _sendSAEA = CreateSendSAEA(epoch);
        _receiveSAEA = CreateReceiveSAEA(epoch);

        Reset();

        var tcp = new TcpClient();
        tcp.NoDelay = true;

        Logger.Log(LogLevel.Information, $"Connecting to {ip}:{port}...");

        while (true) {
            try {
                await tcp.ConnectAsync(ip, port);
                break;
            } catch (SocketException e) {
                if (e.SocketErrorCode == SocketError.ConnectionRefused) {
                    Logger.Log(LogLevel.Warning, "Failed to connect to server. Retrying...");

                    await Task.Delay(1000);
                    continue;
                }

                Logger.Log(LogLevel.Error, e.ToString());
                return;
            }
        }

        // A newer ReconnectTo superseded this attempt while it was
        // connecting: drop it instead of hijacking the current socket.
        if (epoch != _epoch) {
            try { tcp.Close(); } catch { }
            return;
        }

        _tcp = tcp;
        _socket = _tcp.Client;
        if (_socket == null) {
            return;
        }

        State = ConnectionState.Connected;

        Logger.Log(LogLevel.Information, "Connected to server.");

        SendHello(gameId);

        int captured = epoch;
        Task.Run(() => ReceiveLoop(captured));
    }

    // Flash parity (GameSpriteMediator.onReconnect): a server Reconnect means
    // the target world lives behind a fresh handshake. The server only
    // accepts Hello on a new socket in Handshaked state and ignores Hello on
    // the old Connected socket, then closes the old socket after ~2s. Queueing
    // Hello on the old socket therefore always ends on the character list.
    public static void ReconnectTo(int gameId) {
        IsReconnecting = true;

        // Invalidate stale socket completions BEFORE closing the old socket.
        // Close() completes a pending ReceiveAsync, which would otherwise
        // observe the live epoch plus Connected state and run
        // Disconnect("Unknown"), wiping the fresh session back to the
        // character list. Bumping the epoch first makes such completions
        // exit silently; dropping State first engages the stale-Disconnect
        // guard even for a same-epoch observer.
        Interlocked.Increment(ref _epoch);
        State = ConnectionState.Disconnected;

        Logger.Log(LogLevel.Information, $"Reconnecting to game {gameId}...");

        Map.Entities.Clear();
        Map.EntityStorage.Clear();

        while (IncomingQueue.TryDequeue(out var stale)) {
            stale.ReturnPacket();
        }

        var sink = ReconnectSink;
        if (sink != null || OutgoingSink != null) {
            // Headless tests observe the transfer without real sockets.
            sink?.Invoke(gameId);
            return;
        }

        try { _tcp?.Close(); } catch { }
        try { _socket?.Close(); } catch { }

        Reset();

        string host = _lastHost ?? Settings.GameServerAddress;
        ushort port = _lastHost != null ? _lastPort : Settings.SelectedGameServerPort;
        Connect(host, port, gameId);
    }

    private static void ReceiveLoop(int epoch) {
        var sock = _socket;
        while (true) {
            if (epoch != _epoch) {
                return;
            }

            if (State == ConnectionState.Disconnected || sock == null || !sock.Connected) {
                if (epoch == _epoch) {
                    Disconnect("Unknown");
                }
                return;
            }

            _receiveState.PrepareSAEA(_receiveSAEA);

            if (sock.ReceiveAsync(_receiveSAEA))
                break;

            if (!HandleReceive(_receiveSAEA, epoch))
                break;
        }
    }

    private static void ProcessReceive(object sender, SocketAsyncEventArgs args) {
        int epoch = args.UserToken is int e ? e : -1;
        if (epoch != _epoch) {
            return;
        }

        if (HandleReceive(args, epoch)) {
            ReceiveLoop(epoch);
        }
    }

    private static bool HandleReceive(SocketAsyncEventArgs args, int epoch) {
        if (epoch != _epoch) {
            return false;
        }

        if (State == ConnectionState.Disconnected || _socket == null || !_socket.Connected) {
            if (epoch == _epoch) {
                Disconnect("Unknown");
            }
            return false;
        }

        // Check for any errors during the operation
        var error = args.SocketError;
        if (error != SocketError.Success && error != SocketError.IOPending) {
            string msg = null;
            if (error != SocketError.ConnectionReset) {
                msg = $"Receive SocketError.{error}";
            }

            if (epoch == _epoch) {
                Disconnect(msg);
            }
            return false;
        }

        if (args.BytesTransferred == 0) {
            if (epoch == _epoch) {
                Disconnect("Remote host closed connection.");
            }
            return false;
        }

        _receiveState.OnDataReceived(args.BytesTransferred);

        while (_receiveState.PacketReady()) {
            var pktId = (PacketId) _receiveState.ReadPacket(out var rdr);
            var pkt = PacketUtils.CreateIncomingPacket(pktId);
            try {
                // Logger.Debug($"RECEIVING {pktId}");
                pkt.Read(ref rdr);
                IncomingQueue.Enqueue(pkt);
            } catch (Exception ex) {
                Logger.Log(LogLevel.Error, $"Error handling message {pktId}: {ex.Message}");
                pkt.ReturnPacket();
            }
        }

        return true;
    }

    public static void Tick() {
        SendPendingPackets();

        while (IncomingQueue.TryDequeue(out var packet)) {
            try {
                PacketLogger.LogPacket(packet);

                packet.Handle();
            } catch (Exception ex) {
                Logger.Log(LogLevel.Error, $"Error in handler {packet.PacketId}: {ex.Message}");
            } finally {
                packet.ReturnPacket();
            }
        }
    }

    private static void SendPendingPackets() {
        if (State == ConnectionState.Disconnected || _socket == null || !_socket.Connected) {
            return;
        }

        if (!_sendState.TryBeginSend(_sendSAEA))
            return;

        if (!_socket.SendAsync(_sendSAEA))
            ProcessSend(null, _sendSAEA);
    }

    private static void ProcessSend(object sender, SocketAsyncEventArgs args) {
        int epoch = args.UserToken is int e ? e : -1;
        if (epoch != _epoch) {
            return;
        }

        while (true) {
            if (State == ConnectionState.Disconnected) {
                if (epoch == _epoch) {
                    Disconnect("Unknown");
                }
                break;
            }

            if (args.SocketError != SocketError.Success) {
                if (epoch == _epoch) {
                    Disconnect($"Send Error: {args.SocketError}");
                }
                break;
            }

            if (_sendState.OnDataSent(args)) {
                if (_socket == null || !_socket.Connected) {
                    if (epoch == _epoch) {
                        Disconnect("Unknown");
                    }
                    break;
                }
                if (_socket.SendAsync(_sendSAEA))
                    break;
            }

            break;
        }
    }

    public static void QueuePacket(IOutgoingPacket pkt) {
        try {
            if (pkt.PacketId == PacketId.Unknown)
                return;

            //Handshake window (Flash parity: nothing emits until joined):
            //between Escape/ReconnectTo and MapInfo, stale simulation
            //(old projectiles, autofire) can still queue hits and shoots.
            //Those would land on the new socket before Load, where the
            //server has no Player yet. Mute everything but the handshake.
            if (IsReconnecting && pkt.PacketId != PacketId.Hello &&
                pkt.PacketId != PacketId.Load && pkt.PacketId != PacketId.Create)
                return;

            var sink = OutgoingSink;
            if (sink != null) {
                sink(pkt);
                return;
            }

            lock (_sendState) {
                _sendState.WritePacket(pkt, (byte) pkt.PacketId);
                // Logger.Debug($"SENDING {pkt.PacketId}");
            }
        } finally {
            //WritePacket serializes synchronously, so the packet is free
            //to pool again immediately; never returning it starved the
            //pool into an allocation per packet.
            pkt.ReturnPacket();
        }
    }

    public static void Disconnect(string message = null) {
        if (State == ConnectionState.Disconnected) {
            // No live session to tear down (stale socket callback racing a
            // portal transfer, or a duplicate Failure). Returning here keeps
            // a fresh session's map and screen intact instead of silently
            // yanking it back to the character list with no log.
            Logger.Log(LogLevel.Warning, $"Ignoring stale disconnect {(message != null ? $"({message})" : "")}");
            return;
        }

        State = ConnectionState.Disconnected;

        Reset();

        _tcp?.Close();
        _socket?.Close();

        Logger.Log(LogLevel.Information, $"Disconnecting client {(message != null ? $"({message})" : "")}");

        while (IncomingQueue.TryDequeue(out var pkt)) {
            pkt.ReturnPacket();
        }

        Map.Reset();
        ScreenManager.FadeTo(new CharacterListScreen());
    }

    private static void SendHello(int gameId) {
        var login = GlobalData.Get<LoginData>();
        var hello = Hello.CreatePacket();
        hello.BuildVersion = Settings.BuildVersion;
        hello.GameId = gameId;
        hello.Username = login.Username;
        hello.Password = login.Password;
        hello.MapJSON = "";
        QueuePacket(hello);
    }
}