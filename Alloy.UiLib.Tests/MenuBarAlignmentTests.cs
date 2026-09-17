using Alloy.UiLib.Core;
using Alloy.UiLib.Utils;
using AlloyClient.Ui.Components.Graphics;

namespace Alloy.UiLib.Tests;

// TitleScreen centers every menu button on the ribbon (Middle-family anchor
// at MenuCenterY) so 22pt and 36pt labels share one vertical center. The
// other menu screens must use the same Y/anchor scheme; Top-anchored buttons
// at fixed top Y values drift apart as soon as heights differ.
internal static class MenuBarAlignmentTests {
    public static void Run() {
        Equal(524, TitleMenuRibbon.TopY);
        Equal(52, TitleMenuRibbon.RibbonHeight);
        Equal(553, TitleMenuRibbon.MenuCenterY);
        Equal(TitleMenuRibbon.TopY + TitleMenuRibbon.RibbonHeight / 2 + 3, TitleMenuRibbon.MenuCenterY);

        if (TitleMenuRibbon.MenuCenterY < TitleMenuRibbon.TopY ||
            TitleMenuRibbon.MenuCenterY > TitleMenuRibbon.TopY + TitleMenuRibbon.RibbonHeight)
            throw new Exception("MenuCenterY falls outside the menu ribbon.");

        // Representative button heights: the small (22pt) and large (36pt)
        // menu labels have different content heights.
        const int smallHeight = 30;
        const int largeHeight = 50;
        const int width = 100;

        // Middle-family anchors at MenuCenterY share one vertical center
        // regardless of content height.
        foreach (var anchor in new[] { UiAnchor.Middle, UiAnchor.MiddleLeft, UiAnchor.MiddleRight }) {
            Equal(TitleMenuRibbon.MenuCenterY, CenterY(anchor, width, smallHeight, TitleMenuRibbon.MenuCenterY));
            Equal(TitleMenuRibbon.MenuCenterY, CenterY(anchor, width, largeHeight, TitleMenuRibbon.MenuCenterY));
        }

        // Top-anchored buttons cannot share a center across font sizes, even
        // at a common top Y.
        if (CenterY(UiAnchor.LeftTop, width, smallHeight, 532) == CenterY(UiAnchor.LeftTop, width, largeHeight, 532))
            throw new Exception("Top-anchored buttons unexpectedly share a center across heights.");

        // The old split top values (520 for 36pt, 532 for 22pt) did not align centers either.
        if (CenterY(UiAnchor.LeftTop, width, smallHeight, 532) == CenterY(UiAnchor.MiddleTop, width, largeHeight, 520))
            throw new Exception("Legacy menu button tops unexpectedly share a center.");

        // Switching only the vertical anchor component preserves horizontal
        // placement, so fixing Y never moves X: LeftTop->MiddleLeft keeps the
        // left edge, MiddleTop->Middle keeps the center.
        Equal(
            UiAnchor.LeftTop.GetOffset(width, smallHeight).Item1,
            UiAnchor.MiddleLeft.GetOffset(width, smallHeight).Item1);
        Equal(
            UiAnchor.MiddleTop.GetOffset(width, smallHeight).Item1,
            UiAnchor.Middle.GetOffset(width, smallHeight).Item1);
    }

    private static float CenterY(UiAnchor anchor, int w, int h, int y) {
        var (_, offsetY) = anchor.GetOffset(w, h);
        return y + offsetY + h / 2f;
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}.");
    }
}
