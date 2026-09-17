using System;
using System.Collections.Generic;
using System.Linq;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;
using AlloyClient.Assets.Libraries;
using AlloyClient.Data;
using AlloyClient.Display;
using AlloyClient.Game;
using AlloyClient.Screens.Components.CharacterSelection;
using AlloyClient.Ui;
using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Ui.Components.Scrollbars;
using AlloyClient.Utils;

namespace AlloyClient.Screens.Components.Containers;

public sealed class ClassContainer : Container {
    private const int ScreenWidth = Settings.DefaultScreenWidth;
    private const int ScreenHeight = Settings.DefaultScreenHeight;

    // Flash CharacterSkinView / NewCharacterScreen geometry in 800x600 design units.
    private const int DividerY = 105;
    private const int DividerX = 346;
    private const int DividerBottomY = 526;
    private const int DetailX = 5;
    private const int DetailY = 110;
    private const int DetailWidth = 344;
    private const int DetailTextWidth = 188;
    private const int DetailStatsRightX = 205;
    private const int DetailStatsValueX = 223;
    private const int SkinListX = 351;
    private const int SkinListY = 110;
    private const int SkinListWidth800 = 442;
    private const int SkinListHeight800 = 400;
    private const uint DividerColor = 0x545454;

    // Flash NewCharacterScreen class grid: 5 columns on a 140px pitch.
    private const int ClassColumns = 5;
    private const int ClassPitch = 140;
    private const int ClassGridX = 50;
    private const int ClassGridY = 88;
    private const int ClassBackY = 524;

    // Flash CharacterSkinView buttons.
    private const int PlayButtonY = 520;
    private const int DetailBackX = 30;
    private const int DetailBackY = 534;

    private readonly Container _classScreen;
    private readonly Container _detailScreen;
    private readonly List<SkinChoiceRow> _skinRows = [];
    private readonly Action _onBack;

    private ushort _selectedClassType;
    private ushort _selectedSkinType;
    private int _screenWidth = ScreenWidth;
    private ObjectRect _detailPortrait;

    private int SkinListWidth => Math.Max(SkinListWidth800, _screenWidth - (SkinListX + 7));

    public ClassContainer(Action onBack)
        : base(new ContainerConfig { Width = ScreenWidth, Height = ScreenHeight }) {
        _onBack = onBack;

        _classScreen = new Container(new ContainerConfig { Width = ScreenWidth, Height = ScreenHeight });
        _detailScreen = new Container(new ContainerConfig { Width = ScreenWidth, Height = ScreenHeight });
        _detailScreen.Visible = false;

        AddChild(_classScreen);
        AddChild(_detailScreen);
        AddEventListener(Event.EnterFrame, AnimateDetailPortrait);

        BuildClassScreen();
    }

    public void ResizeLayout(int width) {
        width = Math.Max(1, width);
        if (_screenWidth == width) {
            return;
        }

        _screenWidth = width;
        Resize(_screenWidth, ScreenHeight);
        _classScreen.Resize(_screenWidth, ScreenHeight);
        _detailScreen.Resize(_screenWidth, ScreenHeight);

        _classScreen.RemoveChildren();
        BuildClassScreen();

        if (_detailScreen.Visible) {
            RebuildDetailScreen();
        }
    }

    private void BuildClassScreen() {
        // Flash NewCharacterScreen: x = 50 + 140 * col + 70 - w / 2, y = 88 + 140 * row.
        var classes = ObjectLibrary.TypeToClassProps.Keys.ToArray();
        for (var i = 0; i < classes.Length; i++) {
            var type = classes[i];
            var stats = GetClassStats(type);
            var card = new ClassChoiceCard(type, stats?.BestFame ?? 0, ShowClassDetails) {
                X = ClassGridX + ClassPitch * (i % ClassColumns) + ClassPitch / 2 - ClassChoiceCard.CardWidth / 2,
                Y = ClassGridY + ClassPitch * (i / ClassColumns)
            };

            _classScreen.AddChild(card);
        }

        // Flash: "back" 36pt, centered, top edge at y = 524.
        _classScreen.AddChild(new MenuBarButton(new TextButtonConfig {
            Text = "back",
            FontSize = 36,
            FontType = FontType.Bold,
            OutlineThickness = 4,
            OnClicked = _onBack,
            X = _screenWidth / 2,
            Y = ClassBackY,
            Anchor = UiAnchor.MiddleTop
        }));
    }

