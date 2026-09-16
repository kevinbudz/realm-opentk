using Alloy.ContentReader;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Data;
using Alloy.UiLib.Extra;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

/// <summary>Optional real-font contact sheet: --preview CONTENT_DIRECTORY OUTPUT.ppm.</summary>
internal static class TextPreview {
    internal static void Render() {
        var args = Environment.GetCommandLineArgs();
        var option = Array.IndexOf(args, "--preview");
        if (option < 0) return;
        if (option + 2 >= args.Length) throw new ArgumentException("--preview requires a content directory and output PPM path.");

        const int width = 1040, height = 400;
        ContentLoader.Init(args[option + 1]);
        var font = new BitmapFamily(ContentLoader.LoadFont("Fonts/MyriadPro/MyriadPro.msdf"));
        font.Sampler.Bind(6);
        UiRender.RegisterFont(font);
        var target = GL.CreateTexture(TextureTarget.Texture2D);
        GL.TextureStorage2D(target, 1, SizedInternalFormat.Rgba8, width, height);
        var framebuffer = GL.CreateFramebuffer();
        GL.NamedFramebufferTexture(framebuffer, FramebufferAttachment.ColorAttachment0, target, 0);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        GL.Viewport(0, 0, width, height);
        GL.Disable(EnableCap.ScissorTest);
        UiRender.ViewMatrix = Matrix4.Identity;
        UiRender.ViewMatrix.M11 = 2f / width;
        UiRender.ViewMatrix.M22 = -2f / height;
        UiRender.ViewMatrix.M41 = -1;
        UiRender.ViewMatrix.M42 = 1;
        UiRender.UiShader.SetValue("ViewMatrix", UiRender.ViewMatrix);

        try {
            var stage = new Stage { MouseEnabled = false };
            stage.SetSize(new Vector2i(width, height), Vector2.One);
            stage.AddChild(new ColorRect(new ColorRectConfig { Width = width, Height = height, Color = 0x242832 }));
            stage.AddChild(new ColorRect(new ColorRectConfig { Y = 120, Width = width, Height = 100, Color = 0x4C606A }));
            stage.AddChild(new ColorRect(new ColorRectConfig { Y = 220, Width = width, Height = 100, Color = 0x888888 }));
            DropShadowFilter?[] filters = [null,
                new(distance: 2, angle: 45, alpha: 0.35f),
                new(distance: 0, angle: 0, alpha: 0.5f, blurX: 12, blurY: 12),
                new(distance: 0, angle: 0, strength: 2)];
            string[] names = ["No filter", "Light shadow", "Flash soft", "Flash outline"];
            for (var column = 0; column < filters.Length; column++) {
                var x = 20 + column * 260;
                stage.AddChild(new SimpleText(new TextConfig {
                    Text = names[column], X = x, Y = 16, FontSize = 18, FontType = FontType.Bold,
                    Color = 0x9BC8D7, OutlineColor = 0x9BC8D7
                }));
                for (var row = 0; row < 3; row++) {
                    stage.AddChild(new SimpleText(new TextConfig {
                        Text = row == 0 ? "HP 720/720 | MP 252" : "Realm 012345",
                        X = x, Y = 70 + row * 88, FontSize = row == 0 ? 14 : row == 1 ? 24 : 36,
                        FontType = FontType.Bold, Color = 0xFFFFFF, OutlineColor = 0xFFFFFF,
                        DropShadow = filters[column]
                    }));
                }
            }
            stage.AddChild(new SimpleText(new TextConfig {
                Text = "Myriad Pro Bold  /  14, 24, 36 design pixels  /  rendered by Alloy OpenTK",
                X = 20, Y = 350, FontSize = 16, Color = 0xAAB6C5, OutlineColor = 0xAAB6C5
            }));

            // Exercise the public text API with real glyphs, including a wrap
            // rewind across an unsupported character and a subsequent shrink.
            var changing = new SimpleText(new TextConfig {
                Text = "one two\uFFFFthree four", MaxWidth = 90, FontSize = 14
            });
            var measured = (changing.Width, changing.Height);
            changing.DropShadow = filters[3];
            if (measured != (changing.Width, changing.Height)) throw new Exception("Filter changed logical text bounds.");
            changing.SetText("HP");
            if (changing.OverridePrimCount != 4) throw new Exception("Shrinking text retained old glyphs.");
            changing.SetText("");
            if (changing.OverridePrimCount != 0 || changing.Width != 0 || changing.Height != 0)
                throw new Exception("Empty text retained geometry or bounds.");

            stage.InternalUpdateLoop();
            stage.InternalDrawLoop();
            var pixels = new byte[width * height * 4];
            GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels.AsSpan());
            using var output = File.Create(args[option + 2]);
            output.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));
            for (var y = height - 1; y >= 0; y--)
                for (var x = 0; x < width; x++) output.Write(pixels.AsSpan((y * width + x) * 4, 3));
            Console.WriteLine($"  GPU PASS real-font layout/shrinking; preview: {args[option + 2]}");
        } finally {
            GL.DeleteFramebuffer(framebuffer);
            GL.DeleteTexture(target);
            font.Sampler.Dispose();
            font.Atlas.Dispose();
        }
    }
}
