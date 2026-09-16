using AlloyClient.Networking.Enums;

namespace AlloyClient.Networking.Structs.DataObjects;

public struct StatData : IDataObject {
    public StatsType Type;
    public int Value;
    public string Text;

    public void Reset() {
        Type = default;
        Value = 0;
        Text = null;
    }

    public void Read(ref SpanReader reader) {
        Type = (StatsType)reader.ReadByte();

        if (IsStringStat(Type)) {
            Text = reader.ReadUTF();
        } else {
            Value = reader.ReadInt32();
        }
    }

    public void Write(ref SpanWriter writer) {
        writer.Write((byte)Type);

        if (IsStringStat(Type)) {
            writer.WriteUTF(Text);
        }
        else {
            writer.Write(Value);
        }
    }

    //Must match RotMG.Common.ObjectStatus.IsStringStat on the server: only
    //Name and Guild travel as UTF strings, everything else is an int.
    //Treating any other stat as a string desynchronizes the whole stream.
    public static bool IsStringStat(StatsType type)
    {
        switch (type)
        {
            case StatsType.Name:
            case StatsType.Guild:
                return true;
            default:
                return false;
        }
    }

    public override string ToString() {
        return $"Type: {Type}, Value: {Value}, Text: {Text}";
    }
}