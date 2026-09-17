using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using OpenTK.Mathematics;

namespace AlloyClient.Screens.Components.CharacterList;

public sealed class CharacterListScrollbar : Container {
    private const int TrackY = 17;
    private const int TrackHeight = 365;
    private readonly CutEdgeRect _handle;
    private readonly Action<int> _scroll;
    private readonly int _overflow;
    private readonly int _travel;
    private float _position;
    private bool _dragging;
    private int _dragOffset;
    private int _direction;
    private double _lastTime;

    public CharacterListScrollbar(int contentHeight, Action<int> scroll)
        : base(new ContainerConfig { X = 375, Y = 113, Width = 16, Height = 399 }) {
        _scroll = scroll;
        _overflow = contentHeight - CharacterSelectionLayout.ViewHeight;
        MouseEnabled = true;
        var track = new ColorRect(new ColorRectConfig {
            Y = TrackY, Width = 16, Height = TrackHeight, Color = 0x545454, MouseEnabled = true
        });
        AddChild(track);
        track.AddEventListener(MouseEvent.LeftDown, () => Move(GetRelativeMousePosition().Y < _handle.Y ? -399 : 399));
        var height = Math.Clamp(399 * TrackHeight / contentHeight, 16, TrackHeight);
        _travel = TrackHeight - height;
        _handle = new CutEdgeRect(new CutEdgeConfig {
            Y = TrackY, Width = 16, Height = height, CutX = 4, CutY = 4, Color = 0xFFFFFF, MouseEnabled = true
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
            if (_dragging && _travel > 0) SetPosition((GetRelativeMousePosition().Y - _dragOffset - TrackY) / (float)_travel);
        });
        AddEventListener(MouseEvent.LeftUp, Stop);
        AddEventListener(Event.RemovedFromStage, Stop);
        AddArrow([new(0, 12), new(8, 0), new(16, 12)], 0, -1);
        AddArrow([new(0, 0), new(16, 0), new(8, 12)], 387, 1);
    }

    public void Wheel(MouseEvent e) {
        if (e.VerticalDelta == 0) return;
        Move(e.VerticalDelta > 0 ? -80 : 80);
        e.StopImmediatePropagation();
    }

    private static void AddHover(Sprite sprite) {
        sprite.AddEventListener(MouseEvent.MouseOver, () => sprite.SetColor(0xFFDC85));
        sprite.AddEventListener(MouseEvent.MouseOut, () => sprite.SetColor(0xFFFFFF));
    }

    private void AddArrow(Vector2[] points, int y, int direction) {
        var arrow = new SelectionShape(points, 0xFFFFFF) { Y = y, MouseEnabled = true };
        AddHover(arrow);
        AddChild(arrow);
        arrow.AddEventListener(MouseEvent.LeftDown, () => {
            _direction = direction;
            _lastTime = Stage.GameTime.TotalMs;
            CapturePointer();
            AddEventListener(Event.EnterFrame, Advance);
        });
    }

    private void Advance() {
        if (Stage == null || _travel <= 0) return;
        var now = Stage.GameTime.TotalMs;
        SetPosition(_position + (float)((now - _lastTime) / 1000) * 351 * _direction / _travel);
        _lastTime = now;
    }

    private void Stop() {
        _dragging = false;
        _direction = 0;
        RemoveEventListener(Event.EnterFrame, Advance);
        ReleasePointer();
    }

    private void Move(int pixels) {
        if (_overflow > 0) SetPosition(_position + pixels / (float)_overflow);
    }

    private void SetPosition(float position) {
        _position = Math.Clamp(position, 0, 1);
        _handle.Y = TrackY + (int)MathF.Round(_position * _travel);
        _scroll((int)MathF.Round(_position * _overflow));
    }
}
