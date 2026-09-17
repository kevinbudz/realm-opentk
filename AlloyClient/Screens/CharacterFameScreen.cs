using System;
using System.Collections.Generic;
using System.Linq;
using Alloy.Common;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;
using AlloyClient.AppEngine;
using AlloyClient.Assets.Libraries;
using AlloyClient.Display;
using AlloyClient.Screens.Components;
using AlloyClient.Screens.Components.CharacterList;
using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Ui.Components.Graphics;
using AlloyClient.Ui.Flash;
using AlloyClient.Utils;

namespace AlloyClient.Screens;

public sealed class CharacterFameScreen : TitleScreenBase {
    private readonly Container _content = new(new ContainerConfig { Width = 800, Height = 600 });
    private bool _removed;

    public CharacterFameScreen(int accountId, int characterId) {
        MenuBar.Visible = false;
        Overlay.Visible = false;
        AddChild(_content);
        var loading = SelectionGraphics.Text("Loading...", 24, 400, 275);
        loading.SetAnchor(UiAnchor.MiddleTop);
        _content.AddChild(loading);
        var back = new MenuBarButton("continue", 36, () => ScreenManager.FadeToScreen(new CharacterListScreen(), Easing.SineInOut, 1000, 0)) { Y = TitleMenuRibbon.MenuCenterY };
        back.SetAnchor(UiAnchor.MiddleLeft);
        back.X = (800 - back.Width) / 2;
        _content.AddChild(back);
        AddEventListener(Event.RemovedFromStage, () => _removed = true);
        var task = AppRequests.GetCharacterFame(accountId, characterId);
        AddEventListener(task, (TaskState state) => {
            if (_removed) return;
            if (!task.IsCompletedSuccessfully || task.Result.Name.LocalName != "Fame" || task.Result.Element("Char") == null) {
                loading.SetText(task.IsCompletedSuccessfully ? task.Result.Value : "Failed to load character fame.");
                return;
            }
            _content.RemoveChild(loading);
            var xml = task.Result;
            var character = xml.Element("Char");
            var type = character.GetValue<ushort>("ObjectType");
            var className = ObjectLibrary.TypeToObjectProps.GetValueOrDefault(type)?.DisplayName ?? "Unknown";
            Center($"{character.Element("Account")?.GetValue("Name", "")}, Level {character.GetValue("Level", 0)} {className}", 38, 225);
            var killedOn = DateTimeOffset.FromUnixTimeSeconds(xml.GetValue("KilledOn", 0)).LocalDateTime.ToString("MMMM d, yyyy");
            var killer = xml.GetValue("KilledBy", "");
            Center(string.IsNullOrEmpty(killer) ? $"died {killedOn}" : $"killed on {killedOn} by {killer}", 24, 272);
            var portrait = new ObjectRect(new ObjectRectConfig {
                X = 300, Y = 20, Width = 200, Height = 200,
                Texture = TextureHelper.FromGameAtlas(character.GetValue<ushort>("Texture", 0) is > 0 and var skin ? skin : type)
            });
            _content.AddChild(portrait);
            var clip = new Container(new ContainerConfig { X = 8, Y = 316, Width = 784, Height = 150, EnableClip = true });
            var scores = new Container();
            clip.AddChild(scores);
            _content.AddChild(clip);
            AddScore(scores, "Base Fame", xml.GetValue("BaseFame", 0), 0, 18);
            var index = 1;
            foreach (var bonus in xml.Elements("Bonus")) {
                AddScore(scores, bonus.GetAttribute("id", ""), int.TryParse(bonus.Value, out var value) ? value : 0, index++ * 26, 18);
            }
            var overflow = Math.Max(0, index * 26 - 150);
            clip.AddEventListener(MouseEvent.ScrollVertical, (MouseEvent e) => scores.Y = Math.Clamp(scores.Y + (int)(e.VerticalDelta * 26), -overflow, 0));
            var total = new Container { X = 10, Y = 470 };
            AddScore(total, "Total Fame Earned", xml.GetValue("TotalFame", 0), 0, 24);
            _content.AddChild(total);
        });
    }

    private void Center(string text, int size, int y) {
        var label = SelectionGraphics.Text(text, size, 0, y, 0xCCCCCC, true);
        label.DropShadow = FlashTextFilters.Soft;
        label.X = (800 - label.Width) / 2;
        _content.AddChild(label);
    }

    private static void AddScore(Container parent, string title, int value, int y, int size) {
        parent.AddChild(SelectionGraphics.Text(title, size, 0, y, 0xCCCCCC));
        var score = SelectionGraphics.Text(value.ToString(), size, 0, y, 0xFFC800);
        score.X = 750 - score.Width;
        parent.AddChild(score);
    }

    protected override void OnResize(ResizeEvent args) {
        base.OnResize(args);
        _content.Scale = Stage.ScreenScale;
        _content.X = (int)MathF.Round(FlashLayout.ContentOffsetX(args.Width, Stage.ScreenScale.X));
    }
}
