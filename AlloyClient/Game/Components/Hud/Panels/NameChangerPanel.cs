using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Display;
using AlloyClient.Game.Objects;
using AlloyClient.Networking;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Ui.Components.Dialogs;
using AlloyClient.Ui.Components.Elements;
using AlloyClient.Ui.Flash;

namespace AlloyClient.Game.Components.Hud.Panels;

// Flash parity (kabam.rotmg.game.view.NameChangerPanel + NameChangerPanelMediator
// + ChooseNameFrame/Mediator): the interact panel for NameChanger objects.
// The panel branches on the local player's nameChosen_/stars vs the rank
// requirement: named players get a "Change" buy button, unnamed players with
// enough stars get "Choose", and the rest see the rank requirement. Either
// button opens the ChooseNameFrame dialog for the actual text entry (it is a
// purchase when the player already has a name); the dialog sends ChooseName,
// and NameResult applies the pending name on success or surfaces the server
// error in the dialog on failure.
public class NameChangerPanel : Panel {

    // Flash parity (Parameters.NAME_CHANGE_PRICE).
    public const int NameChangePrice = 1000;

    // Flash parity (ChooseNameFrame nameInput maxChars).
    public const int MaxNameLength = 10;

    // The name awaiting server confirmation (ChooseNameFrameMediator.name).
    public static string PendingName;

    // The last NameResult failure text (ChooseNameFrame.setError).
    public static string LastError;

    // Fired by ApplyNameResult so the open ChooseNameDialog can close on
    // success or show the error on failure (NameChangedSignal/onNameResult).
    public static event Action<bool> OnNameResult;

    public enum NamePanelState {
        Buy,
        Choose,
        RankRequired
    }

    private readonly int _rankRequired;

    private readonly SimpleText _titleText;
    private Sprite _actionRow;
    private Sprite _rankRow;

    public NameChangerPanel(Entity entity, int rankRequired = 0) {
        _rankRequired = rankRequired;

        var player = Map.LocalPlayer;

        _titleText = new SimpleText(new TextConfig {
            Text = GetTitleText(player?.Name, player?.NameChosen ?? false),
            FontSize = 18,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            MaxWidth = PanelWidth,
            DropShadow = FlashTextFilters.Default,
            Anchor = UiAnchor.MiddleTop
        });
        _titleText.X = PanelWidth / 2;
        AddChild(_titleText);

        Layout(GateState(player));
        AddEventListener(Event.AddedToStage, () => { AddEventListener(Event.EnterFrame, OnFrameEnter); });
        AddEventListener(Event.RemovedFromStage, () => { RemoveEventListener(Event.EnterFrame, OnFrameEnter); });
    }

    private static NamePanelState GateState(Player player) {
        return GetPanelState(player?.NameChosen ?? false, player?.Stars ?? 0, 0);
    }

    // Named action so the button and the Interact key share one path. Flash
    // dispatches chooseName, and the mediator opens the ChooseNameFrame (or a
    // register prompt for guests); Alloy has no guest flow, so the dialog
    // opens directly, once.
    public void SubmitName() {
        if (ChooseNameDialog.IsOpen)
            return;
        DialogManager.Enqueue(new ChooseNameDialog());
    }

    protected override void OnInteractKey() {
        SubmitName();
    }

