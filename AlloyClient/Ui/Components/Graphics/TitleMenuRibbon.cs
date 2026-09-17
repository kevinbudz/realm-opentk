using Alloy.UiLib.BuiltIn;

namespace AlloyClient.Ui.Components.Graphics;

/// <summary>
/// Shared title-screen ribbon, positioned from the Flash MenuFrame design.
///
/// MenuFrame is authored in the 800x600 design space. The parent title
/// screen supplies the height-derived scale and expands this ribbon to the
/// physical window width, so the bar remains edge-anchored on wide windows.
/// </summary>
public sealed class TitleMenuRibbon : Container {
    // MenuFrame.as: BAR_TOP = 524, BAR_HEIGHT = 52.
    public const int TopY = 524;
    public const int RibbonHeight = 52;
    public const int MenuCenterY = TopY + RibbonHeight / 2 + 3;

    private readonly ColorRect _body;
    private readonly ColorRect _bottomEdge;

    public TitleMenuRibbon(int width)
        : base(new ContainerConfig { Width = width, Height = RibbonHeight }) {
        // Flash MenuFrame draws the bar solid across the full window width;
        // there is no horizontal fade at the edges.
        _body = new ColorRect(new ColorRectConfig {
            Height = RibbonHeight - 1,
            Color = 0x545454
        });
        AddChild(_body);

        _bottomEdge = new ColorRect(new ColorRectConfig {
            Y = RibbonHeight - 1,
            Height = 1,
            Color = 0x4C4C4C
        });
        AddChild(_bottomEdge);

        Layout(width);
    }

    public void ResizeWidth(int width) {
        Resize(width, RibbonHeight);
        Layout(width);
    }

    private void Layout(int width) {
        _body.X = 0;
        _body.Resize(System.Math.Max(0, width), RibbonHeight - 1);
        _bottomEdge.X = 0;
        _bottomEdge.Resize(System.Math.Max(0, width), 1);
    }
}
