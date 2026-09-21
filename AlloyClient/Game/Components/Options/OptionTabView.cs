using System;
using System.Collections.Generic;
using AlloyClient.Game.Components.Options.OptionTypes;
using AlloyClient.Ui.Components.Scrollbars;
using Alloy.UiLib.BuiltIn;
using OpenTK.Platform;

namespace AlloyClient.Game.Components.Options;

public class OptionTabView : Container {
    private readonly static string[] OnOffLabels = ["On", "Off"];
    private readonly static object[] OnOffValues = [true, false];
    private readonly static string[] WindowLabels = ["Windowed", "Maximized", "Borderless", "Fullscreen"];
    private readonly static object[] WindowValues = [WindowMode.Normal, WindowMode.Maximized, WindowMode.WindowedFullscreen, WindowMode.ExclusiveFullscreen];
    
    private readonly Container _container;
    private readonly List<Option> _options = [];
    private readonly VerticalScrollBar _scrollbar;
    
    public OptionTabView(string name) : base(new ContainerConfig { Width = OptionsView.PanelWidth, Height = OptionsView.OptionsHeight, EnableClip = true }) {
        _container = new Container();
        AddChild(_container);

        switch (name) {
            case OptionsView.ControlsTab:
                AddControlsOptions();
                break;
            case OptionsView.HotkeysTab:
                AddHotkeysOptions();
                break;
            case OptionsView.ChatTab:
                AddChatOptions();
                break;
            case OptionsView.GraphicsTab:
                AddGraphicsOptions();
                break;
            case OptionsView.SoundTab:
                AddSoundOptions();
                break;
            case OptionsView.ExtraTab:
                AddExtraOptions();
                break;
        }

        if (name == OptionsView.SoundTab)
        {
            PositionSoundChildren();
        }
        else
        {
            PositionChildren();
        }

        // Note: compare against the fixed visible height, not the live
        // Height property. The parent bounds grow with _container content,
        // so _container.Height > Height is never true once content overflows.
        var visibleHeight = OptionsView.OptionsHeight;
        if (_container.Height > visibleHeight)
        {
            _scrollbar = new VerticalScrollBar(this, new VerticalScrollBarConfig {
                X = OptionsView.PanelWidth - 25,
                Y = 5,
                Width = 15,
                Height = visibleHeight - 10,
                TotalContentHeight = _container.Height,
                VisibleContentHeight = visibleHeight,
                OnValueChanged = val => _container.Y = -val
            });
            AddChild(_scrollbar);
        }
    }

    private void PositionSoundChildren()
    {
        var y = 22;
        foreach (var option in _options) {
            if (option == null) {
                y += 48;
                continue;
            }

            option.X = 20;
            option.Y = y;
            _container.AddChild(option);

            y += 48;
        }
    }

    // Two-column flow layout. For single-row options this matches the old
    // index grid exactly. Pinned options always take the left column, and
    // full-width dividers sync both columns to the taller one before taking
    // their own row.
    private void PositionChildren()
    {
        var xCols = new[] { 20, 415 };
        var yCols = new[] { 22, 22 };
        var col = 0;
        foreach (var option in _options)
        {
            if (option == null)
            {
                yCols[col] += 44;
                col ^= 1;
                continue;
            }

            if (option.FullWidth)
            {
                var rowY = Math.Max(yCols[0], yCols[1]);
                option.X += 20;
                option.Y += rowY;

                _container.AddChild(option);

                yCols[0] = yCols[1] = rowY + option.RowHeight;
                col = 0;
                continue;
            }

            var c = option.PinLeft ? 0 : col;
            option.X += xCols[c];
            option.Y += yCols[c];

            _container.AddChild(option);

            yCols[c] += option.RowHeight;
            col = c ^ 1;
        }
    }

    public void Refresh() {
        foreach (var option in _options) {
            option?.Refresh();
        }

        OnAllowRotationChange();
        GetOption(Settings.FpsCap)?.SetDisabled(Settings.VSync);
    }

