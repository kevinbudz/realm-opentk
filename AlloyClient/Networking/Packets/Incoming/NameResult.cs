using AlloyClient.Game.Components.Hud.Panels;

namespace AlloyClient.Networking.Packets.Incoming;

public class NameResult : IncomingPacket<NameResult> {
    public bool Success;
    public string ErrorText;

    public override PacketId PacketId => PacketId.NameResult;

    public override void Reset() {
        Success = false;
        ErrorText = null;
    }

    public override void Read(ref SpanReader reader) {
        Success = reader.ReadBoolean();
        ErrorText = reader.ReadUTF();
    }

    // Flash parity (ChooseNameFrameMediator.onNameResult): success applies
    // the pending name to the local player, failure surfaces the error text.
    public override void Handle() {
        NameChangerPanel.ApplyNameResult(Success, ErrorText);
    }

    public override string ToString() {
        return $"Success: {Success}, ErrorText: {ErrorText}";
    }
}