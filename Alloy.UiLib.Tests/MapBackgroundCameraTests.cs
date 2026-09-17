using AlloyClient.Game;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

// Flash draws the background map through the non-perspective camera path at
// a fixed scale of 50: one tile covers exactly 50 screen pixels. These
// assertions lock that framing for the OpenTK port.
internal static class MapBackgroundCameraTests {
    public static void Run() {
        var camera = Camera.Update(new Vector2(10f, 10f), new Vector3i(800, 600, 0), 0f, 1f);
        var viewport = new Vector2i(800, 600);

        var center = camera.WorldToScreen(new Vector3(10f, 10f, 0f), viewport);
        Within(1, 400, center.X);
        Within(1, 300, center.Y);

        var right = camera.WorldToScreen(new Vector3(11f, 10f, 0f), viewport);
        Within(1, 50, right.X - center.X);
        Within(1, 0, right.Y - center.Y);

        // The menu camera uses a fixed 7*PI/4 angle; rotation must preserve
        // the 50-pixel tile size.
        var angled = Camera.Update(new Vector2(10f, 10f), new Vector3i(800, 600, 0), 7f * MathF.PI / 4f, 1f);
        var a = angled.WorldToScreen(new Vector3(10f, 10f, 0f), viewport);
        var b = angled.WorldToScreen(new Vector3(11f, 10f, 0f), viewport);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        if (MathF.Abs(length - 50f) > 1f) {
            throw new Exception($"expected one tile to span ~50px, got {length}");
        }
    }

    private static void Within(int tolerance, int expected, int actual) {
        if (Math.Abs(expected - actual) > tolerance) {
            throw new Exception($"expected {expected} +/- {tolerance}, got {actual}");
        }
    }
}
