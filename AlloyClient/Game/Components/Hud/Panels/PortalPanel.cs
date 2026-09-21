using System;
using AlloyClient.Game.Objects;
using AlloyClient.Networking;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Ui.Flash;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Ui.Components.Buttons;

namespace AlloyClient.Game.Components.Hud.Panels;

// Flash parity (com.company.assembleegameclient.ui.panels.PortalPanel):
// 18px bold white centered name (WIDTH wide, y=6, or y=0 when wrapped tall),
// 16px Flash TextButton "Enter" bottom-anchored at y=HEIGHT-h-4 centered, and
// 18px bold red "Locked"/"Full" at y=HEIGHT-h-12. Draw swaps button/label on
// locked/active and strips a leading "Locked " from the live name.
public class PortalPanel : Panel {

    private readonly Entity _portal;

    private readonly bool _locked;

    private readonly SimpleText _nameText;

    private readonly SimpleText _fullText;

    private readonly TextButton _enterButton;

    public PortalPanel(Entity entity) {
        _portal = entity;
        _locked = entity.Properties.LockedPortal;

        _nameText = new SimpleText(new TextConfig {
            Text = GetPortalName(entity),
            FontSize = 18,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            MaxWidth = PanelWidth,
            DropShadow = FlashTextFilters.Default,
            Anchor = UiAnchor.MiddleTop
        });
        _nameText.X = PanelWidth / 2;
        _nameText.Y = 6;
        AddChild(_nameText);

        _fullText = new SimpleText(new TextConfig {
            Text = _locked ? "Locked" : "Full",
            FontSize = 18,
            FontType = FontType.Bold,
            Color = 0xFF0000,
            MaxWidth = PanelWidth,
            DropShadow = FlashTextFilters.Default,
            Anchor = UiAnchor.MiddleTop
        });
        _fullText.X = PanelWidth / 2;

        _enterButton = new TextButton(new TextButtonConfig {
            Text = "Enter",
            FontSize = 16,
            FontType = FontType.Bold,
            FlashBackground = true,
            OnClicked = OnInteractKey
        });
        LayoutBottom();
        AddChild(_enterButton);

        AddEventListener(Event.AddedToStage, () => { AddEventListener(Event.EnterFrame, OnFrameEnter);});
        AddEventListener(Event.RemovedFromStage, () => { RemoveEventListener(Event.EnterFrame, OnFrameEnter);});
    }

    private void LayoutBottom() {
        _enterButton.X = PanelWidth / 2 - _enterButton.Width / 2;
        _enterButton.Y = PanelHeight - _enterButton.Height - 4;
        _fullText.Y = PanelHeight - _fullText.Height - 12;
    }

    // Flash parity (PortalPanel.draw via GameObject.getName): the panel shows
    // the live Name stat (e.g. the PortalMonitor "Realm (count)" rename) and
    // falls back to the static DisplayId when no name arrived yet. A locked
    // portal strips the leading "Locked " like Flash.
    public static string GetPortalName(Entity entity) {
        if (entity?.Properties == null)
            return "";

        var name = string.IsNullOrEmpty(entity.Name)
            ? entity.Properties.DisplayName
            : entity.Name;

        const string lockedPrefix = "Locked ";
        if (entity.Properties.LockedPortal && name.StartsWith(lockedPrefix, StringComparison.Ordinal))
            name = name[lockedPrefix.Length..];

        return name;
    }

    protected override void OnInteractKey() {
        var pkt = UsePortal.CreatePacket();
        pkt.ObjectId = _portal.ObjectId;
        Client.QueuePacket(pkt);
    }

    private void OnFrameEnter() {
        _nameText.SetText(GetPortalName(_portal));
        _nameText.Y = _nameText.Height > 30 ? 0 : 6;
        LayoutBottom();

        if ((!_portal.PortalUsable || _locked) && Contains(_enterButton)) {
            RemoveChild(_enterButton);
            AddChild(_fullText);
            LayoutBottom();
        }

        if ((_portal.PortalUsable && !_locked) && Contains(_fullText)) {
            RemoveChild(_fullText);
            AddChild(_enterButton);
            LayoutBottom();
        }
    }
}
