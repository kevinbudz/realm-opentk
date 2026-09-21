using AlloyClient.Game.Objects;
using AlloyClient.Networking;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Ui.Flash;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Ui.Components.Buttons;

namespace AlloyClient.Game.Components.Hud.Panels;

// Flash parity (com.company.assembleegameclient.ui.panels.GuildHallPortalPanel):
// 18px bold white centered "Guild Hall" at y=6; guild members get a 16px Flash
// TextButton "Enter" centered at y=HEIGHT-h-4 (click or Interact key sends
// UsePortal), guildless players get 18px bold red "Not In Guild" at
// y=HEIGHT-h-12 and can never send.
public class GuildHallPortalPanel : Panel {

    private readonly Entity _portal;

    public GuildHallPortalPanel(Entity entity) {
        _portal = entity;

        var name = new SimpleText(new TextConfig {
            Text = "Guild Hall",
            FontSize = 18,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            MaxWidth = PanelWidth,
            DropShadow = FlashTextFilters.Default,
            Anchor = UiAnchor.MiddleTop
        });
        name.X = PanelWidth / 2;
        name.Y = 6;
        AddChild(name);

        if (HasGuild(Map.LocalPlayer)) {
            var enterButton = new TextButton(new TextButtonConfig {
                Text = "Enter",
                FontSize = 16,
                FontType = FontType.Bold,
                FlashBackground = true,
                OnClicked = SendUsePortal
            });
            enterButton.X = PanelWidth / 2 - enterButton.Width / 2;
            enterButton.Y = PanelHeight - enterButton.Height - 4;
            AddChild(enterButton);
        } else {
            var noGuildText = new SimpleText(new TextConfig {
                Text = "Not In Guild",
                FontSize = 18,
                FontType = FontType.Bold,
                Color = 0xFF0000,
                MaxWidth = PanelWidth,
                DropShadow = FlashTextFilters.Default,
                Anchor = UiAnchor.MiddleTop
            });
            noGuildText.X = PanelWidth / 2;
            AddChild(noGuildText);
            noGuildText.Y = PanelHeight - noGuildText.Height - 12;
        }
    }

    // Flash parity (p.guildName_ != null && length > 0). Static and
    // null-safe so the guild gate is unit-testable headless.
    public static bool HasGuild(Player player) => !string.IsNullOrEmpty(player?.Guild);

    // Named action so the Enter button and the Interact key share one path.
    // Guildless players never send (Flash never wires the button or key).
    public void SendUsePortal() {
        TrySendUsePortal(_portal);
    }

    // Static so the gated send is unit-testable without constructing UI
    // (font/engine init is unavailable headless). Returns false when blocked.
    public static bool TrySendUsePortal(Entity entity) {
        if (entity == null || !HasGuild(Map.LocalPlayer))
            return false;

        var pkt = UsePortal.CreatePacket();
        pkt.ObjectId = entity.ObjectId;
        Client.QueuePacket(pkt);
        return true;
    }

    protected override void OnInteractKey() {
        SendUsePortal();
    }
}