    private void ShowClassDetails(ClassChoiceCard card) {
        _selectedClassType = card.Type;
        _selectedSkinType = 0;

        _classScreen.Visible = false;
        _detailScreen.Visible = true;
        RebuildDetailScreen();
    }

    private void RebuildDetailScreen() {
        _detailScreen.RemoveChildren();
        _skinRows.Clear();

        // Flash: 2px 0x545454 vertical line at x = 346 from y = 105 to y = 526.
        _detailScreen.AddChild(new ColorRect(new ColorRectConfig {
            X = DividerX,
            Y = DividerY,
            Width = 2,
            Height = DividerBottomY - DividerY,
            Color = DividerColor,
            Alpha = 1f
        }));

        BuildClassDetails();
        BuildSkinList();
        BuildDetailNavigation();
    }

    private void BuildClassDetails() {
        // Flash ClassDetailView (WIDTH 344 at x = 5, y = 110): labels right-justified
        // at x = 205, values/stars at x = 223, all text with DropShadow(0,0,0,1,8,8).
        var props = ObjectLibrary.TypeToObjectProps[_selectedClassType];
        var stats = GetClassStats(_selectedClassType);
        var bestFame = stats?.BestFame ?? 0;
        var stars = FameUtils.FameToStar(bestFame);
        var textureData = ObjectLibrary.TypeToTextureData[_selectedClassType];
        const int centerX = DetailWidth / 2;
        var info = new Container(new ContainerConfig { Width = DetailWidth });

        var faceRight = textureData.AnimatedTextures.FaceRight;
        var portraitTexture = faceRight is { Length: > 0 } ? faceRight[0] : textureData.Texture;
        _detailPortrait = new ObjectRect(new ObjectRectConfig {
            Texture = TextureHelper.Create(portraitTexture, TextureType.GameAtlas),
            X = centerX,
            Y = 52,
            Width = 104,
            Height = 104,
            Anchor = UiAnchor.Middle,
            OutlineEnabled = false,
            GlowEnabled = false
        });

        info.AddChild(_detailPortrait);

        var name = new SimpleText(new TextConfig {
            Text = props.DisplayName,
            FontSize = 20,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            DropShadow = new DropShadowFilter(0, 0, 0, 1, 8, 8),
            X = centerX,
            Y = 110,
            Anchor = UiAnchor.MiddleTop
        });

        info.AddChild(name);

        var description = new SimpleText(new TextConfig {
            Text = props.Description,
            FontSize = 14,
            Color = 0xFFFFFF,
            DropShadow = new DropShadowFilter(0, 0, 0, 1, 8, 8),
            X = centerX,
            Y = 110 + name.Height + 5,
            MaxWidth = DetailTextWidth,
            Anchor = UiAnchor.MiddleTop
        });

        info.AddChild(description);

        var questY = description.Y + description.Height + 20;
        var questLabel = AddDetailLabel(info, "Class Quests Completed", questY);
        AddDetailStars(info, stars, DetailStatsValueX, questY);
        var levelY = questY + questLabel.Height + 5;
        var levelLabel = AddDetailLabel(info, "Highest Level Achieved", levelY);
        AddDetailValue(info, (stats?.BestLevel ?? 0).ToString(), levelY);
        var fameY = levelY + levelLabel.Height + 5;
        var fameLabel = AddDetailLabel(info, "Most Fame Achieved", fameY);
        var fameValue = AddDetailValue(info, bestFame.ToString(), fameY, 0xEACC6C);
        info.AddChild(new ObjectRect(new ObjectRectConfig {
            Texture = TextureHelper.FromGameAtlas("lofiObj3", 0xE0),
            X = fameValue.X + fameValue.Width - 3,
            Y = fameY - 7,
            Width = 18,
            Height = 18,
            Anchor = UiAnchor.LeftTop,
            OutlineEnabled = false,
            GlowEnabled = false
        }));

        // Flash hides both next-goal texts when there is no next goal (-1).
        var nextFame = FameUtils.NextStarFame(bestFame, 0);
        var hasGoal = nextFame >= 0;
        var nextGoalLabelY = fameY + fameLabel.Height + 17;
        var nextGoalLabel = new SimpleText(new TextConfig {
            Text = "Next Goal:",
            FontSize = 14,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            DropShadow = new DropShadowFilter(0, 0, 0, 1, 8, 8),
            X = centerX,
            Y = nextGoalLabelY,
            Anchor = UiAnchor.MiddleTop
        });

        nextGoalLabel.Visible = hasGoal;
        info.AddChild(nextGoalLabel);

        var nextGoalText = new SimpleText(new TextConfig {
            Text = $"Earn {nextFame} Fame with a {props.DisplayName}",
            FontSize = 14,
            Color = 0xFFFFFF,
            DropShadow = new DropShadowFilter(0, 0, 0, 1, 8, 8),
            X = centerX,
            Y = nextGoalLabelY + nextGoalLabel.Height,
            Anchor = UiAnchor.MiddleTop
        });

        nextGoalText.Visible = hasGoal;
        info.AddChild(nextGoalText);

        var contentHeight = hasGoal
            ? nextGoalText.Y + nextGoalText.Height
            : fameY + fameLabel.Height;

        info.Resize(DetailWidth, contentHeight);
        info.X = DetailX;
        info.Y = DetailY;
        _detailScreen.AddChild(info);
    }

