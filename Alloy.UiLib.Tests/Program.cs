using System.Reflection;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using Alloy.Common;
using Alloy.UiLib.Extra;
using Alloy.UiLib.Rendering;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

internal static class Program
{
    private static int _passed;
    private static int _skipped;

    public static int Main()
    {
        Run("HP bars keep world size across zoom", HpBarScaleTests.Run);
        Run("DropShadowFilter defaults", Defaults);
        Run("DropShadowFilter normalization", Normalization);
        Run("DropShadowFilter rejects non-finite values", RejectsNonFinite);
        Run("Flash tier-tag filter values", TierTagValues);
        Run("Mask scale quantization", MaskScaleQuantization);
        Run("EGL surfaceless context", EglBootstrap);
        Run("Text filter GPU pixel regression suite", GpuPixels);

        Console.WriteLine($"PASS {_passed}, SKIP {_skipped}");
        return 0;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"PASS {name}");
            _passed++;
        }
        catch (SkipTestException ex)
        {
            Console.WriteLine($"SKIP {name}: {ex.Message}");
            _skipped++;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
            throw;
        }
    }

    private static void Defaults()
    {
        var f = new DropShadowFilter();
        Equal(4f, f.Distance);
        Equal(45f, f.Angle);
        Equal(0u, f.Color);
        Equal(1f, f.Alpha);
        Equal(4f, f.BlurX);
        Equal(4f, f.BlurY);
        Equal(1f, f.Strength);
        Equal(1, f.Quality);
        Equal(false, f.Inner);
        Equal(false, f.Knockout);
        Equal(false, f.HideObject);

        // Flash's new DropShadowFilter(0, 0, 0) relies on these defaults.
        var flashDefault = new DropShadowFilter(0, 0, 0);
        Equal(0f, flashDefault.Distance);
        Equal(0f, flashDefault.Angle);
        Equal(4f, flashDefault.BlurX);
        Equal(4f, flashDefault.BlurY);
    }

    private static void Normalization()
    {
        var f = new DropShadowFilter(distance: 12, angle: 725, color: 0xFF12ABCD,
            alpha: 2, blurX: 400, blurY: -4, strength: 500, quality: 99,
            inner: true, knockout: true, hideObject: true);
        Equal(5f, f.Angle);
        Equal(0x12ABCDu, f.Color);
        Equal(1f, f.Alpha);
        Equal(255f, f.BlurX);
        Equal(0f, f.BlurY);
        Equal(255f, f.Strength);
        Equal(15, f.Quality);
        Equal(true, f.Inner);
        Equal(true, f.Knockout);
        Equal(true, f.HideObject);
    }

    private static void RejectsNonFinite()
    {
        Throws<ArgumentOutOfRangeException>(() => new DropShadowFilter(distance: float.NaN));
        Throws<ArgumentOutOfRangeException>(() => new DropShadowFilter(angle: float.PositiveInfinity));
        Throws<ArgumentOutOfRangeException>(() => new DropShadowFilter(alpha: float.NegativeInfinity));
    }

    private static void TierTagValues()
    {
        // Flash ItemTile.setTierTag applies GlowFilter(0, 1, 2, 2, 10, 1):
        // a zero-distance black glow, blur 2, strength 10, quality 1.
        var f = AlloyClient.Ui.Flash.FlashTextFilters.TierTag;
        Equal(0f, f.Distance);
        Equal(0u, f.Color);
        Equal(1f, f.Alpha);
        Equal(2f, f.BlurX);
        Equal(2f, f.BlurY);
        Equal(10f, f.Strength);
        Equal(1, f.Quality);
        Equal(false, f.Inner);
        Equal(false, f.Knockout);
        Equal(false, f.HideObject);
    }

    private static void MaskScaleQuantization()
    {
        static SpriteInstanceData Identity() => new(new SpriteVertexMatrix(new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0)),
            Color.White, Color.Black, Vector2.Zero, Vector4.Zero, Vector4.Zero, Vector4.Zero, ColorTransform.Default);
        Equal(new Vector2(1, 1), TextFilterRender.QuantizedMaskScale(Identity()));
        var scaled = Identity();
        scaled.TransformX = new Vector4(2, 0, 0, 0);
        scaled.TransformY = new Vector4(0, 2, 0, 0);
        Equal(new Vector2(2, 2), TextFilterRender.QuantizedMaskScale(scaled));
        var fractional = Identity();
        fractional.TransformX = new Vector4(1.1f, 0, 0, 0);
        fractional.TransformY = new Vector4(0, 1.1f, 0, 0);
        Equal(new Vector2(1.25f, 1.25f), TextFilterRender.QuantizedMaskScale(fractional));
        var small = Identity();
        small.TransformX = new Vector4(0.5f, 0, 0, 0);
        small.TransformY = new Vector4(0, 0.5f, 0, 0);
        Equal(new Vector2(1, 1), TextFilterRender.QuantizedMaskScale(small));
        var huge = Identity();
        huge.TransformX = new Vector4(10, 0, 0, 0);
        huge.TransformY = new Vector4(0, 10, 0, 0);
        Equal(new Vector2(4, 4), TextFilterRender.QuantizedMaskScale(huge));
        var moved = Identity();
        moved.TransformX = new Vector4(1, 0, 100, 0);
        moved.TransformY = new Vector4(0, 1, -50, 0);
        Equal(new Vector2(1, 1), TextFilterRender.QuantizedMaskScale(moved));
        var rotated = Identity();
        rotated.TransformX = new Vector4(0, -2, 0, 0);
        rotated.TransformY = new Vector4(2, 0, 0, 0);
        Equal(new Vector2(2, 2), TextFilterRender.QuantizedMaskScale(rotated));
        var broken = Identity();
        broken.TransformX = new Vector4(float.NaN, 0, 0, 0);
        Equal(new Vector2(1, 1), TextFilterRender.QuantizedMaskScale(broken));
    }

    private static void GpuPixels()
    {
        if (!EglContext.TryCreate(out var context, out var reason))
            throw new SkipTestException(reason);
        using (context!)
        {
            GLLoader.LoadBindings(new EglBindings(context!));
            GpuFilterTests.Run();
        }
    }

    private static void EglBootstrap()
    {
        if (!EglContext.TryCreate(out var context, out var reason))
            throw new SkipTestException(reason);
        using (context!)
        {
            GLLoader.LoadBindings(new EglBindings(context!));
            GL.GetString(StringName.Version);
        }
    }

    private sealed class EglBindings(EglContext context) : IBindingsContext
    {
        public IntPtr GetProcAddress(string procName) => context.GetProcAddress(procName);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception($"expected {typeof(T).Name}");
    }

    private sealed class SkipTestException(string message) : Exception(message);
}