    private void AddControlsOptions() {
        _options.Add(new KeyMapperOption(Settings.MoveUp, "Move Up", "Key to will move character up"));
        _options.Add(new KeyMapperOption(Settings.MoveLeft, "Move Left", "Key to will move character to the left"));
        _options.Add(new KeyMapperOption(Settings.MoveDown, "Move Down", "Key to will move character down"));
        _options.Add(new KeyMapperOption(Settings.MoveRight, "Move Right", "Key to will move character to the right"));
        _options.Add(new ChoiceOption<bool>(Settings.AllowRotation, OnOffLabels, OnOffValues, "Allow Camera Rotation", "Toggles whether to allow for camera rotation", OnAllowRotationChange));
        _options.Add(null);
        _options.Add(new KeyMapperOption(Settings.RotateLeft, "Rotate Left", "Key to will rotate the camera to the left"));
        _options.Add(new KeyMapperOption(Settings.RotateRight, "Rotate Right", "Key to will rotate the camera to the right"));
        _options.Add(new KeyMapperOption(Settings.Special, "Use Special Ability", "This key will activate your special ability"));
        _options.Add(new KeyMapperOption(Settings.AutoFire, "Autofire Toggle", "This key will toggle autofire"));
        _options.Add(new KeyMapperOption(Settings.ResetCameraAngle, "Reset To Default Camera Angle", "This key will reset the camera angle to the default position"));
        _options.Add(new KeyMapperOption(Settings.PerformanceStats, "Toggle Performance Stats", "This key will toggle a display of fps and memory usage"));
        _options.Add(new KeyMapperOption(Settings.CenterPlayerKey, "Toggle Centering of Player", "This key will toggle the position between centered and offset"));
        _options.Add(new KeyMapperOption(Settings.Interact, "Interact/Buy", "This key will allow you to enter a portal or buy an item without using your mouse."));
    }

    private void AddHotkeysOptions() {
        _options.Add(new KeyMapperOption(Settings.HealthPotion, "Use Health Potion", "This key will use health potions if available"));
        _options.Add(new KeyMapperOption(Settings.MagicPotion, "Use Magic Potion", "This key will use magic potions if available"));
        _options.Add(new KeyMapperOption(Settings.InvOne, "Use Inventory Slot 1", "Use item in inventory slot 1"));
        _options.Add(new KeyMapperOption(Settings.InvTwo, "Use Inventory Slot 2", "Use item in inventory slot 2"));
        _options.Add(new KeyMapperOption(Settings.InvThree, "Use Inventory Slot 3", "Use item in inventory slot 3"));
        _options.Add(new KeyMapperOption(Settings.InvFour, "Use Inventory Slot 4", "Use item in inventory slot 4"));
        _options.Add(new KeyMapperOption(Settings.InvFive, "Use Inventory Slot 5", "Use item in inventory slot 5"));
        _options.Add(new KeyMapperOption(Settings.InvSix, "Use Inventory Slot 6", "Use item in inventory slot 6"));
        _options.Add(new KeyMapperOption(Settings.InvSeven, "Use Inventory Slot 7", "Use item in inventory slot 7"));
        _options.Add(new KeyMapperOption(Settings.InvEight, "Use Inventory Slot 8", "Use item in inventory slot 8"));
        _options.Add(new KeyMapperOption(Settings.MiniMapZoomIn, "Mini-Map Zoom In", "This key will zoom in the minimap"));
        _options.Add(new KeyMapperOption(Settings.MiniMapZoomOut, "Mini-Map Zoom Out", "This key will zoom out the minimap"));
        _options.Add(new KeyMapperOption(Settings.Escape, "Escape To Nexus", "This key will instantly escape you to the Nexus"));
        _options.Add(new KeyMapperOption(Settings.Options, "Show Options", "This key will bring up the options screen"));//TODO: force this to be disabled to prevent changing it
        _options.Add(new KeyMapperOption(Settings.SwitchTabs, "Switch Tabs", "This key will switch from available tabs"));
        _options.Add(new ChoiceOption<bool>(Settings.InventorySwap, OnOffLabels, OnOffValues, "Switch item to/from backpack", "Hold the Ctrl key and click on an item to swap it between your inventory and your backpack."));
        _options.Add(new KeyMapperOption(Settings.ResetMScale, "Reset Map Scale", "Resets your map scale to default."));
        _options.Add(new KeyMapperOption(Settings.SetBagPriority, "Bag priority", "Toggle whether to make bags easier to interact with or not."));
        _options.Add(new KeyMapperOption(Settings.FullscreenKey, "Fullscreen", "Toggles fullscreen mode."));
    }

