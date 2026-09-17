using AlloyClient.Game.Components.Hud.Panels;
using AlloyClient.Game.Objects;
using AlloyClient.Game.Objects.Util;
using Alloy.UiLib.Core;
using Alloy.UiLib.Signals;

namespace AlloyClient.Game.Components.Hud;

public sealed class InteractPanel : Sprite {

    public readonly static SingleSignal<Panel> AddOverride = new();

    private Entity _currentObject;

    private Panel _currentPanel;

    private Panel _overridePanel;

    private readonly PartyPanel _partyPanel = new();

    public InteractPanel() {
        AddOverride.Set(SetOverride);
    }

    private void SetOverride(Panel panel) {
        if (_overridePanel != null)
            RemoveChild(_overridePanel);

        _currentPanel.Visible = false;
        _overridePanel = panel;
        AddChild(_overridePanel);
    }
    

    public void Update() {
        if (_overridePanel != null) {
            return;
        }

        if (!EntityUtils.FindClosestInteractableInRadius(Map.LocalPlayer.Position, 1f, out var obj)) {
            _currentObject = null;
            SetPanel(_partyPanel);
            return;
        }

        if (obj == _currentObject && _currentPanel != null) {
            return;
        }
        
        _currentObject = obj;
        SetPanel(GetInteractPanel(_currentObject));
    }

    private void SetPanel(Panel panel) {
        if (panel == _currentPanel) {
            return;
        }

        RemoveChild(_currentPanel);
        _currentPanel = panel;

        if (_currentPanel == null)
            return;

        PositionPanel(_currentPanel);
        AddChild(_currentPanel);
    }

    // Flash InteractPanel.positionPanelAndAdd centers grid panels in the
    // 200px HUD slot and places every other panel at (6, 8).
    private static void PositionPanel(Panel panel) {
        panel.Y = 8;
        panel.X = panel is ContainerPanel ? (HudView.HudWidth - panel.Width) / 2 : 6;
    }
    
    public static bool IsInteractiveObject(Entity entity) {
        return entity.Properties.Class switch {
            "Container" => true,
            "OneWayContainer" => true,
            "Portal" => true,
            _ => false
        };
    }

    private static Panel GetInteractPanel(Entity entity) {
        if (entity == null)
            return null;
        
        return entity.Properties.Class switch {
            "Container" => new ContainerPanel(entity, false),
            "OneWayContainer" => new ContainerPanel(entity, true),
            "Portal" => new PortalPanel(entity),
            _ => null
        };
    }
}