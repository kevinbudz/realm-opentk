using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;

namespace AlloyClient.Ui.Components.Tooltips;

public abstract class Tooltip : Sprite {
    private NineSliceRect _background;

    public int ToolWidth;
    public int ToolHeight;

    protected Tooltip(int width, int height) {
        TooltipMode = true;
        // Flash ToolTip disables mouse input so the tooltip never steals hover.
        MouseEnabled = false;

        ToolWidth = width;
        ToolHeight = height;
    }

    // Flash ToolTip.forcePostionLeft: pin the tooltip left/above the cursor.
    public void ForcePositionLeft() {
        ForceTooltipLeft = true;
        ForceTooltipRight = false;
    }

    // Flash ToolTip.forcePostionRight: pin the tooltip right/below the cursor.
    public void ForcePositionRight() {
        ForceTooltipRight = true;
        ForceTooltipLeft = false;
    }

    public virtual void DrawSprite() {
        if (_background is not null) {
            _background.Resize(ToolWidth, ToolHeight);
            return;
        }

        var backgroundConfig = new NineSliceConfig {
            SliceData = SliceLibrary.TooltipBackgroundSmall,
            CutX = 5,
            CutY = 5,
            Width = ToolWidth,
            Height = ToolHeight,
        };

        _background = new NineSliceRect(backgroundConfig);
        AddChildAt(_background, 0);
    }
}
