using System.Runtime.InteropServices;

namespace Alloy.UiLib.Tests;

/// Minimal surfaceless EGL bootstrap used by the renderer regression harness.
/// It deliberately has no OpenTK dependency, so context creation can be tested
/// before the UI renderer's OpenTK binding seam is finalized.
internal sealed class EglContext : IDisposable
{
    private const int EglNone = 0x3038;
    private const int EglRedSize = 0x3024, EglGreenSize = 0x3023, EglBlueSize = 0x3022;
    private const int EglRenderableType = 0x3040, EglOpenGlBit = 0x0008;
    private const int EglOpenGlApi = 0x30A2, EglContextMajor = 0x3098, EglContextMinor = 0x30FB;
    private const int EglCoreProfileMask = 0x00000001, EglOpenGlProfileMask = 0x30FD;
    private const int EglPlatformSurfaceless = 0x31DD;

    private readonly IntPtr _lib;
    private readonly IntPtr _display;
    private readonly IntPtr _context;

    private EglContext(IntPtr lib, IntPtr display, IntPtr context)
        => (_lib, _display, _context) = (lib, display, context);

    public static bool TryCreate(out EglContext? context, out string reason)
    {
        context = null;
        reason = "EGL unavailable";
        if (!OperatingSystem.IsLinux()) return false;
        var lib = NativeLibrary.Load("libEGL.so.1");
        try
        {
            var displayFn = Get<EglGetPlatformDisplay>(lib, "eglGetPlatformDisplay");
            var initialize = Get<EglInitialize>(lib, "eglInitialize");
            var bindApi = Get<EglBindApi>(lib, "eglBindAPI");
            var choose = Get<EglChooseConfig>(lib, "eglChooseConfig");
            var create = Get<EglCreateContext>(lib, "eglCreateContext");
            var makeCurrent = Get<EglMakeCurrent>(lib, "eglMakeCurrent");
            var display = displayFn(EglPlatformSurfaceless, IntPtr.Zero, IntPtr.Zero);
            if (display == IntPtr.Zero || initialize(display, out _, out _) == 0)
            { reason = "eglGetPlatformDisplay/eglInitialize failed"; NativeLibrary.Free(lib); return false; }
            if (bindApi(EglOpenGlApi) == 0)
            { reason = "eglBindAPI(EGL_OPENGL_API) failed"; NativeLibrary.Free(lib); return false; }
            int[] attrs = [0x3033, 1, EglRenderableType, EglOpenGlBit, EglRedSize, 8, EglGreenSize, 8, EglBlueSize, 8, EglNone];
            if (choose(display, attrs, out var config, 1, out var count) == 0 || count == 0)
            { reason = "no EGL OpenGL config"; NativeLibrary.Free(lib); return false; }
            int[] ctxAttrs = [EglContextMajor, 4, EglContextMinor, 5, EglOpenGlProfileMask, EglCoreProfileMask, EglNone];
            var eglContext = create(display, config, IntPtr.Zero, ctxAttrs);
            if (eglContext == IntPtr.Zero || makeCurrent(display, IntPtr.Zero, IntPtr.Zero, eglContext) == 0)
            { reason = "eglCreateContext/eglMakeCurrent failed"; NativeLibrary.Free(lib); return false; }
            context = new EglContext(lib, display, eglContext);
            reason = "";
            return true;
        }
        catch (Exception ex) { reason = ex.Message; NativeLibrary.Free(lib); return false; }
    }

    public void Dispose()
    {
        var destroy = Get<EglDestroyContext>(_lib, "eglDestroyContext");
        var terminate = Get<EglTerminate>(_lib, "eglTerminate");
        destroy(_display, _context);
        terminate(_display);
        NativeLibrary.Free(_lib);
    }

    public IntPtr GetProcAddress(string name)
    {
        var get = Get<EglGetProcAddress>(_lib, "eglGetProcAddress");
        return get(name);
    }

    private static T Get<T>(IntPtr lib, string name) where T : Delegate
        => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(lib, name));

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr EglGetPlatformDisplay(int platform, IntPtr native, IntPtr attrs);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EglInitialize(IntPtr display, out int major, out int minor);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EglBindApi(int api);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EglChooseConfig(IntPtr display, int[] attrs, out IntPtr config, int size, out int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr EglCreateContext(IntPtr display, IntPtr config, IntPtr share, int[] attrs);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EglMakeCurrent(IntPtr display, IntPtr draw, IntPtr read, IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EglDestroyContext(IntPtr display, IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EglTerminate(IntPtr display);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr EglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string name);
}
