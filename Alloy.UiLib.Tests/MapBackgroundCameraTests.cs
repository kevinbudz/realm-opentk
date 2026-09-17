using AlloyClient.Game;
using AlloyClient.Screens.Components;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

// Flash draws the background map through the non-perspective camera path at
// a fixed scale of 50: one tile covers exactly 50 screen pixels at 800x600.
// These assertions lock that framing for the OpenTK port, plus the
// height-scaled zoom that keeps taller windows from revealing the map edge.
internal static class MapBackgroundCameraTests {
    public static void Run() {
        ZoomScalesWithWindowHeight();
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

    private static void ZoomScalesWithWindowHeight() {
        var baseline = new MapBackground();
        baseline.Resize(800, 600);
        if (MathF.Abs(baseline.Zoom - 1f) > 0.001f) {
            throw new Exception($"expected baseline zoom 1, got {baseline.Zoom}");
        }

        var tall = new MapBackground();
        tall.Resize(1920, 1200);
        if (MathF.Abs(tall.Zoom - 2f) > 0.001f) {
            throw new Exception($"expected zoom 2 at 1200px height, got {tall.Zoom}");
        }

        // Doubling the window height must double the on-screen tile size so
        // the same vertical slice of the map stays in view.
        var shortCamera = Camera.Update(new Vector2(10f, 10f), new Vector3i(800, 600, 0), 0f, baseline.Zoom);
        var tallCamera = Camera.Update(new Vector2(10f, 10f), new Vector3i(1920, 1200, 0), 0f, tall.Zoom);
        var shortSpan = TileSpan(shortCamera, new Vector2i(800, 600));
        var tallSpan = TileSpan(tallCamera, new Vector2i(1920, 1200));
        if (MathF.Abs(tallSpan - 2f * shortSpan) > 1f) {
            throw new Exception($"expected tile size to double with height, got {shortSpan} vs {tallSpan}");
        }

        // Visible tile height must stay constant across window heights;
        // otherwise fullscreen reveals the map edge.
        var shortVisible = 600f / (Camera.BaseCameraZoom * baseline.Zoom);
        var tallVisible = 1200f / (Camera.BaseCameraZoom * tall.Zoom);
        if (MathF.Abs(shortVisible - tallVisible) > 0.001f) {
            throw new Exception($"expected constant visible height, got {shortVisible} vs {tallVisible}");
        }
    }

    private static float TileSpan(Camera camera, Vector2i viewport) {
        var a = camera.WorldToScreen(new Vector3(10f, 10f, 0f), viewport);
        var b = camera.WorldToScreen(new Vector3(11f, 10f, 0f), viewport);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static void Within(int tolerance, int expected, int actual) {
        if (Math.Abs(expected - actual) > tolerance) {
            throw new Exception($"expected {expected} +/- {tolerance}, got {actual}");
        }
    }
}