    private void Layout(NamePanelState state) {
        var player = Map.LocalPlayer;
        var nameChosen = player?.NameChosen ?? false;

        _titleText.SetText(GetTitleText(player?.Name, nameChosen));
        _titleText.Y = state == NamePanelState.Choose ? 6 : 0;

        if (_actionRow != null && Contains(_actionRow)) {
            RemoveChild(_actionRow);
            _actionRow = null;
        }
        if (_rankRow != null && Contains(_rankRow)) {
            RemoveChild(_rankRow);
            _rankRow = null;
        }

        if (state == NamePanelState.RankRequired) {
            _rankRow = BuildRankReqRow(_rankRequired);
            _rankRow.X = PanelWidth / 2 - _rankRow.Width / 2;
            _rankRow.Y = PanelHeight - _rankRow.Height / 2 - 20;
            AddChild(_rankRow);
        } else if (state == NamePanelState.Buy) {
            var buy = new LegacyBuyButton("Change ", 16, NameChangePrice, LegacyBuyButton.Gold, SubmitName);
            _actionRow = buy;
            _actionRow.X = PanelWidth / 2 - _actionRow.Width / 2;
            _actionRow.Y = PanelHeight - _actionRow.Height / 2 - 17;
            AddChild(_actionRow);
        } else {
            var choose = new TextButton(new TextButtonConfig {
                Text = "Choose",
                FontSize = 16,
                FontType = FontType.Bold,
                FlashBackground = true,
                OnClicked = SubmitName
            });
            _actionRow = choose;
            _actionRow.X = PanelWidth / 2 - _actionRow.Width / 2;
            _actionRow.Y = PanelHeight - _actionRow.Height - 4;
            AddChild(_actionRow);
        }
    }

    private void OnFrameEnter() {
        var player = Map.LocalPlayer;
        var state = GetPanelState(player?.NameChosen ?? false, player?.Stars ?? 0, _rankRequired);
        Layout(state);
    }

    // Flash parity (SellableObjectPanel.createRankReqText shape).
    private static Sprite BuildRankReqRow(int rankRequired) {
        var row = new Container(new ContainerConfig());
        var required = new SimpleText(new TextConfig {
            Text = "Rank Required:",
            FontSize = 16,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            DropShadow = FlashTextFilters.Default
        });
        row.AddChild(required);
        var badge = new RankText(rankRequired, largeText: false, includePrefix: false);
        badge.X = required.Width + 4;
        badge.Y = (required.Height - badge.Height) / 2;
        row.AddChild(badge);
        return row;
    }

    // Flash parity (NameChangerPanel.makeNameText / ctor title branch).
    public static string GetTitleText(string playerName, bool nameChosen) {
        return nameChosen ? $"Your name is:\n{playerName ?? ""}" : "Choose Account Name";
    }

    // Flash parity (LegacyBuyButton "Change " + price vs TextButton "Choose").
    public static string GetActionText(bool nameChosen) {
        return nameChosen ? $"Change ({NameChangePrice} Gold)" : "Choose";
    }

    // Flash parity (NameChangerPanel ctor branches): named players buy,
    // unnamed players below the rank see the requirement, the rest choose.
    public static NamePanelState GetPanelState(bool nameChosen, int stars, int rankRequired) {
        if (nameChosen)
            return NamePanelState.Buy;
        return stars < rankRequired ? NamePanelState.RankRequired : NamePanelState.Choose;
    }

    // Flash parity (ChooseNameFrame nameInput restrict A-Za-z, maxChars 10).
    // Static so the send gate is unit-testable without constructing UI
    // (font/engine init is unavailable headless).
    public static bool IsValidName(string name) {
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            return false;
        foreach (var c in name) {
            if ((c < 'A' || c > 'Z') && (c < 'a' || c > 'z'))
                return false;
        }
        return true;
    }

    // Flash parity (GameServerConnection.chooseName): validates like the
    // frame, records the pending name (ChooseNameFrameMediator.name), and
    // queues ChooseName. Returns false without sending when invalid.
    public static bool TrySendChooseName(string name) {
        if (!IsValidName(name)) {
            LastError = "Invalid name.";
            return false;
        }

        PendingName = name;
        LastError = null;

        var pkt = ChooseName.CreatePacket();
        pkt.Name = name;
        Client.QueuePacket(pkt);
        return true;
    }

    // Flash parity (ChooseNameFrameMediator.onNameResult): success sets the
    // model name and clears the dialog; failure shows the error and keeps
    // the typed name for retry.
    public static void ApplyNameResult(bool success, string errorText) {
        if (success) {
            var player = Map.LocalPlayer;
            if (player != null && PendingName != null) {
                player.Name = string.Intern(PendingName);
                player.NameChosen = true;
                player.RenderBaseType?.SetName(PendingName);
            }
            PendingName = null;
            LastError = null;
        } else {
            LastError = errorText;
        }
        OnNameResult?.Invoke(success);
    }
}
