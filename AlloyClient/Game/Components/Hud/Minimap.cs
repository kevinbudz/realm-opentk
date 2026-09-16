using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;
using Alloy.UiLib.Rendering;
using Alloy.UiLib.Signals;
using AlloyClient.Utils;
using AlloyClient.Ui;
using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Logging;
using Microsoft.Extensions.Logging;
using OpenTK.Mathematics;

namespace AlloyClient.Game.Components.Hud;

public sealed class Minimap : Sprite {

    public readonly static SingleSignal<int> OnZoom = new();
    public readonly static SingleSignal<int, int> OnNewMap = new();

    private readonly static ILogger Logger = ILogger.CreateLogger(nameof(Minimap));

    private readonly static ColorTransform DefaultCt = new (1f, 1f, 1f, 1f);
    private readonly static ColorTransform FadeCt = new (0.5f, 0.5f, 0.5f, 1f);
    
    // Flash constructs MiniMap at its final 192x192 design size. Keeping this
    // native avoids resampling the map, controls, markers, and player arrow.
    public const int MapSize = 192;
    
    private float _zoom = 4.0f;
    private float _maxZoom;
    private float _zoomStep;
    private float _size;

    private bool _mouseOver;

    private readonly MinimapLayer _layer;

    private readonly IconButton _zoomIn;
    private readonly IconButton _zoomOut;
    private readonly ObjectRect _arrow;

    public Minimap() {
        TextureId = TextureType.Minimap;

        ResizeBackBuffer();
        FillData();
        
        OnZoom.Set(ZoomHandle);
        OnNewMap.Set(OnMapEnter);

        _layer = new MinimapLayer();
        AddChild(_layer);

        _zoomOut = new IconButton(new IconButtonConfig {
            Texture = TextureHelper.FromGameAtlas("lofiInterface", 54, false),
            X = MapSize - 20,
            Y = 4,
            Width = 16,
            Height = 16,
            OnClick = () => ZoomHandle(-1)
        });
        AddChild(_zoomOut);
        
        _zoomIn = new IconButton(new IconButtonConfig {
            Texture = TextureHelper.FromGameAtlas("lofiInterface", 55, false),
            X = MapSize - 20,
            Y = 14,
            Width = 16,
            Height = 16,
            OnClick = () => ZoomHandle(1)
        });
        AddChild(_zoomIn);

        _arrow = new ObjectRect(new ObjectRectConfig {
            Texture = TextureHelper.FromGameAtlas("lofiInterface", 54, false),
            X = MapSize / 2,
            Y = MapSize / 2,
            Width = 8,
            Height = 32,
            Anchor = UiAnchor.Middle
        });
        _arrow.ColorTransformation = new ColorTransform(0f, 0f, 1f, 1f);
        AddChild(_arrow);
        
        AddEventListener(Event.EnterFrame, OnFrameEnter);
        
        // :smallbrain: not valid callbacks, doesnt support lambdas /shrug >:
        //AddEventListener(MouseEventId.MouseOver, () => _mouseOver = true);
        //AddEventListener(MouseEventId.MouseOut, () => _mouseOver = false);
    }

    private void UpdateButtons() {
        if (_zoom <= 1f) {
            _zoomIn.ColorTransformation = Transforms.Default;
            _zoomOut.ColorTransformation = Transforms.Dark;
        } else if (_zoom >= _maxZoom) {
            _zoomIn.ColorTransformation = Transforms.Dark;
            _zoomOut.ColorTransformation = Transforms.Default;
        } else {
            _zoomIn.ColorTransformation = Transforms.Default;
            _zoomOut.ColorTransformation = Transforms.Default;
        }
    }

    private void ResizeBackBuffer() {
        VertexData = new VertexUi[4];
        Indices = [0, 1, 2, 0, 2, 3];
    }

    private void FillData() {
        VertexData[0] = new VertexUi(new Vector2(0, 0)); //Top Left
        VertexData[1] = new VertexUi(new Vector2(MapSize, 0)); //Top Right
        VertexData[2] = new VertexUi(new Vector2(MapSize, MapSize)); //Bottom Right
        VertexData[3] = new VertexUi(new Vector2(0, MapSize)); //Bottom Left
        
        SetGraphicsBuffer();
    }
    
    private void ZoomHandle(int zoom) {
        _zoom += _zoomStep * zoom;
        _zoom = Math.Max(1, Math.Min(_maxZoom, _zoom));
        UpdateButtons();
    }
    
    private void OnMapEnter(int w, int h) {
        var size = (float)Math.Max(w, h);
        _maxZoom = size / 32;
        _zoomStep = size / Settings.DefaultScreenWidth ;
        _size = size;
        // Flash MiniMap starts at zoom index 0 (4 screen px per tile over its
        // 192px view = 48 visible tiles). Our view diameter is _size/_zoom
        // tiles, so _zoom = _size/48 reproduces the default framing; clamp to
        // whole-map for maps smaller than 48 tiles like Flash's minZoom does.
        _zoom = Math.Max(1f, Math.Min(size / 48f, _maxZoom));
        UpdateButtons();
        Logger.Log(LogLevel.Information, $"[MinimapParity] map={w}x{h} size={size} zoom={_zoom} maxZoom={_maxZoom}");
        MinimapTexture.ClearData();
    }
    
    private void OnFrameEnter() {
        if (Map.LocalPlayer == null) return;

        _arrow.Rotation = Settings.CameraAngle;

        var pos = Map.LocalPlayer.Position;
        var size = _size / _zoom / 2.0f;

        var x1 = pos.X - size;
        var x2 = pos.X + size;
        var y1 = pos.Y - size;
        var y2 = pos.Y + size;
        VertexData[0].UV = new Vector2(x1 / 4096, y1 / 4096);
        VertexData[1].UV = new Vector2(x2 / 4096, y1 / 4096);
        VertexData[2].UV = new Vector2(x2 / 4096, y2 / 4096);
        VertexData[3].UV = new Vector2(x1 / 4096, y2 / 4096);
        
        _layer.SetSize(size);
    }
}