    private void AddChatOptions() {
        _options.Add(new KeyMapperOption(Settings.Chat, "Activate Chat", "This key will bring up the chat input box"));
        _options.Add(new KeyMapperOption(Settings.ChatCommand, "Start Chat Command",
            "This key will bring up chat with a '/' prepended to allow commands such as /who and /ignore."));
        _options.Add(new KeyMapperOption(Settings.TellKey, "Begin Tell", "This key will bring up a tell in the chat input box"));
        _options.Add(new ChoiceOption<int>(Settings.ChatInclude, ["None", "Guild", "Party", "GP"], [0, 1, 2, 3], "Include In Begin Tell",
            "This key will include the chat in the chat input box"));
        _options.Add(new KeyMapperOption(Settings.GuildChat, "Begin Guild Chat", "This key will bring up a guild chat in the chat input box"));
        _options.Add(new KeyMapperOption(Settings.PartyChat, "Begin Party Chat", "This key will bring up a party chat in the chat input box"));
        _options.Add(new ChoiceOption<bool>(Settings.ChatVisible, OnOffLabels, OnOffValues, "Chat visible", "Turn chat visibility ON/OFF.", OnChatVisible));
        _options.Add(new ChoiceOption<float>(Settings.ChatScaling, ["100%", "90%", "80%", "70%", "60%", "50%"], [1f, 0.9f, 0.8f, 0.7f, 0.6f, 0.5f], "Chat Scale",
            "Click here to change the scale of the chat", OnChatBoxScale));
        _options.Add(new KeyMapperOption(Settings.ChatHistoryUp, "Navigate Chat History Up", "Navigate previous messages"));
        _options.Add(new KeyMapperOption(Settings.ChatHistoryDown, "Navigate Chat History Down", "Navigate next messages"));
        _options.Add(new ChoiceOption<int>(Settings.ChatHideList, ["All", "None", "Locked", "Guild", "Party", "GPL"], [0, 1, 2, 3, 4, 5], "Show Player Messages",
            "Choose which players messages should be shown to you. This includes whispers and global chat."));
    }

    private void AddGraphicsOptions() {
        _options.Add(new ChoiceOption<float>(Settings.DefaultCameraAngle, ["45°", "0°"], [7 * MathF.PI / 4, 0f], "Default Camera Angle", "This toggles the default camera angle", OnDefautCameraAngleChange));
        _options.Add(new ChoiceOption<bool>(Settings.CenterPlayer, OnOffLabels, OnOffValues, "Center On Player", "This toggles whether the player is centered or offset"));
        _options.Add(new ChoiceOption<bool>(Settings.DrawShadows, OnOffLabels, OnOffValues, "Draw Shadows", "This toggles whether to draw shadows"));
        _options.Add(new ChoiceOption<bool>(Settings.ProjectileOutline, OnOffLabels, OnOffValues, "Projectile Outline", "Makes projectiles render with an outline."));
        _options.Add(new ChoiceOption<ParticleMode>(Settings.EyeCandyParticles, ["Off", "Reduced", "On"], [ParticleMode.Off, ParticleMode.Reduced, ParticleMode.On], "Eye Candy Particles", "This toggles eye candy particles. Reduced shows fewer of them; disabling this will improve performance."));
        _options.Add(new ChoiceOption<FullscreenType>(Settings.FullscreenMode, ["Exclusive", "Borderless"], [FullscreenType.Exclusive, FullscreenType.Borderless], "Fullscreen type", "Changes which type fullscreen uses", OnWindowModeChange));
        _options.Add(new ChoiceOption<int>(Settings.FpsCap, ["30", "60", "90", "120", "144", "165", "240", "300", "360", "None"], [30, 60, 90, 120, 144, 165, 240, 300, 360, -1], "FPS Cap", "This allows you to choose a frame rate cap", OnFPSChange));
        _options.Add(new ChoiceOption<int>(Settings.MaxRenderDistance, ["Low", "Medium", "High", "Max"], [15, 20, 25, 60], "Max Render Distance", "Pick the maximum render distance of your client. Can improve performance greatly.", OnRenderDistanceChange));
        _options.Add(new ChoiceOption<bool>(Settings.VSync, OnOffLabels, OnOffValues, "VSync", "This toggles whether to have VSync enabled or not.", OnVSyncToggle));
        _options.Add(new DividerOption());
        _options.Add(new ToggleGroupOption(Settings.AllyInfo,
            [("Shots", Settings.AllyShots), ("Damage", Settings.AllyDamage), ("Notifications", Settings.AllyNotifs)],
            "Ally Information", "Show combat information from other players. Turn this off to hide it all and improve performance."));
        _options.Add(new ToggleSliderOption(Settings.PlayerAlpha, Settings.PlayerAlphaValue,
            "Player Alpha", "Fade other players. Turn this on, then use the slider to change the opacity of other players and everything attached to them."));
    }

