using Alloy.UiLib.Utils;

namespace Alloy.UiLib.Tests;

// Flash ToolTip.position() parity: 12px past the cursor in the left/top
// region, tucked before the cursor otherwise, clamped 12px inside the low
// stage edges. The vertical flip happens at a third of the stage height.
internal static class TooltipPositionTests {
    private const float StageWidth = 800;
    private const float StageHeight = 600;
    private const float ContentWidth = 230;
    private const float ContentHeight = 100;

    public static void Run() {
        // Cursor top-left: tooltip goes right and below with a 12px gap.
        Equal((112f, 112f), Place(100, 100, false, false));
        // Cursor right half: tooltip tucks left of the cursor.
        Equal((700 - ContentWidth - 1, 112f), Place(700, 100, false, false));
        // Cursor below the top third: tooltip tucks above the cursor.
        Equal((112f, 500 - ContentHeight - 1), Place(100, 500, false, false));
        // Bottom-right quadrant flips on both axes.
        Equal((700 - ContentWidth - 1, 500 - ContentHeight - 1), Place(700, 500, false, false));
        // The flip threshold is a third of the height, not a half.
        Equal((112f, 199f + 12), Place(100, 199, false, false));
        Equal((112f, 200 - ContentHeight - 1), Place(100, 200, false, false));
        // Forced sides ignore the cursor quadrant.
        Equal((700 - ContentWidth - 1, 500 - ContentHeight - 1), Place(700, 500, true, false));
        Equal((712f, 512f), Place(700, 500, false, true));
        // Forced left pins above the cursor and clamps into the stage.
        Equal((12f, 12f), Place(100, 100, true, false));
        // Oversized content clamps to the 12px low edge on the flipped axis.
        Equal((12f, 112f), Place(790, 100, false, false, 900, 100));
    }

    private static (float X, float Y) Place(float mouseX, float mouseY, bool forceLeft, bool forceRight,
        float contentWidth = ContentWidth, float contentHeight = ContentHeight) =>
        TooltipPosition.Place(mouseX, mouseY, contentWidth, contentHeight,
            StageWidth, StageHeight, forceLeft, forceRight);

    private static void Equal((float X, float Y) expected, (float X, float Y) actual) {
        if (expected != actual)
            throw new Exception($"Expected {expected}, got {actual}.");
    }
}
