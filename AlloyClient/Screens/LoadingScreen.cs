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

        _text = new SimpleText(new TextConfig {
            Text = "Loading...",
            FontSize = 40,
            FontType = FontType.Bold,
            OutlineThickness = 4,
            Color = 0xFFFFFF,
            Anchor = UiAnchor.Middle
        });

        MenuBar.AddChild(_text);

        AddEventListener(LoadAsync(isRetry), OnLoadComplete);
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
