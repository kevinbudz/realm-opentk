using System;

namespace AlloyClient.Ui.Components.Tooltips;

// Pure positioning rules for EquipmentToolTip. The view measures real
// SimpleText Width/Height values and feeds them here; keeping the arithmetic
// GL-free means the screenshot-driven regressions stay covered by unit tests.
// All positions are sprite X/Y under the anchors the view uses
// (title MiddleLeft, tier MiddleRight, everything else LeftTop).
public static class EquipmentTooltipLayout {
    public const int MaxWidth = 230;
    public const int IconSize = 40;
    public const int IconXY = 5;
    public const int Padding = 8;
    public const int Gap = 4;

    // Title starts right of the icon.
    public static int TitleX() => IconXY + IconSize + 3;

    // Right edge available to the title: left edge of the measured tier tag,
    // or the padded tooltip edge when there is no tag.
    public static int TitleRight(int? tierRightX, int? tierWidth) =>
        tierRightX.HasValue ? tierRightX.Value - tierWidth!.Value : MaxWidth - Padding;

    public static int TitleWidth(int titleX, int titleRight) => titleRight - titleX - Gap;

    // Tier tag is right-anchored, so X is its right edge: inset from the
    // tooltip edge instead of overflowing past it.
    public static int TierRightX() => MaxWidth - Padding;

    // MiddleLeft-anchored title centers on the icon middle, so multi-line
    // titles grow symmetrically instead of hanging below the sprite.
    public static int TitleMiddleY() => IconXY + IconSize / 2;

    // MiddleRight-anchored tier top-aligns with the title (Flash behavior).
    public static int TierMiddleY(int titleMiddleY, int titleH, int tierH) =>
        titleMiddleY - (titleH - tierH) / 2;

    // Description starts below the icon, pushed down when a wrapped title
    // extends past it.
    public static int DescY(int titleMiddleY, int titleH) => Math.Max(
        IconXY + IconSize + 3,
        titleMiddleY + titleH / 2 + Gap);

    // Effect label matches Flash's "Name: value" single-space gap: the label
    // carries the trailing space and the value starts exactly at its width.
    public static string EffectLabel(string name) => name + ": ";
}
