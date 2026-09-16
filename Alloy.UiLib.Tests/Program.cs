using System.Reflection;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using Alloy.UiLib.Extra;

namespace Alloy.UiLib.Tests;

internal static class Program
{
    private static int _passed;
    private static int _skipped;

    public static int Main()
    {
        Run("DropShadowFilter defaults", Defaults);
        Run("DropShadowFilter normalization", Normalization);
        Run("DropShadowFilter rejects non-finite values", RejectsNonFinite);
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
