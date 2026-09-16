using System;
using Alloy.UiLib.Core;
using Alloy.UiLib.Rendering;
using OpenTK.Mathematics;

namespace Alloy.UiLib.BuiltIn;

public struct RoundedRectConfig {
    public int X = 0;
    public int Y = 0;
    public int Width = 0;
    public int Height = 0;
    public int Radius = 0;
    public CutEdges Corners = CutEdges.All;
    public uint Color = 0x000000;
    public float Alpha = 1.0f;
    public UiAnchor Anchor = UiAnchor.LeftTop;

    public bool MouseEnabled = false;

    public RoundedRectConfig() {
    }
}

public sealed class RoundedRect : Sprite {
    private const int SegmentsPerCorner = 5;

    public RoundedRect(RoundedRectConfig config) {
        X = config.X;
        Y = config.Y;
        SetColor(config.Color);
        Alpha = config.Alpha;
        SetAnchor(config.Anchor);
        MouseEnabled = config.MouseEnabled;

        TextureId = TextureType.Color;

        SetHitboxType(CollisionType.Vertices);

        Build(config.Width, config.Height, config.Radius, config.Corners);
    }

    private void Build(int w, int h, int radius, CutEdges corners) {
        var r = Math.Min(radius, Math.Min(w / 2, h / 2));
        var pts = new System.Collections.Generic.List<Vector2>();

        bool tl = (corners & CutEdges.TopLeft) != 0;
        bool tr = (corners & CutEdges.TopRight) != 0;
        bool br = (corners & CutEdges.BottomRight) != 0;
        bool bl = (corners & CutEdges.BottomLeft) != 0;

        if (r <= 0 || (!tl && !tr && !br && !bl)) {
            VertexData = [
                new VertexUi(new Vector2(0, 0)),
                new VertexUi(new Vector2(w, 0)),
                new VertexUi(new Vector2(w, h)),
                new VertexUi(new Vector2(0, h)),
            ];
            Indices = [0, 1, 2, 0, 2, 3];
            SetGraphicsBuffer();
            return;
        }

        // Clockwise starting just after top-left corner along the top edge.
        pts.Add(new Vector2(tl ? r : 0, 0));
        pts.Add(new Vector2(tr ? w - r : w, 0));

        if (tr) AddArc(pts, w - r, r, r, -MathF.PI / 2, 0);
        else pts.Add(new Vector2(w, 0));

        pts.Add(new Vector2(w, br ? h - r : h));

        if (br) AddArc(pts, w - r, h - r, r, 0, MathF.PI / 2);
        else pts.Add(new Vector2(w, h));

        pts.Add(new Vector2(bl ? r : 0, h));

        if (bl) AddArc(pts, r, h - r, r, MathF.PI / 2, MathF.PI);
        else pts.Add(new Vector2(0, h));

        pts.Add(new Vector2(0, tl ? r : 0));

        if (tl) AddArc(pts, r, r, r, MathF.PI, MathF.PI * 1.5f);
        else pts.Add(new Vector2(0, 0));

        var n = pts.Count;
        VertexData = new VertexUi[n + 1];
        VertexData[0] = new VertexUi(new Vector2(w / 2f, h / 2f));
        for (var i = 0; i < n; i++) {
            VertexData[i + 1] = new VertexUi(pts[i]);
        }

        Indices = new ushort[n * 3];
        for (var i = 0; i < n; i++) {
            Indices[i * 3] = 0;
            Indices[i * 3 + 1] = (ushort)(i + 1);
            Indices[i * 3 + 2] = (ushort)((i + 1) % n + 1);
        }

        SetGraphicsBuffer();
    }

    private static void AddArc(System.Collections.Generic.List<Vector2> pts, float cx, float cy, float r, float a0, float a1) {
        for (var i = 1; i <= SegmentsPerCorner; i++) {
            var a = a0 + (a1 - a0) * i / SegmentsPerCorner;
            pts.Add(new Vector2(cx + r * MathF.Cos(a), cy + r * MathF.Sin(a)));
        }
    }
}
