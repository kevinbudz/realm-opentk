using AlloyClient.Data;
using AlloyClient.Game;
using AlloyClient.Networking.Packets.Outgoing;

namespace AlloyClient.Networking.Packets.Incoming;

public class MapInfo : IncomingPacket<MapInfo> {
    public int Width;
    public int Height;
    public string Name;
    public string DisplayName;
    public uint Seed;
    public int Background;
    public bool ShowDisplays;
    public bool AllowPlayerTeleport;

    public override PacketId PacketId => PacketId.MapInfo;

    public override void Reset() {
        Width = 0;
        Height = 0;
        Name = null;
        DisplayName = null;
        Seed = 0;
        Background = 0;
        ShowDisplays = false;
        AllowPlayerTeleport = false;
    }

    //Wire has no difficulty: width, height, name, displayName, seed, background,
    //showDisplays, allowPlayerTeleport.
    public override void Read(ref SpanReader reader) {
        Width = reader.ReadInt32();
        Height = reader.ReadInt32();
        Name = reader.ReadUTF();
        DisplayName = reader.ReadUTF();
        Seed = reader.ReadUInt32();
        Background = reader.ReadInt32();
        ShowDisplays = reader.ReadBoolean();
        AllowPlayerTeleport = reader.ReadBoolean();
    }

    public override void Handle() {
        Map.Reset();

        Map.InitMap(Width, Height, Name, DisplayName, 0, Seed, Background,
            AllowPlayerTeleport, ShowDisplays);

        LoadOrCreate();

        Client.IsReconnecting = false;
    }

    private static void LoadOrCreate() {
        if (GlobalData.CharacterType > 0) {
            var create = Create.CreatePacket();
            create.ClassType = GlobalData.CharacterType;
            create.SkinType = GlobalData.CharacterSkin;
            Client.QueuePacket(create);

            GlobalData.CharacterType = 0;
            GlobalData.CharacterSkin = 0;
            return;
        }
        
        var load = Load.CreatePacket();
        load.CharId = GlobalData.SelectedCharacterId;
        Client.QueuePacket(load);
    }

    public override string ToString() {
        return $"Width: {Width}, Height: {Height}, Name: {Name}, DisplayName: {DisplayName}, Seed: {Seed}, Background: {Background}, AllowPlayerTeleport: {AllowPlayerTeleport}, ShowDisplays: {ShowDisplays}";
    }
}
