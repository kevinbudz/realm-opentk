using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;
using AlloyClient.Assets.Libraries;
using AlloyClient.Ui;
using AlloyClient.Ui.Components.Graphics;
using AlloyClient.Utils;

namespace AlloyClient.Screens.Components.CharacterSelection;

public sealed class ClassChoiceCard : Container {
    // Flash NewCharacterScreen places one CharacterBox per 140px cell
    // (x = 50 + 140 * col + 70 - w / 2, y = 88 + 140 * row); the card keeps
    // that pitch with a 10px gutter on each side.
    public const int CardWidth = 120;
    public const int CardHeight = 100;

    private const uint BackgroundColor = 0x2B2B2B;
    private const uint HoverColor = 0x4A4A4A;

    private readonly ColorRect _background;

    public ushort Type { get; }

    public ClassChoiceCard(ushort type, int bestFame, Action<ClassChoiceCard> onSelected)
        : base(new ContainerConfig { Width = CardWidth, Height = CardHeight }) {
        Type = type;

        _background = new ColorRect(new ColorRectConfig {
            Width = CardWidth,
            Height = CardHeight,
            Color = BackgroundColor,
            Alpha = 1f
        });

        AddChild(_background);
        AddChild(new CutCornerOutline(CardWidth, CardHeight));

        var textureData = ObjectLibrary.TypeToTextureData[type];
        var frames = textureData.AnimatedTextures.FaceRight;
        var texture = frames is { Length: > 0 } ? frames[0] : textureData.Texture;
        AddChild(new ObjectRect(new ObjectRectConfig {
            Texture = TextureHelper.Create(texture, TextureType.GameAtlas),
            X = CardWidth / 2,
            Y = 28,
            Width = 48,
            Height = 48,
            Anchor = UiAnchor.Middle,
            OutlineEnabled = false,
            GlowEnabled = false
        }));

        AddStars(FameUtils.FameToStar(bestFame));

        var props = ObjectLibrary.TypeToObjectProps[type];
        AddChild(new SimpleText(new TextConfig {
            Text = props.DisplayName,
            FontSize = 14,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            X = CardWidth / 2,
            Y = 84,
            MaxWidth = CardWidth - 16,
            Anchor = UiAnchor.Middle
        }));

        MouseEnabled = true;
        AddEventListener(MouseEvent.MouseOver, () => _background.SetColor(HoverColor));
        AddEventListener(MouseEvent.MouseOut, () => _background.SetColor(BackgroundColor));
        AddEventListener(MouseEvent.LeftClick, () => onSelected(this));
    }

    private void AddStars(int earnedStars) {
        // Flash CharacterBox tints: full 0.8 gray, empty 0.2 gray, no background.
        const int size = 16;
        const int startX = CardWidth / 2 - size * 2;
        for (var i = 0; i < FameUtils.StarFameRequirements.Length; i++) {
            var star = new ObjectRect(new ObjectRectConfig {
                Texture = TextureHelper.FromUiAtlas("CharacterList/StarGraphic"),
                X = startX + i * size,
                Y = 62,
                Width = size,
                Height = size,
                Anchor = UiAnchor.Middle,
                OutlineEnabled = false,
                GlowEnabled = false
            });

            star.ColorTransformation = i < earnedStars
                ? new ColorTransform(0.8f, 0.8f, 0.8f, 1f)
                : new ColorTransform(0.2f, 0.2f, 0.2f, 1f);

            AddChild(star);
        }
    }
}