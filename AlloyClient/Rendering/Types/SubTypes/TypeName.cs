using System.Collections.Generic;
using Alloy.Common;
using AlloyClient.Game.Objects;
using AlloyClient.Rendering.VertexData;
using Alloy.UiLib;
using Alloy.UiLib.Core;
using Alloy.UiLib.Data;
using OpenTK.Mathematics;

namespace AlloyClient.Rendering.Types.SubTypes;

public class TypeName : SubRenderBase {

    private float _height;
    public override float Height {
        get => _height * 1.75f;
    }

    // Flash parity (GameObject.generateNameText): SimpleText(16, white) with
    // bold set, no letter-spacing. Both clients map one tile to 50px
    // (Flash Camera appendScale(50); Alloy BaseCameraZoom at zoom 1), so a
    // 16px Flash em is 16/50 tiles in the world.
    internal const float WorldTextSize = 16f / 50f;
    internal static readonly FontType NameFont = FontType.Bold;

    // Flash parity: GameObject floating names render white; Player overrides
    // recolor to NAME_COLOUR gold (0xFCDF00). Kept out of SetTextures so a
    // live rename (e.g. the portal "Realm (count)" Name stat) never resets a
    // portal back to gold.
    internal static Color DefaultColorFor(Entity entity) =>
        entity is Player ? new Color(0xFC, 0xDF, 0, 1) : Color.White;

    public string Name;

    private GlyphData[] _glyphs;

    public TypeName(RenderBase parent, Entity entity) {
        Parent = parent;
        Entity = entity;
        if (entity is Player player) {
            Name = player.Name;
        } else {
            Name = entity.Properties.DisplayName;
        }

        if (string.IsNullOrEmpty(Name))
            Name = "Default";

        Color = DefaultColorFor(entity);
        SetTextures();
        Extra = new ExtraData(RenderConfig.TypeText, RenderConfig.NoShade);
    }

    public void SetTextures() {
        SetTextures(UiRender.GetFont(NameFont));
    }

    internal void SetTextures(BitmapFont font) {
        var size = WorldTextSize;
        _glyphs = Layout(Name, font, size, out _height, out _);

        Rotation = new Vector4(0, 1, 1, -1);
    }

    // Mirrors UiLib SimpleText.FillData (advance + single kerning, no extra
    // tracking) but emits world-space billboard quads centered on the string.
    internal static GlyphData[] Layout(string name, BitmapFont font, float size, out float height, out float totalWidth) {
        height = font.Ascender * size;
        var zero = new Vector2(0f, height);

        var len = name.Length;
        var glyphs = new GlyphData[len];
        for (var i = 0; i < len; i++) {
            var c = name[i];
            if (!font.Glyphs.TryGetValue(c, out var glyph)) {
                continue;
            }

            var uv = glyph.UV;
            var pos = glyph.Position;
            var w = (pos.X1 - pos.X0) * size;
            var h = (pos.Y0 - pos.Y1) * size;
            var cx = zero.X + (pos.X0 + pos.X1) * 0.5f * size;
            var cy = zero.Y - (pos.Y0 + pos.Y1) * 0.5f * size;

            glyphs[i] = new GlyphData(uv.ToVector4(), w, h, cx, cy);

            if (i < len - 1) {
                font.Kernings.TryGetValue((c, name[i + 1]), out var kern);
                zero.X += kern * size;
            }

            zero.X += glyph.Advance * size;
        }

        totalWidth = zero.X;
        for (var index = 0; index < glyphs.Length; index++) {
            glyphs[index].Center(totalWidth / 2f);
        }

        return glyphs;
    }

    public override void Draw(float yOffset, List<VertexObject> targets, double time) {
        for (var index = 0; index < _glyphs.Length; index++) {
            var g = _glyphs[index];
            g.Scale.W += yOffset;
            targets.Add(new VertexObject(Parent.Position, g.UV, g.Scale, Rotation, Extra, Color));
        }
    }

    internal struct GlyphData(Vector4 uv, float w, float h, float cx, float cy) {
        public Vector4 UV = uv;
        public Vector4 Scale = new(w, h, cx, cy);

        public void Center(float x) {
            Scale.Z -= x;
        }
    }
}
