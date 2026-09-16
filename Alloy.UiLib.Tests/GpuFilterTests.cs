using System.Reflection;
using Alloy.Common;
using Alloy.Common.SourceGen;
using Alloy.Engine.Graphics;
using Alloy.UiLib.Extra;
using Alloy.UiLib.Rendering;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

/// <summary>Runs the actual UI shader, batching and cache against a synthetic solid glyph.</summary>
internal static class GpuFilterTests {
    private const int Size = 64;
    private static readonly ushort[] Indices = [0, 1, 2, 0, 2, 3];
    private static int _target, _framebuffer;
    private static SpriteInstanceData _instance;

    internal static void Run() {
        var source = (ShaderSource)typeof(UiRender).GetProperty("UiShaderSource", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        UiRender.UiShader = Shader.FromSource(source);
        UiRender.GpuDraw = new GpuTimer();
        SpriteRender.Init();
        using var glyph = new Texture(new Color[] { Color.White }, 1, 1);
        using var sampler = new Sampler(glyph, TextureFilter.Linear, 6);
        UiRender.UiShader.SetValue("TextTexture", sampler);
        UiRender.UiShader.SetValue("TextTextureSize", Vector2.One);
        UiRender.UiShader.SetValue("PixelRange", 4f);
        UiRender.ViewMatrix = Matrix4.Identity;
        UiRender.ViewMatrix.M11 = 2f / Size;
        UiRender.ViewMatrix.M22 = -2f / Size;
        UiRender.ViewMatrix.M41 = -1;
        UiRender.ViewMatrix.M42 = 1;
        UiRender.UiShader.SetValue("ViewMatrix", UiRender.ViewMatrix);
        _target = GL.CreateTexture(TextureTarget.Texture2D);
        GL.TextureStorage2D(_target, 1, SizedInternalFormat.Rgba8, Size, Size);
        _framebuffer = GL.CreateFramebuffer();
        GL.NamedFramebufferTexture(_framebuffer, FramebufferAttachment.ColorAttachment0, _target, 0);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        GL.Viewport(0, 0, Size, Size);
        GL.Enable(EnableCap.Blend);
        GL.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
            BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        GL.Scissor(0, 0, Size, Size);
        GL.Enable(EnableCap.ScissorTest);
        _instance = new SpriteInstanceData(new SpriteVertexMatrix(new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0)),
            Color.White, Color.Black, new Vector2(5, 1), new Vector4(0, 0, Size, Size),
            Vector4.Zero, Vector4.Zero, ColorTransform.Default);
        try {
            OffsetAndZeroQuality();
            BlurStrengthAndQuality();
            InnerKnockoutAndHide();
            AlphaAndTintReuse();
            ClippingAndState();
            GeometryInvalidation();
            LongGeometry();
            CacheEviction();
            TextPreview.Render();
            var error = GL.GetError();
            Check(error == ErrorCode.NoError, $"OpenGL error: {error}");
        } finally {
            TextFilterRender.Dispose();
            SpriteRender.Dispose();
            UiRender.GpuDraw.Dispose();
            UiRender.UiShader.Dispose();
            GL.DeleteFramebuffer(_framebuffer);
            GL.DeleteTexture(_target);
        }
    }

    private static void OffsetAndZeroQuality() {
        var right = Draw(new DropShadowFilter(6, 0, blurX: 0, blurY: 0, quality: 0));
        Check(A(right, 21, 24) > 250 && R(right, 21, 24) > 250, "visible white text body");
        Check(A(right, 31, 24) > 250 && R(right, 31, 24) < 5, "zero-quality shadow moves right");
        Check(A(right, 18, 24) == 0, "no shadow to left");
        var down = Draw(new DropShadowFilter(6, 90, blurX: 0, blurY: 0));
        Check(A(down, 24, 31) > 250 && A(down, 31, 24) == 0, "positive angle moves down");
        Console.WriteLine("  GPU PASS offset direction and zero-quality copy");
    }