    private void BuildSkinList() {
        // Flash CharacterSkinListView: 442x400 at (351, 110) with 5px item padding;
        // items are 420 wide (CharacterSkinListItem.WIDTH).
        const int rowGap = 5;
        const int rowWidth = 420;
        var listWidth = SkinListWidth;
        var listClip = new Container(new ContainerConfig {
            X = SkinListX,
            Y = SkinListY,
            Width = listWidth,
            Height = SkinListHeight800,
            EnableClip = true
        });

        _detailScreen.AddChild(listClip);

        var rowContainer = new Container { X = 5 };
        listClip.AddChild(rowContainer);

        var rowIndex = 0;
        AddSkinRow(rowContainer, rowWidth, rowIndex++, _selectedClassType, 0, "Classic", false);

        var ownedSkins = GlobalData.Get<CharacterListData>()?.OwnedSkins ?? [];
        var skins = ObjectLibrary.TypeToObjectProps.Values
            .Where(props => props.Skin && !props.NoSkinSelect && props.PlayerClassType == _selectedClassType)
            .OrderBy(props => props.DisplayName);

        foreach (var skinProps in skins) {
            var skinType = skinProps.ObjectType;
            AddSkinRow(rowContainer, rowWidth, rowIndex++, skinType, skinType, skinProps.DisplayName,
                !ownedSkins.Contains(skinType));
        }

        var contentHeight = rowIndex * (SkinChoiceRow.RowHeight + rowGap) - rowGap;
        if (contentHeight <= SkinListHeight800) {
            return;
        }

        listClip.AddChild(new VerticalScrollBar(listClip, new VerticalScrollBarConfig {
            X = listWidth - 12,
            Width = 10,
            Height = SkinListHeight800,
            TotalContentHeight = contentHeight,
            VisibleContentHeight = SkinListHeight800,
            ScrollStep = SkinChoiceRow.RowHeight + rowGap,
            OnValueChanged = value => rowContainer.Y = -value
        }));
    }

    private void AddSkinRow(Container parent, int width, int index, ushort textureType, ushort skinType,
        string name, bool locked) {
        var row = new SkinChoiceRow(width, textureType, skinType, name, locked, SelectSkin) {
            Y = index * (SkinChoiceRow.RowHeight + 5)
        };

        parent.AddChild(row);
        _skinRows.Add(row);

        if (skinType == _selectedSkinType) {
            row.SetSelected(true);
        }
    }

    private void SelectSkin(SkinChoiceRow selectedRow) {
        _selectedSkinType = selectedRow.SkinType;
        foreach (var row in _skinRows) {
            row.SetSelected(row == selectedRow);
        }
    }

