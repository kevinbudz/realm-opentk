using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Screens.Components.CharacterList;
using OpenTK.Mathematics;

namespace AlloyClient.Ui.Components.Scrollbars;

public struct VerticalScrollBarConfig {
    public int X = 0;
    public int Y = 0;
    public int Width = 0;
    public int Height = 0;
    public int TotalContentHeight = 0;
    public int VisibleContentHeight = 0;
    public Action<int> OnValueChanged = null;

    // When set, scroll value will be incremented directly from this value.
    // (e.g. when set to 32, value changes will be 32, 64, 96, etc.)
    public int ScrollStep = -1;

    public VerticalScrollBarConfig() { }
}

// Flash parity: com.company.assembleegameclient.ui.Scrollbar.
// Layout is derived from the bar width exactly like Flash resize():
// arrowHeight = width * 0.75, track inset 5px from each arrow.
public readonly record struct ScrollbarLayout(int ArrowHeight, int TrackY, int TrackHeight);

public class VerticalScrollBar : Sprite {
    private const uint TrackColor = 0x545454;
    private const uint HandleColor = 0xFFFFFF;
    private const uint HoverColor = 0xFFDC85;

    private readonly CutEdgeRect _handle;
    private readonly Action<int> _onValueChanged;

    private readonly int _scrollStep;
    private readonly int _trackY;
    private readonly int _trackHeight;
    private readonly int _visibleHeight;
    private readonly int _travel;
    private readonly int _overflow;
    private float _position;
    private bool _dragging;
    private int _dragOffset;
    private int _direction;
    private double _lastTime;

    public VerticalScrollBar(Container clipRect, VerticalScrollBarConfig config) {
        X = config.X;
        Y = config.Y;
        _onValueChanged = config.OnValueChanged;

        _scrollStep = config.ScrollStep == -1 ? config.Height / 20 : config.ScrollStep;

        var layout = CalculateLayout(config.Width, config.Height);
        _trackY = layout.TrackY;
        _trackHeight = layout.TrackHeight;
        _visibleHeight = config.VisibleContentHeight;
        _overflow = config.TotalContentHeight - config.VisibleContentHeight;

        MouseEnabled = true;

        var track = new ColorRect(new ColorRectConfig {
            Y = _trackY, Width = config.Width, Height = _trackHeight, Color = TrackColor, MouseEnabled = true
        });
        AddChild(track);
        track.AddEventListener(MouseEvent.LeftDown,
            () => Move(GetRelativeMousePosition().Y < _handle.Y ? -_visibleHeight : _visibleHeight));

        var handleHeight = CalculateHandleHeight(_trackHeight, config.Width, config.TotalContentHeight, config.VisibleContentHeight);
        _travel = _trackHeight - handleHeight;
        _handle = new CutEdgeRect(new CutEdgeConfig {
            Y = _trackY, Width = config.Width, Height = handleHeight, CutX = 4, CutY = 4, Color = HandleColor, MouseEnabled = true
        });
        AddChild(_handle);
        AddHover(_handle);
        _handle.AddEventListener(MouseEvent.LeftDown, (MouseEvent e) => {
            e.StopImmediatePropagation();
            _dragging = true;
            _dragOffset = GetRelativeMousePosition().Y - _handle.Y;
            CapturePointer();
        });
        AddEventListener(MouseEvent.MouseMove, () => {
            if (_dragging && _travel > 0)
                SetPosition((GetRelativeMousePosition().Y - _dragOffset - _trackY) / (float) _travel);
        });
        AddEventListener(MouseEvent.LeftUp, Stop);
        AddEventListener(Event.RemovedFromStage, Stop);

        var arrowHeight = layout.ArrowHeight;
        var half = config.Width / 2f;
        AddArrow([new Vector2(0, arrowHeight), new Vector2(half, 0), new Vector2(config.Width, arrowHeight)], 0, -1);
        AddArrow([new Vector2(0, 0), new Vector2(config.Width, 0), new Vector2(half, arrowHeight)], config.Height - arrowHeight, 1);

        clipRect.AddEventListener(MouseEvent.ScrollVertical, Scroll);
    }

    public static ScrollbarLayout CalculateLayout(int width, int height) {
        var arrowHeight = (int) (width * 0.75f);
        return new ScrollbarLayout(arrowHeight, arrowHeight + 5, height - arrowHeight * 2 - 10);
    }

    public static int CalculateHandleHeight(int trackHeight, int width, int totalContentHeight, int visibleContentHeight) {
        if (totalContentHeight <= 0) return trackHeight;
        var handleHeight = (int) (trackHeight * (visibleContentHeight / (float) totalContentHeight));
        return Math.Clamp(handleHeight, width, trackHeight);
    }

    private static void AddHover(Sprite sprite) {
        sprite.AddEventListener(MouseEvent.MouseOver, () => sprite.SetColor(HoverColor));
        sprite.AddEventListener(MouseEvent.MouseOut, () => sprite.SetColor(HandleColor));
    }

    private void AddArrow(Vector2[] points, int y, int direction) {
        var arrow = new SelectionShape(points, HandleColor) { Y = y, MouseEnabled = true };
        AddHover(arrow);
        AddChild(arrow);
        arrow.AddEventListener(MouseEvent.LeftDown, () => {
            if (Stage == null) return;
            _direction = direction;
            _lastTime = Stage.GameTime.TotalMs;
            CapturePointer();
            AddEventListener(Event.EnterFrame, Advance);
        });
    }

    private void Advance() {
        if (Stage == null || _travel <= 0) return;
        var now = Stage.GameTime.TotalMs;
        // Flash arrow speed (speed_ = 1.0): full indicator traverse per second.
        SetPosition(_position + (float) ((now - _lastTime) / 1000) * _direction);
        _lastTime = now;
    }

    private void Stop() {
        _dragging = false;
        _direction = 0;
        RemoveEventListener(Event.EnterFrame, Advance);
        ReleasePointer();
    }

    private void Scroll(MouseEvent args) {
        if (args.VerticalDelta == 0) return;
        Move((int) (-args.VerticalDelta * _scrollStep));
    }

    private void Move(int pixels) {
        if (_overflow > 0) SetPosition(_position + pixels / (float) _overflow);
    }

    private void SetPosition(float position) {
        _position = Math.Clamp(position, 0, 1);
        _handle.Y = _trackY + (int) MathF.Round(_position * _travel);
        var scrollY = (int) MathF.Round(_position * _overflow);
        _onValueChanged(scrollY);
    }
}
