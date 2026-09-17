using System;
using System.Threading.Tasks;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;
using AlloyClient.AppEngine;
using AlloyClient.Assets;
using AlloyClient.Data;
using AlloyClient.Display;
using AlloyClient.Screens.Components;
using AlloyClient.Ui.Components.Dialogs;
using AlloyClient.Ui.Components.Graphics;
using AlloyClient.Ui.Flash;

namespace AlloyClient.Screens;

public class LoadingScreen : TitleScreenBase {

    private const int MinLoadingTime = 2000;

    private readonly SimpleText _text;
    private bool _routeStarted;
    private bool _isAttached = true;

    public LoadingScreen(bool isRetry = false) : base(Components.ScreenType.Loading) {
        if (isRetry) {
            // AppEngineClient records transport failures globally and refuses
            // new requests while the flag is present. A retry owns the point
            // at which that failure is consumed.
            GlobalData.TryRemove<AppRequestFailedFlag>(out _);
        }

        // Flash LoadingScreen.as: bold 30-point white text with a
        // DropShadowFilter(0, 0, 0, 1, 4, 4), top edge at y = 526 and
        // horizontally centered via LayoutHelper.centerX.
        _text = new SimpleText(new TextConfig {
            Text = "Loading...",
            FontSize = 30,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            DropShadow = FlashTextFilters.Default,
            Anchor = UiAnchor.Middle
        });

        CenterText();
        MenuBar.AddChild(_text);

        AddEventListener(LoadAsync(isRetry), OnLoadComplete);
    }

    // Flash LoadingScreen.setText: replaces the message and re-centers it.
    // The Middle anchor keeps the text horizontally centered; the Y offset
    // keeps its top edge on the Flash y = 526 line in design units.
    public void SetText(string value) {
        _text.SetText(value);
        CenterText();
    }

    private void CenterText() {
        const int flashTextY = 526;
        _text.Y = flashTextY + _text.Height / 2 - TitleMenuRibbon.MenuCenterY;
    }

    private static async Task<AppResponse> LoadAsync(bool isRetry) {
        var startup = AppRequests.Startup();
        var assets = isRetry ? Task.CompletedTask : AssetParser.LoadAssetsAsync();

        try {
            await Task.WhenAll(startup, assets, Task.Delay(MinLoadingTime));
        } catch (Exception) {
            return new AppResponse { Success = false, Message = "Failed to load the game." };
        }

        return await startup;
    }

    private void OnLoadComplete(AppResponse response) {
        if (!_isAttached || _routeStarted) {
            return;
        }

        _routeStarted = true;
        if (!response.Success) {
            if (!GlobalData.Contains<AppRequestFailedFlag>()) {
                GlobalData.Add(new AppRequestFailedFlag(string.IsNullOrWhiteSpace(response.Message) ? "Failed to load the game." : response.Message));
            }

            AddChild(new ScreenDarkenOverlay());
            DialogManager.Enqueue(new RetryLoadDialog());
            return;
        }

        ScreenManager.FadeToScreen(new TitleScreen(), Easing.SineInOut, 1000, 0x0);
    }

    protected override void OnRemovedFromStage() {
        _isAttached = false;
        base.OnRemovedFromStage();
    }
}
