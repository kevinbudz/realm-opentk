using System;
using Alloy.UiLib.Core;
using Alloy.UiLib.Rendering;
using OpenTK.Mathematics;

namespace AlloyClient.Ui.Components.Graphics;

public sealed class ScreenGraphic : UiElement {
    // Flash LayoutHelper/ScaledScreen design space. The title textures are
    // retained at their native 1280x720 resolution, then centered and
    // height-scaled into the 800x600 client viewport.
    private const int DesignWidth = 800;
    private const int DesignHeight = 600;
    private const int TexWidth = 1280;
    private const int TexHeight = 720;

    public ScreenGraphic(bool splash = false) {

        TextureId = splash ? TextureType.TitleGraphic : TextureType.TitleBackground;
        
        ResizeBackBuffer();
        FillData(TexWidth, TexHeight);
    }

    protected override void OnResize(ResizeEvent args) {
        // LayoutHelper.scaleForHeight(stageHeight), with the same fallback
        // for a transient zero-sized stage. Width can require additional
        // cover scaling because this is a full-window backdrop, while menu
        // content continues to use the height-derived Stage.ScreenScale.
        var heightScale = args.Height > 0 ? (float)args.Height / DesignHeight : 1f;
        var widthScale = args.Width > 0 ? (float)args.Width / TexWidth : 0f;
        var scale = MathF.Max(heightScale, widthScale);

        var w = Math.Max(1, (int)MathF.Ceiling(TexWidth * scale));
        var h = Math.Max(1, (int)MathF.Ceiling(TexHeight * scale));

        X = (args.Width - w) / 2;
        Y = (args.Height - h) / 2;
        
        FillData(w, h);
    }
    
    private void ResizeBackBuffer() {
        VertexData = new VertexUi[4];
        Indices = [0, 1, 2, 0, 2, 3];
    }

   private void FillData(int width, int height) {
       VertexData[0] = new VertexUi(new Vector2(0, height), new Vector2(0f, 1f));
       VertexData[1] = new VertexUi(new Vector2(0, 0), new Vector2(0f, 0f));
       VertexData[2] = new VertexUi(new Vector2(width, 0), new Vector2(1f, 0f));
       VertexData[3] = new VertexUi(new Vector2(width, height), new Vector2(1f, 1f));
       
       SetGraphicsBuffer();
    }
}
