using System;

namespace AlloyClient.Networking.Structs.DataObjects;

public struct ObjectDef : IDataObject {
    public ushort ObjectType;
    public int Id;
    public Position Position;
    public int StatOffset; // where in StatsPool this entity's stats begin
    public int StatCount;
    
    public static StatData[] StatsPool = new StatData[4096];
    public static int StatsPoolIndex = 0; // resets each packet

    public void Reset() {
        ObjectType = 0;
        Id = 0;
        Position.Reset();
        StatOffset = 0;
        StatCount = 0;
    }

    public void Read(ref SpanReader reader) {
        Read(ref reader, ref StatsPool, ref StatsPoolIndex);
    }

    //Per-packet overload: see ObjectStats.Read. Update keeps an instance
    //pool so queued packets cannot overwrite each other's stats.
    public void Read(ref SpanReader reader, ref StatData[] pool, ref int index) {
        ObjectType = reader.ReadUInt16();
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
        writer.Write(ObjectType);
        writer.Write(Id);
        Position.Write(ref writer);

        writer.Write((short)StatCount);

        for (var i = 0; i < StatCount; i++) {
            StatsPool[StatOffset + i].Write(ref writer);
        }
    }

    public override string ToString() {
        return $"ObjectType: {ObjectType}, Id: {Id}, Position: {Position}";
    }
}