    private void BuildDetailNavigation() {
        // Flash: "back" 22pt at (30, 534), "play" 36pt centered at y = 520.
        _detailScreen.AddChild(new MenuBarButton(new TextButtonConfig {
            Text = "back",
            FontSize = 22,
            FontType = FontType.Bold,
            OutlineThickness = 4,
            OnClicked = ShowClassScreen,
            X = DetailBackX,
            Y = DetailBackY,
            Anchor = UiAnchor.LeftTop
        }));

        _detailScreen.AddChild(new MenuBarButton(new TextButtonConfig {
            Text = "play",
            FontSize = 36,
            FontType = FontType.Bold,
            OutlineThickness = 4,
            OnClicked = Play,
            X = _screenWidth / 2,
            Y = PlayButtonY,
            Anchor = UiAnchor.MiddleTop
        }));
    }

    private void ShowClassScreen() {
        _detailScreen.Visible = false;
        _classScreen.Visible = true;
        _detailPortrait = null;
    }

    private void Play() {
        GlobalData.CharacterType = _selectedClassType;
        GlobalData.CharacterSkin = _selectedSkinType;
        ScreenManager.FadeToScreen(new GameScreen(), Easing.SineInOut, 1000, 0x0);
    }

    private static SimpleText AddDetailLabel(Container parent, string text, int top) {
        // Flash: 14pt bold labels right-justified at x = 205; y is the top edge.
        var label = new SimpleText(new TextConfig {
            Text = text,
            FontSize = 14,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            DropShadow = new DropShadowFilter(0, 0, 0, 1, 8, 8),
            X = DetailStatsRightX,
            Anchor = UiAnchor.MiddleRight
        });

        label.Y = top + label.Height / 2;
        parent.AddChild(label);
        return label;
    }

    private static SimpleText AddDetailValue(Container parent, string text, int top, uint color = 0xFFFFFF) {
        // Flash: 16pt bold values left-aligned at x = 223.
        var value = new SimpleText(new TextConfig {
            Text = text,
            FontSize = 16,
            FontType = FontType.Bold,
            Color = color,
            DropShadow = new DropShadowFilter(0, 0, 0, 1, 8, 8),
            X = DetailStatsValueX,
            Anchor = UiAnchor.MiddleLeft
        });

        value.Y = top + value.Height / 2;
        parent.AddChild(value);
        return value;
    }

    private void AnimateDetailPortrait() {
        if (!_detailScreen.Visible || _detailPortrait == null ||
            !ObjectLibrary.TypeToTextureData.TryGetValue(_selectedClassType, out var textureData)) {
            return;
        }

        var frames = textureData.AnimatedTextures.FaceRight;
        if (frames is not { Length: > 2 }) {
            return;
        }

        const int frameDuration = 250;
        var frameIndex = 1 + (int)(Stage.GameTime.TotalMs / frameDuration) % 2;
        _detailPortrait.ChangeTexture(TextureHelper.Create(frames[frameIndex], TextureType.GameAtlas));
    }

    private static void AddDetailStars(Container parent, int earnedStars, int x, int y) {
        // Flash StarsView: 0x252525 background with a 4px margin, filled white
        // stars and 0x838383 empty stars.
        const int size = 16;
        const int margin = 4;
        const float emptyTint = 131f / 255f;
        parent.AddChild(new ColorRect(new ColorRectConfig {
            X = x,
            Y = y,
            Width = size * FameUtils.StarFameRequirements.Length + margin * 2,
            Height = size + margin * 2,
            Color = 0x252525,
            Alpha = 1f
        }));

        for (var i = 0; i < FameUtils.StarFameRequirements.Length; i++) {
            var star = new ObjectRect(new ObjectRectConfig {
                Texture = TextureHelper.FromUiAtlas("CharacterList/StarGraphic"),
                X = x + margin + i * size,
                Y = y + margin,
                Width = size,
                Height = size,
                Anchor = UiAnchor.LeftTop,
                OutlineEnabled = false,
                GlowEnabled = false
            });

            if (i >= earnedStars) {
                star.ColorTransformation = new ColorTransform(emptyTint, emptyTint, emptyTint, 1f);
            }

            parent.AddChild(star);
        }
    }

    private static ClassStats GetClassStats(ushort type) {
        return GlobalData.Get<AccountData>()?.Stats?.ClassStats?.FirstOrDefault(stats => stats.ObjectType == type);
    }

}