using Alloy.UiLib.Core;
using Alloy.UiLib.Signals;

namespace AlloyClient.Game.Components.Hud.Panels;

public abstract class Panel : Sprite {

    // Flash parity (com.company.assembleegameclient.ui.panels.Panel):
    // panels live in a 200px HUD slot at x=6, so the content area is
    // WIDTH=188 x HEIGHT=84. Buttons and labels bottom-anchor to these.
    public const int PanelWidth = 188;

    public const int PanelHeight = 84;

    public readonly static Signal OnInteract = new();

    protected Panel() {
        AddEventListener(Event.AddedToStage, () => { OnInteract.Add(OnInteractKey); });
        AddEventListener(Event.RemovedFromStage, () => { OnInteract.Remove(OnInteractKey); });
    }

    protected virtual void OnInteractKey() {
        
    }
}