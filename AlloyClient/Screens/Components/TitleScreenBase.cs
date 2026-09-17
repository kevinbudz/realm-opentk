using System;
using Alloy.Engine;
using Alloy.UiLib.Core;
using System.Linq;
using Alloy.UiLib.BuiltIn;
using AlloyClient.Data;
using AlloyClient.Display;
using AlloyClient.Ui;
using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Ui.Components.Elements;
using AlloyClient.Ui.Components.Graphics;

namespace AlloyClient.Screens.Components;

public enum ScreenType {
    Loading,
    Title,
    Other
}

public abstract class TitleScreenBase : Screen {

    protected const int MenuGap = 50;

    // AccountScreen.as keeps the header in the unscaled window chrome. These
    // are Flash design-space offsets; OnResize converts them to window px.
    private const int MusicX = 0;
    private const int MusicY = 0;
    private const int RankX = 36;
    private const int RankY = 4;
    private const int AccountInfoMargin = 10;
    private const int AccountInfoY = 2;

    private readonly ScreenDarkenOverlay _darken = new();
    private readonly ScreenGraphic _fallbackBackground;
    private readonly MapBackground _mapBackground = new();
    private readonly TitleMenuRibbon _menuRibbon = new(Settings.DefaultScreenWidth);

    private readonly MusicButton _music = new(new MusicButtonConfig { X = MusicX, Y = MusicY, Width = 32, Height = 32 });

    private readonly Container _accountIdentity = new(new ContainerConfig { X = RankX, Y = RankY });

    protected readonly Container MenuBar = new(new ContainerConfig {
        X = Settings.DefaultScreenWidth / 2,
        Y = TitleMenuRibbon.MenuCenterY,
        Anchor = UiAnchor.LeftTop
    });

    protected readonly AccountOverlay Overlay;

    protected TitleScreenBase(ScreenType type = ScreenType.Other) {
        // Static splash shown until the live map backdrop loads (asset
        // libraries arrive after the loading screen is already up).
        _fallbackBackground = new ScreenGraphic(type == ScreenType.Title);
        AddChild(_fallbackBackground);

        // Flash MenuBackground dims every menu over the map. Loading keeps
        // its undimmed look; everything else gets the dim treatment.
        if (type != ScreenType.Loading) {
            AddChild(_darken);
        }

        AddChild(_music);

        AddChild(_accountIdentity);
        RefreshAccountIdentity();

        Overlay = new AccountOverlay(type == ScreenType.Title) {
            X = Settings.DefaultScreenWidth - 10,
            Y = 10
        };

        Overlay.SetAnchor(UiAnchor.RightTop);

        Overlay.AddEventListener(AccountOverlay.AccountChangedEvent, RefreshAccountIdentity);
        AddChild(Overlay);

        AddChild(_menuRibbon);
        AddChild(MenuBar);

        AddEventListener(Event.AddedToStage, OnStageEnter);
        AddEventListener(Event.RemovedFromStage, OnStageExit);
    }

    private void OnStageEnter() {
        Stage.AddEventListener(ResizeEvent.Resize, OnResize);
        OnResize(new ResizeEvent(ResizeEvent.Resize, Stage.StageWidth, Stage.StageHeight));
    }

    private void OnStageExit() {
        Stage.RemoveEventListener(ResizeEvent.Resize, OnResize);
    }

    public override void Update(GameTime gameTime) {
        _mapBackground.Update(gameTime);
        _fallbackBackground.Visible = !_mapBackground.IsActive;
    }

    public override void Draw(GameTime gameTime) {
        _mapBackground.Draw(gameTime);
    }

    protected override void OnResize(ResizeEvent args) {
        _mapBackground.Resize(args.Width, args.Height);

        var scale = Stage.ScreenScale;

        _music.X = (int)MathF.Round(MusicX * scale.X);
        _music.Y = (int)MathF.Round(MusicY * scale.Y);

        _accountIdentity.Scale = scale;
        _accountIdentity.X = (int)MathF.Round(RankX * scale.X);
        _accountIdentity.Y = (int)MathF.Round(RankY * scale.Y);

        Overlay.Scale = scale;
        Overlay.X = args.Width - (int)MathF.Round(AccountInfoMargin * scale.X);
        Overlay.Y = (int)MathF.Round(AccountInfoY * scale.Y);

        var contentWidth = (int)System.Math.Ceiling(args.Width / scale.X);
        _menuRibbon.ResizeWidth(contentWidth);
        _menuRibbon.Scale = scale;
        _menuRibbon.X = 0;
        _menuRibbon.Y = (int)(TitleMenuRibbon.TopY * scale.Y);

        MenuBar.Scale = scale;
        MenuBar.X = args.Width / 2;
        MenuBar.Y = (int)(TitleMenuRibbon.MenuCenterY * scale.Y);
    }

    private void RefreshAccountIdentity() {
        _accountIdentity.RemoveChildren();

        var stars = 0;
        var guildName = string.Empty;
        if (GlobalData.TryGet<AccountData>(out var account)) {
            stars = account.Stats.ClassStats.Sum(stats => FameUtils.FameToStar(stats.BestFame));
            guildName = account.GuildName ?? string.Empty;
        }

        var starCount = new SimpleText(new TextConfig {
            Text = stars.ToString(),
            FontSize = 24,
            FontType = FontType.Bold,
            Color = 0xB3B3B3
        });

        starCount.Y = (32 - starCount.Height) / 2;
        _accountIdentity.AddChild(starCount);

        var star = new FameStar(32, stars) {
            X = starCount.Width + 7
        };

        _accountIdentity.AddChild(star);

        if (string.IsNullOrWhiteSpace(guildName)) {
            return;
        }

        var guild = new SimpleText(new TextConfig {
            Text = guildName,
            FontSize = 24,
            Color = 0xB3B3B3,
            X = star.X + star.Width + 10
        });

        guild.Y = (32 - guild.Height) / 2;
        _accountIdentity.AddChild(guild);
    }
}
