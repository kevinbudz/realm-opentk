using System;
using AlloyClient.Game;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Networking.Structs.DataObjects;
using Microsoft.Extensions.Logging;

namespace AlloyClient.Networking.Packets.Incoming;

public class NewTick : IncomingPacket<NewTick> {

    public override PacketId PacketId => PacketId.NewTick;

    //Instance buffers: the network thread reads packets ahead of the game
    //thread's Handle, so shared static buffers let a later packet
    //overwrite an earlier queued one.
    private ObjectStats[] _statsBuffer = new ObjectStats[256];
    private StatData[] _statsPool = new StatData[1024];
    private int _statsPoolIndex;
    private StatData[] _playerStats = new StatData[64];

    public int ObjectStatsCount;
    public ObjectStats[] ObjectStats => _statsBuffer;
    public int PlayerStatCount;
    public StatData[] PlayerStats => _playerStats;

    public override void Reset() {
        ObjectStatsCount = 0;
        PlayerStatCount = 0;
    }

    public override void Read(ref SpanReader reader) {
        _statsPoolIndex = 0;

        ObjectStatsCount = reader.ReadInt16();
        EnsureCapacity(ref _statsBuffer, ObjectStatsCount);
        for (int i = 0; i < ObjectStatsCount; i++)
            _statsBuffer[i].Read(ref reader, ref _statsPool, ref _statsPoolIndex);

        //Appended private player stats (inventory, currency, potion
        //stacks...), sent only when non-empty. The Flash client applies
        //these to the local player; dropping them lost those updates.
        PlayerStatCount = 0;
        if (reader.Remaining > 0) {
            PlayerStatCount = reader.ReadByte();
            EnsureCapacity(ref _playerStats, PlayerStatCount);
            for (int i = 0; i < PlayerStatCount; i++)
                _playerStats[i].Read(ref reader);
        }
    }

    private static void EnsureCapacity<T>(ref T[] array, int needed) {
        if (array.Length < needed)
            array = new T[needed * 2];
    }

    public override void Handle() {
        if (Map.LocalPlayer != null) {
            var move = Move.CreatePacket();
            move.Time = Environment.TickCount;
            move.NewPosition = new Position {
                X = Map.LocalPlayer.Position.X,
                Y = Map.LocalPlayer.Position.Y
            };

            Client.QueuePacket(move);
            Map.LocalPlayer.OnMove();
        }

        for (int i = 0; i < ObjectStatsCount; i++)
            ProcessObjectStats(_statsBuffer[i]);

        if (PlayerStatCount > 0 && Map.LocalPlayer != null)
            Map.LocalPlayer.UpdateStats(_playerStats, 0, PlayerStatCount);
    }

    private void ProcessObjectStats(ObjectStats stats) {
        if (!Map.Entities.TryGetValue(stats.Id, out var en)) {
            Client.Logger.Log(LogLevel.Warning, $"[NewTick] Unable to lookup id: {stats.Id}");
            return;
        }
        en.UpdateStats(_statsPool, stats.StatOffset, stats.StatCount);

        en.OnTickPosition(stats.Position.X, stats.Position.Y, 0, 0, stats.Id == Map.LocalPlayerId);
    }

    public override string ToString() {
        return $"ObjectStats: {ObjectStats}";
    }
}
