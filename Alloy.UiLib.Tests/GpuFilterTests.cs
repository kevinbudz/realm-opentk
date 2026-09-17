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
    private static Sampler? _glyphSampler;

    internal static void Run() {
        var source = (ShaderSource)typeof(UiRender).GetProperty("UiShaderSource", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        UiRender.UiShader = Shader.FromSource(source);
        UiRender.GpuDraw = new GpuTimer();
        SpriteRender.Init();
        using var glyph = new Texture(new Color[] { Color.White }, 1, 1);
        using var sampler = new Sampler(glyph, TextureFilter.Linear, 6);
        _glyphSampler = sampler;
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
            ScaledMaskStaysSharp();
            FilteredBodyMatchesDirectText();
            SmallTextKeepsAntialiasedFringe();
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

    private static void ScaledMaskStaysSharp() {
        var owner = new object();
        var filter = new DropShadowFilter(0, 0, alpha: 0, blurX: 0, blurY: 0, quality: 0);
        Draw(filter, owner);
        var scaled = _instance;
        scaled.TransformX = new Vector4(2, 0, 0, 0);
        scaled.TransformY = new Vector4(0, 2, 0, 0);
        var pixels = Draw(filter, owner, instance: scaled);
        // Design body spans 20..28 with a 2px pad (origin 18), so at 2x the left
        // edge lands on screen x=40. A design-resolution mask stretches that step
        // over ~2 screen pixels; a scale-aware mask keeps it within one.
        var outside = A(pixels, 39, 48);
        var inside = A(pixels, 40, 48);
        Check(outside == 0 && inside > 250,
            $"scaled body edge stays sharp (outside={outside} inside={inside})");
        Console.WriteLine("  GPU PASS scaled mask resolution");
    }

    // The filter body must be pixel-identical to unfiltered MSDF text: a blurred
    // halo behind the glyphs is fine, but the glyph cores and AA fringe must not
    // be baked through the low-resolution coverage mask.
    private static void FilteredBodyMatchesDirectText() {
        const int W = 8, H = 8;
        var ramp = new Color[W * H];
        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++) {
                var v = x * 255 / (W - 1);
                ramp[y * W + x] = new Color(v, v, v);
            }
        using var texture = new Texture(ramp.AsSpan(), W, H);
        using var sampler = new Sampler(texture, TextureFilter.Linear, 6);
        UiRender.UiShader.SetValue("TextTexture", sampler);
        UiRender.UiShader.SetValue("TextTextureSize", new Vector2(W, H));
        try {
            // Shadow parked far away with zero alpha so only the body is measured.
            var filter = new DropShadowFilter(12, 0, alpha: 0, blurX: 0, blurY: 0, quality: 0);
            var shifted = _instance;
            shifted.TransformX = new Vector4(1, 0, 0.5f, 0);
            CheckBodyMatches(filter, _instance, "aligned");
            CheckBodyMatches(filter, shifted, "half-pixel offset");
        } finally {
            // SetValue only writes the uniform; re-bind or unit 6 keeps the
            // deleted gradient texture for every later text draw.
            _glyphSampler!.Bind(6);
            UiRender.UiShader.SetValue("TextTexture", _glyphSampler);
            UiRender.UiShader.SetValue("TextTextureSize", Vector2.One);
        }
        Console.WriteLine("  GPU PASS filtered body matches direct text");
    }

    private static void CheckBodyMatches(DropShadowFilter filter, SpriteInstanceData instance, string name) {
        var direct = DirectDraw(instance);
        var filtered = Draw(filter, new object(), instance: instance);
        var worst = 0;
        var at = -1;
        for (var x = 18; x <= 30; x++) {
            var diff = Math.Abs((int)A(direct, x, 24) - A(filtered, x, 24));
            if (diff > worst) {
                worst = diff;
                at = x;
            }
        }
        Check(worst <= 2, $"filtered body matches direct text ({name}): max alpha diff {worst} at x={at}");
    }

    // Small (<16pt) labels use the anisotropic MSDF path while larger ones use
    // the plain path; both contractually antialias over one screen pixel. A
    // magnified SDF ramp must therefore render the same through either mode.
    private static void SmallTextKeepsAntialiasedFringe() {
        // SDF-correct ramp: distance spans [-0.5, 0.5] over PixelRange texels
        // (slope 1/4 per texel), like a real MSDF atlas with PixelRange 4.
        const int W = 8, H = 8, Range = 4;
        var ramp = new Color[W * H];
        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++) {
                var v = Math.Clamp((int)((x - 3.5) / Range * 255 + 127.5), 0, 255);
                ramp[y * W + x] = new Color(v, v, v);
            }
        using var texture = new Texture(ramp.AsSpan(), W, H);
        using var sampler = new Sampler(texture, TextureFilter.Linear, 6);
        UiRender.UiShader.SetValue("TextTexture", sampler);
        UiRender.UiShader.SetValue("TextTextureSize", new Vector2(W, H));
        try {
            // 2x scale, rotated 30 degrees about the quad center, kept on target.
            var small = _instance;
            small.Extra1 = new Vector4(0, 1, 0, 0);
            small.TransformX = new Vector4(1.7320508f, -1, 14.4f, 0);
            small.TransformY = new Vector4(1, 1.7320508f, -33.6f, 0);
            var pixels = DirectDraw(small);
            var normal = small;
            normal.Extra1 = new Vector4(0, 0, 0, 0);
            var refPixels = DirectDraw(normal);
            var worst = 0;
            var atX = -1;
            var atY = -1;
            for (var y = 20; y <= 44; y++)
                for (var x = 12; x <= 52; x++) {
                    var diff = Math.Abs((int)A(pixels, x, y) - A(refPixels, x, y));
                    if (diff > worst) {
                        worst = diff;
                        atX = x;
                        atY = y;
                    }
                }
            Check(worst <= 65, $"small text matches normal AA when magnified (max diff {worst} at ({atX},{atY}))");
        } finally {
            _glyphSampler!.Bind(6);
            UiRender.UiShader.SetValue("TextTexture", _glyphSampler);
            UiRender.UiShader.SetValue("TextTextureSize", Vector2.One);
        }
        Console.WriteLine("  GPU PASS small-text AA fringe under magnification");
    }

    private static byte[] DirectDraw(SpriteInstanceData instance, VertexUi[]? geometry = null) {
        Clear();
        SpriteRender.StartDraw();
        SpriteRender.Draw(instance, Indices, geometry ?? Quad(20, 20));
        SpriteRender.EndDraw();
        return Read();
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
