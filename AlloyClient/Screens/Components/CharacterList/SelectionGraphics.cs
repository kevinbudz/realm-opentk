using System;
using System.Linq;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;
using Alloy.UiLib.Rendering;
using OpenTK.Mathematics;

namespace AlloyClient.Screens.Components.CharacterList;

public static class SelectionGraphics {
    public static readonly DropShadowFilter TextShadow = new(distance: 0, angle: 0, blurX: 8, blurY: 8);

    public static SimpleText Text(string text, int size, int x, int y, uint color = 0xB3B3B3, bool bold = false) {
        return new SimpleText(new TextConfig {
            Text = text, FontSize = size, X = x, Y = y, Color = color,
            FontType = bold ? FontType.Bold : FontType.Normal, DropShadow = TextShadow
        });
    }
}

public sealed class SelectionShape : Sprite {
    public SelectionShape(Vector2[] points, uint color) {
        TextureId = TextureType.Color;
        SetColor(color);
        VertexData = points.Select(point => new VertexUi(point)).ToArray();
        Indices = new ushort[(points.Length - 2) * 3];
        for (var i = 0; i < points.Length - 2; i++) {
            Indices[i * 3] = 0;
            Indices[i * 3 + 1] = (ushort)(i + 1);
            Indices[i * 3 + 2] = (ushort)(i + 2);
        }
        SetGraphicsBuffer();
    }

    public static SelectionShape Circle(int radius, uint color) {
        return new SelectionShape(Enumerable.Range(0, 64).Select(i => {
            var angle = i * MathF.PI / 32;
            return new Vector2(radius + MathF.Cos(angle) * radius, radius + MathF.Sin(angle) * radius);
        }).ToArray(), color);
    }
}
