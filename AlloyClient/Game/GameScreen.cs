using AlloyClient.Display;
using AlloyClient.Game.Components;
using AlloyClient.Networking;
using Alloy.Engine;
using Alloy.UiLib.Core;
using AlloyClient.Game.Components.Hud;
using AlloyClient.Game.Components.Hud.Chat;
using AlloyClient.Rendering;
using AlloyClient.Ui.Character;
using AlloyClient.Ui.Chat;
using AlloyClient.Ui.Components.Elements;
using OpenTK.Mathematics;

namespace AlloyClient.Game;

public sealed class GameScreen : Screen {

    public const double FixedUpdateStep = 1000d / 60d;

    public static GameScreen GameSprite;

    private readonly UserInput _userInput;
    private readonly ChatLayer _chatLayer;
    private readonly NotificationLayer _notificationLayer;
    private readonly HudView _hud;
    private readonly ChatBox _chat;
    private readonly DebugStats _debugStats;

    private double _fixedUpdateElapsed;
    private Camera _camera;

    public GameScreen() {
        Client.Connect(Settings.GameServerAddress, Settings.SelectedGameServerPort);

        AddChild(_userInput = new UserInput()); // add map as param
        AddChild(_chatLayer = new ChatLayer());
        AddChild(_notificationLayer = new NotificationLayer());
        AddChild(_hud = new HudView());
        AddChild(_chat = new ChatBox());
        AddChild(_debugStats = new DebugStats());
        // Flash parity (MapUserInput.togglePerformanceStats): the profiler
        // starts hidden and only shows while toggled on.
        _debugStats.Visible = false;

        GameSprite = this; // TODO: remove this ;-;
    }

    public void CreatePlayerDependentAssets() => _hud.CreatePlayerDependentAssets(); // TODO: remove this ;-;

    public static void RefreshChatOptions() {
        if (GameSprite?.Stage is null) {
            return;
        }

        GameSprite.ApplyChatOptions();
    }

    public static void TogglePerformanceStats() {
        if (GameSprite is null) {
            return;
        }

        GameSprite._debugStats.Visible = !GameSprite._debugStats.Visible;
    }

    public override void Update(GameTime gameTime) {
        Client.Tick();

        if (Map.LocalPlayer is null) {
            return;
        }

        _camera = Camera.Update(Map.LocalPlayer.Position, new Vector3i(Stage.StageWidth, Stage.StageHeight, _hud.Width),
            Settings.CameraAngle, Settings.CameraZoom, Settings.CenterPlayer);

        _userInput.Update(gameTime, _camera);
        _chatLayer.Update(gameTime, _camera);
        _notificationLayer.Update(gameTime, _camera);
        _hud.Update();
        _debugStats.Update(gameTime);

        _fixedUpdateElapsed += gameTime.ElapsedMs;

        while (_fixedUpdateElapsed >= FixedUpdateStep) {
            _fixedUpdateElapsed -= FixedUpdateStep;
            Map.FixedUpdate(new GameTime(gameTime.TotalMs, FixedUpdateStep));
        }

        Map.Update(gameTime, _camera);
        PartyData.Update(gameTime.TotalMs);
    }

    public override void Draw(GameTime gameTime) {
        Render.SetShaderParams(gameTime, _camera);
        Map.Draw(gameTime, _camera);
        MinimapTexture.PreDrawUpdate();
    }

    protected override void OnResize(ResizeEvent args) {
        var width = args.Width;
        var height = args.Height;

        // The Flash HUD is a 200x600 design-space panel anchored to the
        // viewport's top-right edge.  Keep its logical coordinates in the
        // 800x600 design space while placing the scaled panel in window units.
        _hud.Scale = Stage.ScreenScale;
        _hud.X = width - (int)(HudView.HudWidth * Stage.ScreenScale.X);
        _hud.Y = 0;

        _chat.X = 0;
        _chat.Y = height;
        ApplyChatOptions();

        _debugStats.Scale = Stage.ScreenScale;
    }

    private void ApplyChatOptions() {
        var chatScale = Settings.ChatScaling.Value <= 0 ? 1f : Settings.ChatScaling.Value;
        _chat.Visible = Settings.ChatVisible;
        _chat.Scale = Stage.ScreenScale * chatScale;
    }
}
