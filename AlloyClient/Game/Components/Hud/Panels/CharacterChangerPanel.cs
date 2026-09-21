using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Networking;
using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Ui.Flash;

namespace AlloyClient.Game.Components.Hud.Panels;

// Flash parity (com.company.assembleegameclient.ui.panels.CharacterChangerPanel
// via ButtonPanel "Change Characters"/"Change"): 18px bold white centered
// title at y=6, 16px Flash TextButton centered at y=HEIGHT-h-4. Flash
// dispatches gs_.closed, which leaves the game for character selection; here
// that is Client.Disconnect, which resets the map and fades back to the
// CharacterListScreen.
public class CharacterChangerPanel : Panel {

    public const string TitleText = "Change Characters";

    public const string ButtonText = "Change";

    public CharacterChangerPanel() {
        var title = new SimpleText(new TextConfig {
            Text = TitleText,
            FontSize = 18,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            MaxWidth = PanelWidth,
            DropShadow = FlashTextFilters.Default,
            Anchor = UiAnchor.MiddleTop
        });
        title.X = PanelWidth / 2;
        title.Y = 6;
        AddChild(title);

        var changeButton = new TextButton(new TextButtonConfig {
            Text = ButtonText,
            FontSize = 16,
            FontType = FontType.Bold,
            FlashBackground = true,
            OnClicked = LeaveToCharacterSelect
        });
        changeButton.X = PanelWidth / 2 - changeButton.Width / 2;
        changeButton.Y = PanelHeight - changeButton.Height - 4;
        AddChild(changeButton);
    }

    // Named action so the Change button and the Interact key share one path.
    public void LeaveToCharacterSelect() {
        RequestCharacterChange();
    }

    // The leave action is injectable so the close dispatch is unit-testable
    // without tearing down the live connection (font/engine init and the
    // stage are unavailable headless).
    public static void RequestCharacterChange(Action leaveGame = null) {
        (leaveGame ?? (() => Client.Disconnect()))();
    }

    protected override void OnInteractKey() {
        LeaveToCharacterSelect();
    }
}
