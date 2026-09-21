using AlloyClient.Game;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

/// <summary>The world focuses the camera at ((W-Z)/2, H/2) screen px, where Z is
/// the HUD offset. When that lands on a half-pixel (odd W-Z or odd H), Nearest
/// sampling rasterizes the 1px shader outline asymmetrically (2px ring on one
/// side, 1px on the other, mirroring with sprite flip). The projection must snap
/// the focus to integer pixels so the outline stays symmetric at every size.</summary>
internal static class CameraSnapTests {
    internal static void Run() {
        FocusLandsOnIntegerPixels(800, 600, 200);
        FocusLandsOnIntegerPixels(1280, 800, 266);
        FocusLandsOnIntegerPixels(800, 1000, 333);
        FocusLandsOnIntegerPixels(801, 601, 200);
        FocusLandsOnIntegerPixels(1920, 1081, 360);
        OffsetFramingMatchesFlash();
    }

    private static void FocusLandsOnIntegerPixels(int w, int h, int z) {
        var pos = new Vector2(50f, 50f);
        var cam = Camera.Update(pos, new Vector3i(w, h, z), 0f, 1f);
        var clip = new Vector4(pos.X, pos.Y, 0f, 1f) * cam.Matrix;
        var px = (clip.X / clip.W + 1f) * 0.5f * w;
        var py = (1f - clip.Y / clip.W) * 0.5f * h;
        if (MathF.Abs(px - MathF.Round(px)) > 1e-3f || MathF.Abs(py - MathF.Round(py)) > 1e-3f) {
            throw new Exception($"focus at ({px}, {py}) for {w}x{h} (hud {z}) is not pixel-snapped");
        }
    }

    /// <summary>Flash parity (map/Camera.correctViewingArea, Map.draw): the
    /// "Center On Player" option and its hotkey move the player between the
    /// middle of the screen and 3/4 down it, without changing the horizontal
    /// centering. The offset focus must stay pixel-snapped too.</summary>
    private static void OffsetFramingMatchesFlash() {
        var pos = new Vector2(50f, 50f);
        var viewport = new Vector2i(800, 600);
        var centered = Camera.Update(pos, new Vector3i(800, 600, 200), 0f, 1f, true);
        var offset = Camera.Update(pos, new Vector3i(800, 600, 200), 0f, 1f, false);
        var c = centered.WorldToScreen(new Vector3(50f, 50f, 0f), viewport);
        var o = offset.WorldToScreen(new Vector3(50f, 50f, 0f), viewport);
        if (Math.Abs(c.X - 300) > 1 || Math.Abs(o.X - 300) > 1) {
            throw new Exception($"expected focus x 300 in both modes, got {c.X} vs {o.X}");
        }
        if (Math.Abs(c.Y - 300) > 1 || Math.Abs(o.Y - 450) > 1) {
            throw new Exception($"expected focus y 300 centered / 450 offset, got {c.Y} vs {o.Y}");
        }

        foreach (var (w, h, z) in new[] { (800, 600, 200), (801, 601, 200), (1920, 1081, 360) }) {
            var cam = Camera.Update(pos, new Vector3i(w, h, z), 0f, 1f, false);
            var clip = new Vector4(pos.X, pos.Y, 0f, 1f) * cam.Matrix;
            var px = (clip.X / clip.W + 1f) * 0.5f * w;
            var py = (1f - clip.Y / clip.W) * 0.5f * h;
            if (MathF.Abs(px - MathF.Round(px)) > 1e-3f || MathF.Abs(py - MathF.Round(py)) > 1e-3f) {
                throw new Exception($"offset focus at ({px}, {py}) for {w}x{h} (hud {z}) is not pixel-snapped");
            }
        }
    }
}
