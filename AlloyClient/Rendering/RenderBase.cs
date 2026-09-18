using System;
using System.Collections.Generic;
using AlloyClient.Assets;
using AlloyClient.Game.Objects;
using AlloyClient.Rendering.VertexData;
using Alloy.Common;
using Alloy.Common.Structs;
using OpenTK.Mathematics;


namespace AlloyClient.Rendering;

public abstract class RenderBase : IComparable<RenderBase> {
    public abstract ModelType ModelType { get; }
    
    public abstract bool HasShadow { get; }

    public bool Visible;

    public float RotationAngle;

    public float Size;

    protected internal Entity Entity;
    
    public Vector3 Position = Vector3.Zero;
    public Vector4 UV = Vector4.Zero;
    public Vector4 Scale = Vector4.Zero;
    public Vector4 Rotation = Vector4.Zero;
    public ExtraData Extra;
    public Color Color = Color.Transparent;
    
    public abstract void SetPosition(float x, float y, float z = 0);

    public void SetTexture(AtlasData texture) => SetTexture(texture, false);

    public virtual void SetTexture(AtlasData texture, bool attackFrame) {
        UV = texture.ToVector4();

        // Flash parity: TextureRedrawer.resize scales sprites 5x and the
        // non-perspective camera maps one tile to 50 screen px, so every atlas
        // texel (padding included) covers 0.1 tiles = 5 screen px at zoom 1.
        // Uniform density keeps Nearest sampling integer (no stretched texel
        // columns) and preserves the source aspect for non-square sprites.
        var rawW = texture.RawW();
        var rawH = texture.RawH();
        var w = rawW - AtlasConfig.Padding * 2;

        var widthScale = 0.1f * rawW;
        var heightScale = 0.1f * rawH;

        var padX = attackFrame ? widthScale * (0.5f - (AtlasConfig.Padding + w / 4) / rawW) : 0f;
        var padY = heightScale * (0.5f - AtlasConfig.Padding / rawH);

        Scale = new Vector4(widthScale, heightScale, padX, -padY);
    }

    protected float GetVisibleTopOffset(float sizeScale) {
        var rawHeight = Entity.Texture.RawH();
        if (rawHeight <= 0) {
            return (Scale.W - Scale.Y * 0.5f) * sizeScale;
        }

        var metadata = Main.Atlas.GetSpriteMetadata(Entity.Texture);
        var topmostY = Math.Clamp(metadata.TopmostY, 0, rawHeight);
        var transparentOffset = Scale.Y * topmostY / rawHeight;
        return (Scale.W - Scale.Y * 0.5f + transparentOffset) * sizeScale;
    }

    public abstract void SetVisibility(bool visible);

    public abstract void SetDepth(float depth);
    public abstract void SetName(string name);
    public abstract void SetAlpha(float alpha);
    
    public void SetRotation(float rotation) => RotationAngle = rotation;

    public void SetSize(float size) => Size = size;

    public abstract void Draw(List<VertexObject> targets, double time);

    public float GetCullRadius() {
        var sizeScale = Entity is null ? 1f : MathF.Max(Entity.Size / 100f, 0f);
        var scale = MathF.Max(MathF.Abs(Scale.X), MathF.Abs(Scale.Y));
        return MathF.Max(1f, scale * sizeScale * 0.5f);
    }
    
    public virtual void DrawShadow() { }

    public int CompareTo(RenderBase other) {
        if (Extra.SortId < other.Extra.SortId) {
            return 1;
        }

        if (Extra.SortId > other.Extra.SortId) {
            return -1;
        }
        
        return 0;
    }
}

public abstract class SubRenderBase {
    public abstract float Height { get; }

    protected RenderBase Parent;
    
    protected Entity Entity;
    
    public Vector3 Position = Vector3.Zero;
    public Vector4 UV = Vector4.Zero;
    public Vector4 Scale = Vector4.Zero;
    public Vector4 Rotation = Vector4.Zero;
    public ExtraData Extra;
    public Color Color = Color.Transparent;

    public void SetDepth(float depth) => Extra.SortId = depth;

    public void SetAlpha(float alpha) => Extra.Alpha = alpha;
    
    public abstract void Draw(float yOffset, List<VertexObject> targets, double time);
}

public struct ExtraData {

    public Vector4 Data => _internal;
    
    public float SortId {
        get => _internal.Y;
        set => _internal.Y = value;
    }

    public float Alpha {
        get => _internal.W;
        set => _internal.W = value;
    }

    private Vector4 _internal;

    public ExtraData(float type, float shade) {
        _internal = new Vector4(type, 0f, shade, 1f);
    }
    
    public ExtraData(float type, float sort, float shade, float alpha) {
        _internal = new Vector4(type, sort, shade, alpha);
    }

    public static ExtraData NewShadedObject(float sortId, float alpha) => new ExtraData(RenderConfig.TypeGameObject, sortId, RenderConfig.Shade, alpha);
}
