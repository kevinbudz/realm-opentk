using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Alloy.UiLib.Extra;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Rendering;

/// <summary>
/// Rasterizes the complete text run before filtering. Masks are supersampled by the
/// quantized world scale so a magnified halo keeps ~1 texel per screen pixel;
/// movement, tint and alpha alone do not cause another rasterization. Outer
/// filters draw the halo from the mask and the body through the normal MSDF
/// path, so glyph edges match unfiltered text exactly.
/// Texture units 14 and 15 are reserved while this renderer is drawing.
/// </summary>
internal static class TextFilterRender {
    private const int MaxEntries = 128;
    private const long MaxCacheBytes = 32 * 1024 * 1024;
    // Supersample bucket: quarters are exactly representable, capped to bound memory.
    private const float MinMaskScale = 1f;
    private const float MaxMaskScale = 4f;
    private const float MaskScaleStep = 0.25f;
    private static readonly ConditionalWeakTable<object, Entry> Cache = new();
    private static readonly List<Entry> Entries = new();
    private static long _clock;
    private static long _cacheBytes;
    private static readonly ushort[] QuadIndices = [0, 1, 2, 0, 2, 3];

    private sealed class Entry : IDisposable {
        public int Revision, IndexCount, Width, Height, TexWidth, TexHeight;
        public float TextMode;
        public float ScaleX = MinMaskScale, ScaleY = MinMaskScale;
        public DropShadowFilter Filter;
        public WeakReference<object> Owner;
        public int Body, Shadow;
        public long Used;
        public Vector2 Origin;
        public long Bytes => (long)TexWidth * TexHeight * 4; // Two R16F textures.
        public void Dispose() {
            if (Body != 0) GL.DeleteTexture(Body);
            if (Shadow != 0) GL.DeleteTexture(Shadow);
            Body = Shadow = 0;
        }
    }

    internal static void Draw(object owner, int revision, DropShadowFilter filter,
        SpriteInstanceData instance, ReadOnlySpan<ushort> indices, ReadOnlySpan<VertexUi> vertices) {
        if (indices.IsEmpty || vertices.IsEmpty) return;
        // Bindings cannot change underneath a queued UI batch.
        SpriteRender.Flush();
        for (var i = Entries.Count - 1; i >= 0; i--) {
            if (!Entries[i].Owner.TryGetTarget(out _)) Remove(null, Entries[i]);
        }
        var maskScale = QuantizedMaskScale(instance);
        if (Cache.TryGetValue(owner, out var entry) &&
            (entry.Revision != revision || entry.IndexCount != indices.Length ||
             entry.Filter != filter || entry.TextMode != instance.Extra1.Y ||
             entry.ScaleX != maskScale.X || entry.ScaleY != maskScale.Y)) {
            Remove(owner, entry);
            entry = null;
        }
        if (entry == null) {
            entry = Build(revision, filter, instance, indices, vertices, maskScale);
            while (Entries.Count >= MaxEntries || _cacheBytes + entry.Bytes > MaxCacheBytes) EvictOldest();
            entry.Owner = new WeakReference<object>(owner);
            Cache.Add(owner, entry);
            Entries.Add(entry);
            _cacheBytes += entry.Bytes;
        }
        entry.Used = ++_clock;
        GL.BindTextureUnit(14, entry.Body);
        GL.BindTextureUnit(15, entry.Shadow);
        GL.BindSampler(14, 0);
        GL.BindSampler(15, 0);
        var angle = filter.Angle * MathF.PI / 180f;
        // DropShadowFilter.Color is Flash RGB, while the UI packs ABGR.
        var shadowPacked = 0xff000000u | (filter.Color & 0xff) << 16 |
            (filter.Color & 0xff00) | (filter.Color >> 16 & 0xff);
        var offset = new Vector4(MathF.Cos(angle) * filter.Distance / entry.Width,
            MathF.Sin(angle) * filter.Distance / entry.Height, filter.Alpha, filter.Strength);
        if (!filter.Inner && !filter.Knockout && !filter.HideObject) {
            // Outer filters (every client use) draw the blurred halo from the mask,
            // then the glyph run itself through the normal MSDF path. Shadow-behind
            // composited via source-over equals the old premultiplied composite,
            // but the body keeps per-screen-pixel anti-aliasing instead of a
            // resampled mask: fractional and magnified placements stay sharp.
            var shadow = instance;
            shadow.Info.X = 10;
            shadow.ColorOverride = shadowPacked;
            shadow.Extra1 = offset;
            shadow.Extra2 = new Vector4(0, 0, 1, 0); // Halo only; the body draws next.
            Span<VertexUi> halo = stackalloc VertexUi[4];
            MakeQuad(halo, entry.Origin, entry.Width, entry.Height);
            SpriteRender.Draw(shadow, QuadIndices, halo);
            SpriteRender.Flush();
            var body = instance;
            // The MSDF body path fades via Info.Y but leaves instance-color alpha
            // to the caller, while the mask path baked it into coverage. Fold it
            // here so tinting and fading stay identical to the old composite.
            body.Info = new Vector2(body.Info.X, body.Info.Y * ((body.Color >> 24 & 0xff) / 255f));
            body.Extra1 = new Vector4(0, body.Extra1.Y, body.Extra1.Z, body.Extra1.W);
            SpriteRender.Draw(body, indices, vertices);
            SpriteRender.Flush();
            return;
        }
        instance.Info.X = 10;
        instance.ColorOverride = shadowPacked;
        instance.Extra1 = offset;
        instance.Extra2 = new Vector4(filter.Inner ? 1 : 0, filter.Knockout ? 1 : 0, filter.HideObject ? 1 : 0, 0);
        Span<VertexUi> quad = stackalloc VertexUi[4];
        MakeQuad(quad, entry.Origin, entry.Width, entry.Height);
        SpriteRender.Draw(instance, QuadIndices, quad);
        SpriteRender.Flush();
    }