    private static void BlurStrengthAndQuality() {
        var light = Draw(new DropShadowFilter(0, 0, blurX: 4, blurY: 4));
        var dark = Draw(new DropShadowFilter(0, 0, blurX: 4, blurY: 4, strength: 4));
        Check(A(light, 19, 24) > 0 && A(light, 19, 24) < 250, "blur has soft edge outside source geometry");
        Check(A(dark, 19, 24) > A(light, 19, 24), "strength increases outline opacity");
        var repeated = Draw(new DropShadowFilter(0, 0, blurX: 4, blurY: 0, quality: 3));
        Check(A(repeated, 16, 24) > 0 && A(light, 16, 24) == 0, "quality repeats blur and expands support");
        var horizontal = Draw(new DropShadowFilter(0, 0, blurX: 6, blurY: 0));
        Check(A(horizontal, 18, 24) > 0 && A(horizontal, 24, 18) == 0, "anisotropic blur obeys axes");
        Console.WriteLine("  GPU PASS blur support, strength, quality and anisotropy");
    }

    private static void InnerKnockoutAndHide() {
        var knockout = Draw(new DropShadowFilter(0, 0, blurX: 4, blurY: 4, knockout: true));
        Check(A(knockout, 24, 24) == 0 && A(knockout, 19, 24) > 0, "knockout removes source footprint");
        var hidden = Draw(new DropShadowFilter(12, 0, blurX: 0, blurY: 0, hideObject: true));
        Check(A(hidden, 24, 24) == 0 && A(hidden, 36, 24) > 250, "hideObject retains offset shadow only");
        var inner = Draw(new DropShadowFilter(3, 0, blurX: 0, blurY: 0, inner: true));
        Check(A(inner, 19, 24) == 0 && R(inner, 21, 24) < 5 && R(inner, 26, 24) > 250,
            "inner shadow stays inside source and shades opposing edge");
        var innerOnly = Draw(new DropShadowFilter(3, 0, blurX: 0, blurY: 0, inner: true, knockout: true));
        Check(A(innerOnly, 21, 24) > 250 && A(innerOnly, 26, 24) == 0, "inner knockout retains only inner edge");
        Console.WriteLine("  GPU PASS inner, knockout and hideObject composition");
    }

    private static void AlphaAndTintReuse() {
        var owner = new object();
        var filter = new DropShadowFilter(6, 0, color: 0xff0000, blurX: 0, blurY: 0);
        var normal = Draw(filter, owner);
        Check(R(normal, 31, 24) > 250 && normal[At(31, 24) + 2] == 0, "Flash RGB shadow packing");
        var changed = _instance;
        changed.Info.Y = 0.5f;
        changed.Color = 0x8000ff00; // half-alpha green, same geometry and filter cache entry.
        var faded = Draw(filter, owner, instance: changed);
        Check(A(faded, 24, 24) is >= 63 and <= 66 && A(faded, 31, 24) is >= 63 and <= 66,
            "text color alpha and parent alpha affect cached body and shadow");
        Check(faded[At(24, 24) + 1] > R(faded, 24, 24), "tint changes without stale cached colors");
        Console.WriteLine("  GPU PASS shadow RGB and cached tint/alpha changes");
    }

    private static void ClippingAndState() {
        var clipped = _instance;
        clipped.Scissor = new Vector4(0, 0, 29, Size);
        var pixels = Draw(new DropShadowFilter(6, 0, blurX: 0, blurY: 0), instance: clipped);
        Check(A(pixels, 28, 24) > 250 && A(pixels, 30, 24) == 0, "final filter observes parent clip");
        GL.GetInteger(GetPName.DrawFramebufferBinding, out int fbo);
        Span<int> viewport = stackalloc int[4];
        GL.GetInteger(GetPName.Viewport, viewport);
        Check(fbo == _framebuffer && viewport.SequenceEqual(new int[] { 0, 0, Size, Size }), "framebuffer/viewport restored");
        Check(GL.IsEnabled(EnableCap.Blend) && GL.IsEnabled(EnableCap.ScissorTest), "blend/scissor enable restored");
        GL.GetInteger(GetPName.BlendEquationRgb, out int equation);
        Check(equation == (int)BlendEquationMode.FuncAdd, "blend equation restored");
        Console.WriteLine("  GPU PASS clipping and OpenGL state restoration");
    }

