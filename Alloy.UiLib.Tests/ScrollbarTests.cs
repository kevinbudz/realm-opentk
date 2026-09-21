using Alloy.UiLib;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Ui.Components.Scrollbars;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

// Flash parity for the settings scrollbar: com.company.assembleegameclient.ui
// Scrollbar draws a gray track between up/down arrows, with geometry derived
// from the bar width (arrowHeight = width * 0.75, 5px insets). The shared
// VerticalScrollBar must keep that shape plus wheel scrolling.
internal static class ScrollbarTests {
    public static void Run() {
        // The event system logs through the engine logger factory, which is
        // only set during engine init; headless tests get a null factory.
        UiRender.LogFactory ??= NullLoggerFactory.Instance;
        LayoutMatchesFlashReference();
        HandleHeightClampsLikeFlash();
        StructureHasTrackHandleAndArrows();
        WheelScrollsByStep();
    }

    private static void LayoutMatchesFlashReference() {
        // CharacterListScrollbar hardcodes the width-16 Flash numbers.
        Equal(new ScrollbarLayout(12, 17, 365), VerticalScrollBar.CalculateLayout(16, 399));
        // Options tab bar (width 15, visible height 424).
        Equal(new ScrollbarLayout(11, 16, 392), VerticalScrollBar.CalculateLayout(15, 424));
    }

    private static void HandleHeightClampsLikeFlash() {
        // Fully visible content fills the track; Flash clamps to indicatorRect.
        Equal(365, VerticalScrollBar.CalculateHandleHeight(365, 16, 399, 399));
        // Overflowing content clamps to at least the bar width.
        Equal(16, VerticalScrollBar.CalculateHandleHeight(365, 16, 10000, 399));
        // Proportional in between: 365 * 399 / 450 = 323.
        Equal(323, VerticalScrollBar.CalculateHandleHeight(365, 16, 450, 399));
    }

    private static Container BuildClip() {
        //Like OptionTabView: a sized, clipping container so wheel events
        //dispatched inside its bounds reach the scrollbar listener.
        return new Container(new ContainerConfig { Width = 800, Height = 424, EnableClip = true });
    }

    private static VerticalScrollBar BuildBar(Container clip, List<int> seen) {
        return new VerticalScrollBar(clip, new VerticalScrollBarConfig {
            Width = 15,
            Height = 424,
            TotalContentHeight = 450,
            VisibleContentHeight = 424,
            OnValueChanged = seen.Add
        });
    }

    private static void StructureHasTrackHandleAndArrows() {
        var clip = BuildClip();
        var seen = new List<int>();
        var bar = BuildBar(clip, seen);
        // Track, handle, up arrow, down arrow.
        Equal(4, bar.NumChildren);
        // Handle starts docked at the top of the track.
        Equal(16, bar.GetChildAt(1)?.Y);
    }

    private static void WheelScrollsByStep() {
        var clip = BuildClip();
        var seen = new List<int>();
        // Attach the bar so the clip sizes to its content, like OptionTabView.
        clip.AddChild(BuildBar(clip, seen));

        var step = 424 / 20;
        var inside = new Vector2i(100, 200);
        clip.DispatchEvent(new MouseEvent(MouseEvent.ScrollVertical, inside, new Vector2(0, -1)));
        Equal(1, seen.Count);
        Equal(step, seen[0]);

        clip.DispatchEvent(new MouseEvent(MouseEvent.ScrollVertical, inside, new Vector2(0, 1)));
        Equal(2, seen.Count);
        Equal(0, seen[1]);
    }

    private static void Equal<T>(T expected, T? actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }
}