    private void AddSoundOptions() {
        _options.Add(new SliderOption(Settings.MasterVolume, "Master Volume", OnMasterVolumeChange));
        _options.Add(new SliderOption(Settings.MusicVolume, "Music Volume", OnMusicVolumeChange));
        _options.Add(new SliderOption(Settings.SfxVolume, "Effects Volume", OnSfxVolumeChange));
    }

    private void AddExtraOptions() {
        _options.Add(new ChoiceOption<bool>(Settings.ShowQuestPortraits, OnOffLabels, OnOffValues, "Show Quest Portraits", "This toggles whether quest portraits are displayed"));
        _options.Add(new ChoiceOption<bool>(Settings.TextBubbles, OnOffLabels, OnOffValues, "Draw Text Bubbles", "This toggles whether to draw text bubbles"));
        _options.Add(new ChoiceOption<bool>(Settings.ShowGuildInvitePopup, OnOffLabels, OnOffValues, "Show Guild Invite Panel", "This toggles whether to show guild invites in the lower-right panel or just in chat."));
        _options.Add(new ChoiceOption<bool>(Settings.ShowTradePopup, OnOffLabels, OnOffValues, "Show Trade Popup", "This toggles whether to show trade requests in a panel or just in chat."));
        _options.Add(new ChoiceOption<bool>(Settings.ShowTierTag, OnOffLabels, OnOffValues, "Show Tier Tag", "This toggles whether to show tier tags on your gear."));
        _options.Add(new ChoiceOption<string>(Settings.Cursor,
            ["OS", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15"],
            ["auto", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15"],
            "Cursor", "Changing this will give you a new mouse cursor."));
    }
    
    private void OnMasterVolumeChange(float obj) {
        Settings.SetMasterVolume(obj);
        Audio.SetMasterVolume(Settings.GetMasterVolume());
    }

    private void OnSfxVolumeChange(float obj) {
        Settings.SetSfxVolume(obj);
        Audio.SfxChannel.SetVolume(Settings.GetSfxVolume());
    }

    private void OnMusicVolumeChange(float obj) {
        Settings.SetMusicVolume(obj);
        Audio.MusicChannel.SetVolume(Settings.GetMusicVolume());
    }

    private void OnChatVisible() {
        GameScreen.RefreshChatOptions();
    }

    private void OnChatBoxScale() {
        GameScreen.RefreshChatOptions();
    }

    private void OnDefautCameraAngleChange() {
        Settings.CameraAngle.Set(Settings.DefaultCameraAngle);
    }

    private void OnVSyncToggle() {
        Main.OnScreenChange.Dispatch(ScreenType.Game);
        var option = GetOption(Settings.FpsCap);
        option.SetDisabled(Settings.VSync);
    }

    private void OnRenderDistanceChange() {
    }

    private void OnFPSChange() {
        Main.OnScreenChange.Dispatch(ScreenType.Game);
    }

    private void OnWindowModeChange() {
        if (Settings.FullscreenState) {
            Main.OnFullscreenToggle.Dispatch();
        }
    }

    private void OnAllowRotationChange() {
        GetOption(Settings.RotateLeft)?.SetDisabled(!Settings.AllowRotation);
        GetOption(Settings.RotateRight)?.SetDisabled(!Settings.AllowRotation);
    }
    
    private Option GetOption(ISettingType setting) {
        return _options.Find(option => option?.Setting == setting);
    }
}