    private static void GeometryInvalidation() {
        var owner = new object();
        var filter = new DropShadowFilter(0, 0, alpha: 0);
        var before = Draw(filter, owner);
        var shifted = _instance;
        shifted.TransformX.Z = 10;
        var moved = Draw(filter, owner, instance: shifted);
        Check(A(before, 24, 24) > 250 && A(moved, 24, 24) == 0 && A(moved, 34, 24) > 250,
            "cached mask follows world transform");
        var after = Draw(filter, owner, revision: 2, geometry: Quad(36, 20));
        Check(A(after, 24, 24) == 0 && A(after, 40, 24) > 250, "geometry revision rerasterizes mask");
        Console.WriteLine("  GPU PASS cached transforms and geometry invalidation");
    }

    private static void LongGeometry() {
        const int count = 1100;
        var vertices = new VertexUi[count * 4];
        var indices = new ushort[count * 6];
        var quad = Quad(20, 20);
        for (var i = 0; i < count; i++) {
            quad.CopyTo(vertices, i * 4);
            for (var j = 0; j < 6; j++) indices[i * 6 + j] = (ushort)(Indices[j] + i * 4);
        }
        Clear();
        SpriteRender.StartDraw();
        TextFilterRender.Draw(new object(), 1, new DropShadowFilter(0, 0, alpha: 0), _instance, indices, vertices);
        SpriteRender.EndDraw();
        Check(A(Read(), 24, 24) > 250, "text larger than one batch renders safely");
        Console.WriteLine("  GPU PASS long text batch splitting");
    }

    private static void CacheEviction() {
        var filter = new DropShadowFilter(0, 0, alpha: 0);
        for (var i = 0; i < 130; i++) Draw(filter);
        Check(A(Draw(filter), 24, 24) > 250, "LRU eviction keeps renderer usable");
        Console.WriteLine("  GPU PASS bounded cache eviction");
    }

    private static byte[] Draw(DropShadowFilter filter, object? owner = null, int revision = 1,
        SpriteInstanceData? instance = null, VertexUi[]? geometry = null) {
        Clear();
        SpriteRender.StartDraw();
        TextFilterRender.Draw(owner ?? new object(), revision, filter, instance ?? _instance, Indices, geometry ?? Quad(20, 20));
        SpriteRender.EndDraw();
        return Read();
    }

    private static void Clear() {
        ReadOnlySpan<float> zero = [0, 0, 0, 0];
        GL.ClearNamedFramebufferf(_framebuffer, OpenTK.Graphics.OpenGL.Buffer.Color, 0, zero);
    }
    private static byte[] Read() {
        var pixels = new byte[Size * Size * 4];
        GL.ReadPixels(0, 0, Size, Size, PixelFormat.Rgba, PixelType.UnsignedByte, pixels.AsSpan());
        return pixels;
    }
    private static VertexUi[] Quad(float x, float y) => [
        new(new Vector2(x, y), Vector2.Zero), new(new Vector2(x + 8, y), new Vector2(1, 0)),
        new(new Vector2(x + 8, y + 8), Vector2.One), new(new Vector2(x, y + 8), new Vector2(0, 1))
    ];
    private static int At(int x, int y) => ((Size - 1 - y) * Size + x) * 4;
    private static byte A(byte[] p, int x, int y) => p[At(x, y) + 3];
    private static byte R(byte[] p, int x, int y) => p[At(x, y)];
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
