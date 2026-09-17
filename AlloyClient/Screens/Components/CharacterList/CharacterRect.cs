using System;
using System.Collections.Generic;
using System.Linq;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;
using AlloyClient.Assets.Libraries;
using AlloyClient.Data;
using AlloyClient.Display;
using AlloyClient.Ui;
using AlloyClient.Utils;
using OpenTK.Mathematics;

namespace AlloyClient.Screens.Components.CharacterList;

public sealed class CharacterRect : Container {
    private readonly ColorRect _background;
    private CharacterSelectionTooltip _tooltip;
    private bool _pressed;

    public CharacterRect(Character character, Action play, Action delete)
        : this(0x5C5C5C, 0x7F7F7F, play)
    {
        var props = ObjectLibrary.TypeToObjectProps.GetValueOrDefault(character.ObjectType);
        var className = props?.DisplayName ?? "Unknown";
        AddChild(SelectionGraphics.Text($"{className} {character.Level}", 18, 58, 6, 0xFFFFFF, true));
        AddPortrait(character.Skin != 0 ? character.Skin : character.ObjectType);
        var stats = GlobalData.Get<AccountData>()?.Stats.ClassStats.FirstOrDefault(s => s.ObjectType == character.ObjectType);
        var goal = FameUtils.NextStarFame(stats?.BestFame ?? 0, character.CurrentFame);
        if (goal > 0)
        {
            AddQuest($"Class Quest: {character.CurrentFame} of {goal} Fame");
        }
        var deleteButton = new Container(new ContainerConfig { X = 316, Y = 19, Width = 20, Height = 20 }) { MouseEnabled = true };
        deleteButton.AddChild(new ObjectRect(new ObjectRectConfig
        {
            Texture = TextureHelper.FromUiAtlas("CharacterList/DeleteXGraphic", padding: false),
            Width = 20,
            Height = 20,
            OutlineEnabled = false,
            GlowEnabled = false,
            GameObjectShade = false
        }));
        deleteButton.AddEventListener(MouseEvent.LeftDown, (MouseEvent e) =>
        {
            e.StopImmediatePropagation();
            _pressed = false;
            HideTooltip();
            delete();
        });
        deleteButton.AddEventListener(MouseEvent.LeftUp, (MouseEvent e) => e.StopImmediatePropagation());
        AddChild(deleteButton);
        AddEventListener(MouseEvent.MouseOver, () =>
        {
            if (_tooltip != null) return;
            _tooltip = new CharacterSelectionTooltip(character, className, stats);
            TooltipManager.AddTooltip(_tooltip);
        });
        AddEventListener(MouseEvent.MouseOut, HideTooltip);
        AddEventListener(Event.RemovedFromStage, HideTooltip);
    }

    public CharacterRect(Action create) : this(0x545454, 0x777777, create)
    {
        AddChild(SelectionGraphics.Text("New Character", 18, 58, 6, 0xFFFFFF, true));
        var classes = ObjectLibrary.TypeToClassProps.Keys.ToArray();
        if (classes.Length > 0) AddPortrait(classes[Random.Shared.Next(classes.Length)], true);
        var stars = GlobalData.Get<AccountData>()?.Stats.ClassStats.Sum(s => FameUtils.FameToStar(s.BestFame)) ?? 0;
        var remaining = FameUtils.MaxStars - stars;
        if (remaining > 0) AddQuest($"{remaining} Class quests not yet completed");
    }

    public CharacterRect(int maxCharacters, Action buy) : this(0x1F1F1F, 0x424242, buy)
    {
        var border = SelectionShape.Circle(20, 0x463E41);
        border.X = 6;
        border.Y = 6;
        AddChild(border);
        var circle = SelectionShape.Circle(19, 0x3B3536);
        circle.X = 7;
        circle.Y = 7;
        AddChild(circle);
        AddChild(new ColorRect(new ColorRectConfig { X = 18, Y = 24, Width = 16, Height = 4, Color = 0x1F1F1F }));
        AddChild(new ColorRect(new ColorRectConfig { X = 24, Y = 18, Width = 4, Height = 16, Color = 0x1F1F1F }));
        AddChild(SelectionGraphics.Text($"Buy {CharacterSelectionLayout.Ordinal(maxCharacters + 1)} Character Slot", 18, 58, 6, 0xFFFFFF, true));
        var price = SelectionGraphics.Text(CharacterSelectionLayout.SlotPrice.ToString(), 18, 313, 17, 0xFFFFFF);
        price.X -= price.Width;
        AddChild(price);
        AddChild(new ObjectRect(new ObjectRectConfig
        {
            X = 315,
            Y = 19,
            Width = 22,
            Height = 22,
            Texture = TextureHelper.FromGameAtlas("lofiObj3", 0xE0),
            GameObjectShade = false
        }));
    }

    private CharacterRect(uint color, uint hover, Action action)
        : base(new ContainerConfig { Width = CharacterSelectionLayout.RowWidth, Height = CharacterSelectionLayout.RowHeight })
    {
        MouseEnabled = true;
        _background = new ColorRect(new ColorRectConfig { Width = CharacterSelectionLayout.RowWidth, Height = CharacterSelectionLayout.RowHeight, Color = color });
        AddChild(_background);
        AddEventListener(MouseEvent.MouseOver, () => _background.SetColor(hover));
        AddEventListener(MouseEvent.MouseOut, () => { _background.SetColor(color); _pressed = false; });
        AddEventListener(MouseEvent.LeftDown, () => _pressed = true);
        AddEventListener(MouseEvent.LeftUp, () =>
        {
            if (!_pressed) return;
            _pressed = false;
            HideTooltip();
            action();
        });
    }

    private void AddPortrait(ushort type, bool dimmed = false)
    {
        if (!ObjectLibrary.TypeToTextureData.TryGetValue(type, out var texture)) return;
        var portrait = new ObjectRect(new ObjectRectConfig
        {
            Texture = texture.AnimatedTextures is { } animation
                ? TextureHelper.Create(animation.FaceRight[0], TextureType.GameAtlas)
                : TextureHelper.FromGameAtlas(type),
            Width = 50,
            Height = 50
        });
        if (dimmed) portrait.ColorTransformation = new ColorTransform(0, 0, 0, 0.5f);
        AddChild(portrait);
    }

    private void AddQuest(string text)
    {
        var star = new ObjectRect(new ObjectRectConfig
        {
            Texture = TextureHelper.FromUiAtlas("CharacterList/StarGraphic", padding: false),
            X = 58,
            Y = CharacterSelectionLayout.QuestIconY,
            Width = 12,
            Height = 12,
            OutlineEnabled = false,
            GlowEnabled = false
        })
        { ColorTransformation = new ColorTransform(179 / 255f, 179 / 255f, 179 / 255f, 1) };
        AddChild(star);
        AddChild(SelectionGraphics.Text(text, 14, 72, CharacterSelectionLayout.QuestTextY));
    }

    private void HideTooltip()
    {
        if (_tooltip == null) return;
        TooltipManager.RemoveTooltip(_tooltip);
        _tooltip = null;
    }
}
