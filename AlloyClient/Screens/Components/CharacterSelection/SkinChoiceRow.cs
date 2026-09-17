using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Assets.Libraries;
using AlloyClient.Utils;

namespace AlloyClient.Screens.Components.CharacterSelection;

public sealed class SkinChoiceRow : Container {
    // Flash CharacterSkinListItem.WIDTH/HEIGHT and state colors:
    // HIGHLIGHTED 0x7B7B7B, AVAILABLE 0x5A5A5A, LOCKED 0x282828.
    // Hover and selected share the highlighted color in Flash.
    public const int RowHeight = 60;

    private const uint BackgroundColor = 0x5A5A5A;
    private const uint HoverColor = 0x7B7B7B;
    private const uint SelectedColor = 0x7B7B7B;

    private readonly ColorRect _background;
    private readonly Container _selectionMarker;
    private readonly CutEdgeRect _selectionInterior;
    private readonly CutEdgeRect _selectionFill;
    private readonly bool _locked;
    private bool _selected;

    public ushort SkinType { get; }

    public SkinChoiceRow(int width, ushort textureType, ushort skinType, string name, bool locked,
        Action<SkinChoiceRow> onSelected)
        : base(new ContainerConfig { Width = width, Height = RowHeight }) {
        SkinType = skinType;
        _locked = locked;

        _background = new ColorRect(new ColorRectConfig {
            Width = width,
            Height = RowHeight,
            Color = BackgroundColor,
            Alpha = 1f
        });

        AddChild(_background);

        if (ObjectLibrary.TypeToTextureData.TryGetValue(textureType, out var textureData) && textureData != null) {
            var faceRight = textureData.AnimatedTextures.FaceRight;
            var faceDown = textureData.AnimatedTextures.FaceDown;
            var texture = faceRight is { Length: > 0 }
                ? faceRight[0]
                : faceDown is { Length: > 0 }
                    ? faceDown[0]
                    : textureData.Texture;

            if (texture != default) {
                AddChild(new ObjectRect(new ObjectRectConfig {
                    Texture = TextureHelper.Create(texture, TextureType.GameAtlas),
                    X = 39,
                    Y = RowHeight / 2,
                    Width = 50,
                    Height = 50,
                    Anchor = UiAnchor.Middle,
                    OutlineEnabled = false,
                    GlowEnabled = false
                }));
            }
        }

        // Flash: name 18pt bold at (75, 15); radio 28x28 at (WIDTH - 28 - 15),
        // vertically centered; fill 20x20 at (4, 4) with cut 2.
        AddChild(new SimpleText(new TextConfig {
            Text = name,
            FontSize = 18,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            X = 75,
            Y = RowHeight / 2,
            MaxWidth = width - 190,
            Anchor = UiAnchor.MiddleLeft
        }));

        if (locked) {
            AddChild(new SimpleText(new TextConfig {
                Text = "Locked",
                FontSize = 14,
                Color = 0xFFFFFF,
                X = width - 18,
                Y = RowHeight / 2,
                Anchor = UiAnchor.MiddleRight
            }));
        }

        _selectionMarker = new Container(new ContainerConfig {
            X = width - 29,
            Y = RowHeight / 2,
            Width = 28,
            Height = 28,
            Anchor = UiAnchor.Middle
        });

        _selectionMarker.AddChild(new CutEdgeRect(new CutEdgeConfig {
            Width = 28,
            Height = 28,
            CutX = 4,
            CutY = 4,
            Color = 0xFFFFFF
        }));

        _selectionInterior = new CutEdgeRect(new CutEdgeConfig {
            X = 2,
            Y = 2,
            Width = 24,
            Height = 24,
            CutX = 3,
            CutY = 3,
            Color = BackgroundColor
        });

        _selectionMarker.AddChild(_selectionInterior);
        _selectionFill = new CutEdgeRect(new CutEdgeConfig {
            X = 4,
            Y = 4,
            Width = 20,
            Height = 20,
            CutX = 2,
            CutY = 2,
            Color = 0xFFFFFF
        });

        _selectionFill.Visible = false;
        _selectionMarker.AddChild(_selectionFill);
        _selectionMarker.Visible = !locked;
        AddChild(_selectionMarker);

        MouseEnabled = true;
        AddEventListener(MouseEvent.MouseOver, OnMouseOver);
        AddEventListener(MouseEvent.MouseOut, OnMouseOut);
        AddEventListener(MouseEvent.LeftClick, () => {
            if (!_locked) {
                onSelected(this);
            }
        });
    }

    public void SetSelected(bool selected) {
        _selected = selected;
        _selectionFill.Visible = selected;
        _background.SetColor(selected ? SelectedColor : BackgroundColor);
        _selectionInterior.SetColor(selected ? SelectedColor : BackgroundColor);
    }

    private void OnMouseOver() {
        if (!_locked && !_selected) {
            _background.SetColor(HoverColor);
            _selectionInterior.SetColor(HoverColor);
        }
    }

    private void OnMouseOut() {
        _background.SetColor(_selected ? SelectedColor : BackgroundColor);
        _selectionInterior.SetColor(_selected ? SelectedColor : BackgroundColor);
    }
}