    internal static Vector2 QuantizedMaskScale(SpriteInstanceData instance) {
        // Local X (1,0) maps to world (M11,M21); local Y (0,1) maps to (M12,M22).
        var sx = MathF.Sqrt(instance.TransformX.X * instance.TransformX.X +
            instance.TransformY.X * instance.TransformY.X);
        var sy = MathF.Sqrt(instance.TransformX.Y * instance.TransformX.Y +
            instance.TransformY.Y * instance.TransformY.Y);
        return new Vector2(QuantizeScale(sx), QuantizeScale(sy));
    }

    private static float QuantizeScale(float scale) {
        if (!float.IsFinite(scale) || scale <= MinMaskScale) return MinMaskScale;
        if (scale >= MaxMaskScale) return MaxMaskScale;
        // Round up so the mask never undersamples the screen.
        var quantized = MathF.Ceiling(scale / MaskScaleStep) * MaskScaleStep;
        return Math.Clamp(quantized, MinMaskScale, MaxMaskScale);
    }

    private static Entry Build(int revision, DropShadowFilter filter, SpriteInstanceData source,
        ReadOnlySpan<ushort> indices, ReadOnlySpan<VertexUi> vertices, Vector2 maskScale) {
        var minimum = new Vector2(float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity);
        foreach (var index in indices) {
            minimum = Vector2.ComponentMin(minimum, vertices[index].Position);
            maximum = Vector2.ComponentMax(maximum, vertices[index].Position);
        }
        var radians = filter.Angle * MathF.PI / 180f;
        var dx = MathF.Cos(radians) * filter.Distance;
        var dy = MathF.Sin(radians) * filter.Distance;
        var rx = filter.BlurX * 0.5f;
        var ry = filter.BlurY * 0.5f;
        var padX = MathF.Ceiling(rx) * filter.Quality + MathF.Abs(dx) + 2;
        var padY = MathF.Ceiling(ry) * filter.Quality + MathF.Abs(dy) + 2;
        var origin = new Vector2(MathF.Floor(minimum.X - padX), MathF.Floor(minimum.Y - padY));
        var widthF = MathF.Ceiling(maximum.X + padX - origin.X);
        var heightF = MathF.Ceiling(maximum.Y + padY - origin.Y);
        var texWidthF = MathF.Ceiling(widthF * maskScale.X);
        var texHeightF = MathF.Ceiling(heightF * maskScale.Y);
        GL.GetInteger(GetPName.MaxTextureSize, out int maxTexture);
        if (!float.IsFinite(widthF) || !float.IsFinite(heightF) || widthF < 1 || heightF < 1 ||
            texWidthF > maxTexture || texHeightF > maxTexture || texWidthF * texHeightF * 4 > MaxCacheBytes)
            throw new InvalidOperationException("Filtered text exceeds the GPU texture size or 32 MiB mask budget. Split the text into smaller elements or reduce its blur/distance.");
        var entry = new Entry {
            Revision = revision, IndexCount = indices.Length, Filter = filter, TextMode = source.Extra1.Y,
            Origin = origin, Width = (int)widthF, Height = (int)heightF,
            TexWidth = (int)texWidthF, TexHeight = (int)texHeightF,
            ScaleX = maskScale.X, ScaleY = maskScale.Y
        };
        GL.GetInteger(GetPName.DrawFramebufferBinding, out int framebuffer);
        Span<int> viewport = stackalloc int[4];
        GL.GetInteger(GetPName.Viewport, viewport);
        GL.GetInteger(GetPName.BlendEquationRgb, out int equationRgb);
        GL.GetInteger(GetPName.BlendEquationAlpha, out int equationAlpha);
        var blend = GL.IsEnabled(EnableCap.Blend);
        var scissor = GL.IsEnabled(EnableCap.ScissorTest);
        var fbo = 0;
        var temporary = 0;
        try {
            entry.Body = CreateMask(entry.TexWidth, entry.TexHeight);
            entry.Shadow = CreateMask(entry.TexWidth, entry.TexHeight);
            temporary = CreateMask(entry.TexWidth, entry.TexHeight);
            fbo = GL.CreateFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo);
            // Viewport sets texel density; the view matrix below still spans the
            // design extent so blur radii and UV offsets stay in design pixels.
            GL.Viewport(0, 0, entry.TexWidth, entry.TexHeight);
            GL.Disable(EnableCap.ScissorTest);
            GL.Enable(EnableCap.Blend);
            // MAX merges glyph coverage without squaring alpha or darkening overlaps.
            GL.BlendEquation(BlendEquationMode.Max);
            var view = Matrix4.Identity;
            view.M11 = 2f / entry.Width;
            view.M22 = 2f / entry.Height;
            view.M41 = view.M42 = -1;
            UiRender.UiShader.SetValue("ViewMatrix", view);
            Attach(fbo, entry.Body);
            ReadOnlySpan<float> clear = [0, 0, 0, 0];
            GL.ClearNamedFramebufferf(fbo, OpenTK.Graphics.OpenGL.Buffer.Color, 0, clear);
            source.TransformX = new Vector4(1, 0, -origin.X, 0);
            source.TransformY = new Vector4(0, 1, -origin.Y, 0);
            source.Info = new Vector2(5, 1);
            source.Color = 0xffffffff;
            source.ColorTransform = Vector4.One;
            source.Extra2 = new Vector4(0, 0, 0, 1); // Body-only rasterization, no clip.
            SpriteRender.Draw(source, indices, vertices);
            SpriteRender.Flush();
            GL.Disable(EnableCap.Blend);
            source.TransformX = new Vector4(1, 0, 0, 0);
            source.TransformY = new Vector4(0, 1, 0, 0);
            source.Info.X = 11;
            Span<VertexUi> quad = stackalloc VertexUi[4];
            MakeQuad(quad, Vector2.Zero, entry.Width, entry.Height);
            var input = entry.Body;
            for (var pass = 0; pass < Math.Max(1, filter.Quality); pass++) {
                Blur(fbo, input, temporary, source, quad, new Vector4(1f / entry.Width, 0, filter.Quality == 0 ? 0 : rx, 0));
                Blur(fbo, temporary, entry.Shadow, source, quad, new Vector4(0, 1f / entry.Height, filter.Quality == 0 ? 0 : ry, 0));
                input = entry.Shadow;
            }
            return entry;
        } catch {
            entry.Dispose();
            throw;
        } finally {
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, framebuffer);
            GL.Viewport(viewport[0], viewport[1], viewport[2], viewport[3]);
            GL.BlendEquationSeparate((BlendEquationMode)equationRgb, (BlendEquationMode)equationAlpha);
            if (blend) GL.Enable(EnableCap.Blend); else GL.Disable(EnableCap.Blend);
            if (scissor) GL.Enable(EnableCap.ScissorTest);
            UiRender.UiShader.SetValue("ViewMatrix", UiRender.ViewMatrix);
            UiRender.UiShader.Apply();
            if (temporary != 0) GL.DeleteTexture(temporary);
            if (fbo != 0) GL.DeleteFramebuffer(fbo);
        }
    }

    private static void Blur(int fbo, int input, int output, SpriteInstanceData data,
        ReadOnlySpan<VertexUi> quad, Vector4 parameters) {
        Attach(fbo, output);
        GL.BindTextureUnit(15, input);
        GL.BindSampler(15, 0);
        data.Extra1 = parameters;
        SpriteRender.Draw(data, QuadIndices, quad);
        SpriteRender.Flush();
    }

    private static int CreateMask(int width, int height) {
        var texture = GL.CreateTexture(TextureTarget.Texture2D);
        GL.TextureStorage2D(texture, 1, SizedInternalFormat.R16f, width, height);
        GL.TextureParameteri(texture, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TextureParameteri(texture, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TextureParameteri(texture, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TextureParameteri(texture, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        return texture;
    }

    private static void Attach(int fbo, int texture) {
        GL.NamedFramebufferTexture(fbo, FramebufferAttachment.ColorAttachment0, texture, 0);
        if (GL.CheckNamedFramebufferStatus(fbo, FramebufferTarget.DrawFramebuffer) != FramebufferStatus.FramebufferComplete)
            throw new InvalidOperationException("Could not create a framebuffer for filtered text.");
    }

    private static void MakeQuad(Span<VertexUi> quad, Vector2 origin, int width, int height) {
        quad[0] = new VertexUi(origin, Vector2.Zero);
        quad[1] = new VertexUi(origin + new Vector2(width, 0), new Vector2(1, 0));
        quad[2] = new VertexUi(origin + new Vector2(width, height), Vector2.One);
        quad[3] = new VertexUi(origin + new Vector2(0, height), new Vector2(0, 1));
    }

    private static void Remove(object key, Entry entry) {
        _cacheBytes -= entry.Bytes;
        if (key != null) Cache.Remove(key);
        Entries.Remove(entry);
        entry.Dispose();
    }

    private static void EvictOldest() {
        Entry candidate = null;
        foreach (var entry in Entries) {
            if (candidate == null || entry.Used < candidate.Used) candidate = entry;
        }
        if (candidate != null) {
            candidate.Owner.TryGetTarget(out var owner);
            Remove(owner, candidate);
        }
    }

    internal static void Dispose() {
        foreach (var entry in Entries) entry.Dispose();
        Cache.Clear();
        Entries.Clear();
        _cacheBytes = 0;
    }
}
