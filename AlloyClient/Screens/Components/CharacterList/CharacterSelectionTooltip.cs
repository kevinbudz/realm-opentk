using System;
using Alloy.UiLib.BuiltIn;
using AlloyClient.Data;
using AlloyClient.Ui;
using AlloyClient.Ui.Components.Tooltips;
using AlloyClient.Utils;

namespace AlloyClient.Screens.Components.CharacterList;

public sealed class CharacterSelectionTooltip : Tooltip {
    public CharacterSelectionTooltip(Character character, string className, ClassStats stats) : base(192, 322) {
        AddChild(SelectionGraphics.Text(GlobalData.Get<AccountData>()?.Name ?? "", 16, 8, 0, bold: true));
        AddChild(SelectionGraphics.Text($"Level {character.Level} {className}", 12, 8, 21));
        AddMeter("HP", character.HitPoints, character.MaxHitPoints, 40, 0xE03434);
        AddMeter("MP", character.MagicPoints, character.MaxMagicPoints, 64, 0x6084E0);
        for (var i = 0; i < 12; i++) {
            var x = 8 + i % 4 * 44;
            var y = i < 4 ? 88 : 132 + (i - 4) / 4 * 44;
            AddChild(new ColorRect(new ColorRectConfig { X = x, Y = y, Width = 40, Height = 40, Color = 0x545454 }));
            if (i < character.Equipment.Length && character.Equipment[i] >= 0) {
                AddChild(new ObjectRect(new ObjectRectConfig {
                    X = x, Y = y, Width = 40, Height = 40,
                    Texture = TextureHelper.FromGameAtlas((ushort)character.Equipment[i])
                }));
            }
        }
        AddChild(new ColorRect(new ColorRectConfig { X = 6, Y = 228, Width = 180, Height = 2, Color = 0x1C1C1C }));
        AddChild(SelectionGraphics.Text($"{FameUtils.FameToStar(stats?.BestFame ?? 0)} of 5 Class Quests Completed\nBest Level Achieved: {stats?.BestLevel ?? 0}\nBest Fame Achieved: {stats?.BestFame ?? 0}", 14, 8, 232, 0x5EB131));
        var goal = FameUtils.NextStarFame(stats?.BestFame ?? 0, 0);
        if (goal > 0) AddChild(SelectionGraphics.Text($"Next Goal: Earn {goal} Fame\n  with a {className}", 13, 8, 286, 0xFC8542));
        DrawSprite();
    }

    private void AddMeter(string label, int value, int maximum, int y, uint color) {
        AddChild(new ColorRect(new ColorRectConfig { X = 6, Y = y, Width = 176, Height = 16, Color = 0x545454 }));
        AddChild(new ColorRect(new ColorRectConfig {
            X = 6, Y = y, Width = maximum > 0 ? (int)(176 * Math.Clamp(value / (float)maximum, 0, 1)) : 0,
            Height = 16, Color = color
        }));
        AddChild(SelectionGraphics.Text($"{label} {value}/{maximum}", 14, 10, y, 0xFFFFFF, true));
    }
}
