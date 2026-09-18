namespace Alloy.UiLib.Utils;

// Pure port of the Flash ToolTip.position() placement rule. The tooltip's
// top-left corner sits 12px past the cursor when the cursor is in the left
// (x < half width) or top (y < third height) region, and tucks before the
// cursor otherwise, clamped 12px inside the low stage edges. Kept GL-free
// so the placement stays covered by unit tests.
public static class TooltipPosition {
    public const float MouseGap = 12f;
    public const float EdgeGap = 12f;
    public const float FlipOverlap = 1f;

    public static (float X, float Y) Place(float mouseX, float mouseY, float contentWidth, float contentHeight,
        float stageWidth, float stageHeight, bool forceLeft, bool forceRight) {
        var right = forceRight || (!forceLeft && mouseX < stageWidth / 2);
        var below = forceRight || (!forceLeft && mouseY < stageHeight / 3);
        var x = right ? mouseX + MouseGap : mouseX - contentWidth - FlipOverlap;
        var y = below ? mouseY + MouseGap : mouseY - contentHeight - FlipOverlap;
        if (x < EdgeGap) {
            x = EdgeGap;
        }

        if (y < EdgeGap) {
            y = EdgeGap;
        }

        return (x, y);
    }
}
