using System;

namespace AlloyClient.Networking.Structs.DataObjects;

public struct ObjectStats : IDataObject {
    public int Id;
    public Position Position;
    public int StatOffset;
    public int StatCount;
    
    public static StatData[] StatsPool = new StatData[4096];
    public static int StatsPoolIndex = 0; // resets each packet

    public void Reset() {
        Id = 0;
        Position.Reset();
        StatOffset = 0;
        StatCount = 0;
    }

    public void Read(ref SpanReader reader) {
        Read(ref reader, ref StatsPool, ref StatsPoolIndex);
    }

    //Per-packet overload: the shared static pool aliases across queued
    //packets (network thread reads ahead of the game thread's Handle),
    //so Update/NewTick keep instance pools and read into those instead.
    public void Read(ref SpanReader reader, ref StatData[] pool, ref int index) {
        Id = reader.ReadInt32();
        Position.Read(ref reader);

        var len = reader.ReadByte();
        StatOffset = index;
        StatCount = len;

        if (index + len > pool.Length)
            Array.Resize(ref pool, (index + len) * 2);

        for (int i = 0; i < len; i++)
            pool[index++].Read(ref reader);
    }

    public void Write(ref SpanWriter writer) {
        writer.Write(Id);
        Position.Write(ref writer);

        writer.Write((byte)StatCount);

        for (var i = 0; i < StatCount; i++) {
            StatsPool[StatOffset + i].Write(ref writer);
        }
    }

    public override string ToString() {
        return $"Id: {Id}, Position: {Position}";
    }
}