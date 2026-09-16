using AlloyClient.Game.Components.Hud.Inventory;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;

namespace AlloyClient.Game.Components.Hud;

public sealed class HudView : Sprite {
    // Flash GameSprite reserves a 200x600 HUD in the 800x600 design space.
    public const int HudWidth = 200;

    private const int HudHeight = 600;
    private const int MinimapPosition = 4;
    private const int CharacterDetailsPosition = 198;
    private const int StatMetersPosition = 230;
    private const int EquipmentPosition = 304;
    private const int TabStripPosition = 346;
    private const int InteractPanelPosition = 500;


    private Minimap _minimap;
    private CharacterDetails _details;
    private CharacterBars _bars;

    private EquippedGrid _equippedGrid;
    private TabStrip _tabStrip;

    private InteractPanel _interactPanel;

    public HudView() {
        SetAnchor(UiAnchor.LeftTop);

        var bg = new ColorRect(new ColorRectConfig {Width = HudWidth, Height = HudHeight, Color = 0x363636});
        AddChild(bg);

        _minimap = new Minimap();
        _minimap.X = MinimapPosition;
        _minimap.Y = MinimapPosition;
        AddChild(_minimap);

        _details = new CharacterDetails();
        _details.X = 0;
        _details.Y = CharacterDetailsPosition;
        AddChild(_details);

        _bars = new CharacterBars();
        _bars.X = 12;
        _bars.Y = StatMetersPosition;
        AddChild(_bars);
    }

    public void CreatePlayerDependentAssets() {
        RemoveChild(_equippedGrid);
        RemoveChild(_interactPanel);
        RemoveChild(_tabStrip);

        _equippedGrid = new EquippedGrid(Map.LocalPlayer);
        _equippedGrid.X = 14;
        _equippedGrid.Y = EquipmentPosition;
        AddChild(_equippedGrid);

        _tabStrip = new TabStrip(Map.LocalPlayer); //Inv + Backpack + StatView is handled under TabStrip.
        _tabStrip.X = 7;
        _tabStrip.Y = TabStripPosition;
        AddChild(_tabStrip);

        _interactPanel = new InteractPanel();
        _interactPanel.X = 0;
        _interactPanel.Y = InteractPanelPosition;
        AddChild(_interactPanel);
    }

    public void Update() {
        if (Map.LocalPlayer == null) {
            return;
        }

        _bars.Update();
        _equippedGrid.UpdateAbilitySlot();
        _interactPanel.Update();
    }
}
