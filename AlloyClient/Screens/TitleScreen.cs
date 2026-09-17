using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Data;
using AlloyClient.Display;
using AlloyClient.Screens.Components;
using AlloyClient.Screens.Components.Containers.Account;
using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Ui.Components.Dialogs;
using AlloyClient.Ui.Components.Graphics;
using AlloyClient.Ui.Flash;

namespace AlloyClient.Screens;

public class TitleScreen : TitleScreenBase {

    // Flash TitleView: play is 36pt, secondary options are 22pt.
    public const int PlayFontSize = 36;
    public const int FontSize = 22;

    // Flash TitleView.COPYRIGHT.
    private const string CopyrightText = "© 2010, 2011 by Wild Shadow Studios, Inc.";

    // Flash TitleView version/copyright: 12pt 0x7F7F7F with a default drop shadow.
    private const int FooterFontSize = 12;
    private const uint FooterColor = 0x7F7F7F;

    private readonly SimpleText _versionText;
    private readonly SimpleText _copyrightText;

    public TitleScreen() : base(Components.ScreenType.Title) {
        var editor = new MenuBarButton("editor", FontSize, () => ScreenManager.FadeTo(new EditorScreen()));
        editor.SetAnchor(UiAnchor.MiddleRight);
        MenuBar.AddChild(editor);

        var servers = new MenuBarButton("servers", FontSize, () => ScreenManager.FadeTo(new ServersTitleScreen()));
        servers.SetAnchor(UiAnchor.MiddleRight);
        MenuBar.AddChild(servers);

        var play = new MenuBarButton("play", PlayFontSize, OnPlay, true);
        play.SetAnchor(UiAnchor.Middle);
        MenuBar.AddChild(play);

        servers.X = -play.Width / 2 - MenuGap;
        editor.X = servers.X - servers.Width - MenuGap;

        var legends = new MenuBarButton("legends", FontSize, () => ScreenManager.FadeTo(new LegendsTitleScreen()));
        legends.SetAnchor(UiAnchor.MiddleLeft);
        legends.X = play.Width / 2 + MenuGap;
        MenuBar.AddChild(legends);

        var exit = new MenuBarButton("exit", FontSize, () => Main.OnQuit.Dispatch());
        exit.SetAnchor(UiAnchor.MiddleLeft);
        exit.X = legends.X + legends.Width + MenuGap;
        MenuBar.AddChild(exit);

        // Flash TitleView chrome: version bottom-left, copyright bottom-right.
        _versionText = new SimpleText(new TextConfig {
            Text = $"RotMG {Settings.BuildVersion}",
            FontSize = FooterFontSize,
            FontType = FontType.Normal,
            Color = FooterColor,
            DropShadow = FlashTextFilters.Default,
            X = 0,
            Y = Settings.DefaultScreenHeight
        });
        _versionText.Y = Settings.DefaultScreenHeight - _versionText.Height;
        AddChild(_versionText);

        _copyrightText = new SimpleText(new TextConfig {
            Text = CopyrightText,
            FontSize = FooterFontSize,
            FontType = FontType.Normal,
            Color = FooterColor,
            DropShadow = FlashTextFilters.Default,
            X = Settings.DefaultScreenWidth,
            Y = Settings.DefaultScreenHeight
        });
        _copyrightText.X = Settings.DefaultScreenWidth - _copyrightText.Width;
        _copyrightText.Y = Settings.DefaultScreenHeight - _copyrightText.Height;
        AddChild(_copyrightText);

        CheckForAppFailure();
    }

    // Flash TitleView.layoutChrome: footers scale with the window height and
    // pin to the bottom corners in window pixels.
    protected override void OnResize(ResizeEvent args) {
        base.OnResize(args);

        var scale = Stage.ScreenScale;

        _versionText.Scale = scale;
        _versionText.X = 0;
        _versionText.Y = args.Height - _versionText.Height;

        _copyrightText.Scale = scale;
        _copyrightText.X = args.Width - _copyrightText.Width;
        _copyrightText.Y = args.Height - _copyrightText.Height;
    }

    private void OnPlay() {
        if (GlobalData.Contains<AccountData>()) {
            ScreenManager.FadeTo(new CharacterListScreen());
        } else {
            var login = new LoginContainer();
            login.AddEventListener(LoginContainer.LoginEvent, Overlay.OnLogin);
            OverlayManager.Set(login);
        }
    }

    private void CheckForAppFailure() {
        if (!GlobalData.TryRemove<AppRequestFailedFlag>(out _)) {
            return;
        }

        AddChild(new ScreenDarkenOverlay());

        DialogManager.Enqueue(new RetryLoadDialog());
    }
}