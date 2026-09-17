using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Alloy.Common;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;
using AlloyClient.AppEngine;
using AlloyClient.Assets.Libraries;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Data;
using AlloyClient.Display;
using AlloyClient.Game.Components.Hud.Inventory;
using AlloyClient.Screens.Components;
using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Ui.Components.Scrollbars;
using AlloyClient.Utils;

namespace AlloyClient.Screens;

// Flash reference: realm-client/src/kabam/rotmg/legends/view/LegendsView.as
// (rows: LegendListItem.as, tabs: LegendsTab.as, data: LegendsModel.as,
// GetLegendsListTask.as). Design units are Flash 800x600.
public class LegendsTitleScreen : TitleScreenBase {

    #region Flash Layout Constants

    private const int TitleFontSize = 32;
    private const uint TitleColor = 0xB3B3B3;
    private const int TitleY = 24;

    private const int LoadingFontSize = 22;

    private const int TabFontSize = 20;
    private const int TabX = 20;
    private const int TabY = 70;
    private const int TabStepX = 90;
    private const uint TabOutColor = 0xB2B2B2;
    private const uint TabOverColor = 0xFCCB19;
    private const uint TabSelectedColor = 0xFFFFFF;

    private const int DividerY = 100;
    private const int DividerHeight = 2;
    private const uint DividerColor = 0x545454;

    private const int ListX = 10;
    private const int ListY = 110;
    private const int ListWidth = 756; // LegendListItem.WIDTH
    private const int ClipHeight = 430; // mainContainer mask height
    private const int RowHeight = 56; // LegendListItem.HEIGHT
    private const int VisibleListHeight = 400; // scrollbar window

    private const int ScrollBarWidth = 16; // Scrollbar(16, 400)
    private const int ScrollBarHeight = 400;
    private const int ScrollBarY = 104;
    private const int ScrollBarMargin = 4;

    private const int DoneFontSize = 36; // TitleMenuOption("done", 36, false)

    private const uint OwnLegendColor = 0xFCC219;
    private const uint FirstPlaceColor = 0xFDFF8F;
    private const uint RowTextColor = 0xFFFFFF;

    // LegendListItem row geometry.
    private const int PlaceRightX = 82;
    private const int PortraitX = 104;
    private const int PortraitSize = 44;
    private const int NameX = 170;
    private const int GridX = 400;
    private const int GridY = 8; // HEIGHT / 2 - Slot.HEIGHT / 2
    private const int FameRightX = 660;
    private const int FameIconX = 652;
    private const int FameIconSize = 16;
    private const int RowFontSize = 22;

    // LegendsView/ LegendListItem drop shadows: DropShadowFilter(0, 0, 0, 1, 8, 8).
    private static readonly DropShadowFilter TextShadow = new(distance: 0, angle: 0, blurX: 8, blurY: 8);

    private static readonly Timespan[] Timespans = [
        new Timespan("Week", "week"),
        new Timespan("Month", "month"),
        new Timespan("All Time", "all")
    ];

    #endregion

    public event Action<LegendEntry> LegendSelected;

    private readonly Container _content = new(new ContainerConfig {
        Width = Settings.DefaultScreenWidth,
        Height = Settings.DefaultScreenHeight,
    });

    private readonly SimpleText _loadingBanner;
    private readonly Container _listClip;
    private readonly Container _list;
    private readonly ColorRect _divider;
    private readonly List<LegendsTab> _tabs = [];

    private readonly Dictionary<string, List<LegendEntry>> _cache = new();

    private Timespan _selected = Timespans[0];
    private VerticalScrollBar _scrollBar;
    private int _contentWidth = Settings.DefaultScreenWidth;

    public LegendsTitleScreen() {
        AddChild(_content);

        var title = new SimpleText(new TextConfig {
            Text = "Legends",
            FontSize = TitleFontSize,
            FontType = FontType.Bold,
            Color = TitleColor,
            DropShadow = TextShadow,
            Anchor = UiAnchor.Middle,
        });

        title.X = Settings.DefaultScreenWidth / 2;
        title.Y = TitleY + title.Height / 2;
        _content.AddChild(title);

        _loadingBanner = new SimpleText(new TextConfig {
            Text = "Loading...",
            FontSize = LoadingFontSize,
            FontType = FontType.Bold,
            Color = TitleColor,
            DropShadow = TextShadow,
            Anchor = UiAnchor.Middle,
        });

        _loadingBanner.X = Settings.DefaultScreenWidth / 2;
        _loadingBanner.Y = Settings.DefaultScreenHeight / 2;
        _loadingBanner.Visible = false;
        _content.AddChild(_loadingBanner);

        for (var i = 0; i < Timespans.Length; i++) {
            var tab = new LegendsTab(Timespans[i]) {
                X = TabX + i * TabStepX,
                Y = TabY
            };

            tab.Selected += OnTabSelected;
            _tabs.Add(tab);
            _content.AddChild(tab);
        }

        _divider = new ColorRect(new ColorRectConfig {
            Y = DividerY,
            Width = Settings.DefaultScreenWidth,
            Height = DividerHeight,
            Color = DividerColor,
        });

        _content.AddChild(_divider);

        _listClip = new Container(new ContainerConfig {
            X = ListX,
            Y = ListY,
            Width = ListWidth,
            Height = ClipHeight,
            EnableClip = true
        });

        _content.AddChild(_listClip);

        _list = new Container();
        _listClip.AddChild(_list);

        var doneButton = new MenuBarButton("done", DoneFontSize,
            () => { ScreenManager.FadeToScreen(new TitleScreen(), Easing.SineInOut, 1000, 0x0); });

        doneButton.SetAnchor(UiAnchor.Middle);
        MenuBar.AddChild(doneButton);

        SelectTimespan(Timespans[0]);
    }

    protected override void OnResize(ResizeEvent args) {
        var scale = Stage.ScreenScale;
        _content.Scale = scale;

        _contentWidth = (int)Math.Ceiling(args.Width / scale.X);
        _divider.Resize(_contentWidth, DividerHeight);

        if (_scrollBar is not null) {
            _scrollBar.X = _contentWidth - ScrollBarWidth - ScrollBarMargin;
        }

        base.OnResize(args);
    }

    private void OnTabSelected(LegendsTab tab) {
        if (tab.Timespan.Id == _selected.Id) {
            return;
        }

        SelectTimespan(tab.Timespan);
    }

    private void SelectTimespan(Timespan timespan) {
        _selected = timespan;

        foreach (var tab in _tabs) {
            tab.SetIsSelected(tab.Timespan.Id == timespan.Id);
        }

        if (_cache.TryGetValue(timespan.Id, out var cached)) {
            _loadingBanner.Visible = false;
            RenderList(cached);
            return;
        }

        ClearList();
        _loadingBanner.Visible = true;

        var requestTimespan = timespan;
        AddEventListener(
            AppEngineClient.SendRequest("/fame/list",
                new Dictionary<string, string> { { "timespan", timespan.Id } }, 3),
            response => OnLegendsResponse(requestTimespan, response));
    }

    private void OnLegendsResponse(Timespan timespan, string response) {
        if (timespan.Id != _selected.Id) {
            return;
        }

        _loadingBanner.Visible = false;

        if (string.IsNullOrWhiteSpace(response)) {
            return;
        }

        XElement xml;
        try {
            xml = XElement.Parse(response);
        } catch (Exception) {
            return;
        }

        if (xml.Name.LocalName == "Error") {
            return;
        }

        var ownAccountId = GlobalData.TryGet<AccountData>(out var account) ? account.AccountId : -1;
        var entries = xml.Elements("FameListElem")
            .Select((node, index) => ParseLegend(node, index + 1, ownAccountId))
            .ToList();

        _cache[timespan.Id] = entries;
        RenderList(entries);
    }

    private static LegendEntry ParseLegend(XElement xml, int place, int ownAccountId) {
        var accountId = xml.GetAttribute("accountId", 0);
        var equipment = xml.GetValue("Equipment", "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var id) ? id : -1)
            .ToArray();

        return new LegendEntry(
            accountId,
            xml.GetAttribute("charId", 0),
            xml.GetValue("Name", ""),
            xml.GetValue("TotalFame", 0),
            xml.GetValue<ushort>("ObjectType", 0),
            equipment,
            accountId == ownAccountId);
    }

    private void RenderList(List<LegendEntry> entries) {
        ClearList();

        for (var i = 0; i < entries.Count; i++) {
            var row = new LegendRow(entries[i], i + 1) {
                Y = i * RowHeight
            };

            row.Clicked += OnRowClicked;
            _list.AddChild(row);
        }

        var totalHeight = entries.Count * RowHeight;
        if (totalHeight <= VisibleListHeight) {
            return;
        }

        _scrollBar = new VerticalScrollBar(_listClip, new VerticalScrollBarConfig {
            X = _contentWidth - ScrollBarWidth - ScrollBarMargin,
            Y = ScrollBarY,
            Width = ScrollBarWidth,
            Height = ScrollBarHeight,
            TotalContentHeight = totalHeight,
            VisibleContentHeight = VisibleListHeight,
            OnValueChanged = value => { _list.Y = -value; }
        });

        _content.AddChild(_scrollBar);
    }

    private void OnRowClicked(LegendEntry legend) {
        LegendSelected?.Invoke(legend);
    }

    private void ClearList() {
        _list.RemoveChildren();
        _list.Y = 0;

        if (_scrollBar is null) {
            return;
        }

        _content.RemoveChild(_scrollBar);
        _scrollBar = null;
    }

    private sealed record Timespan(string Name, string Id);

    public sealed record LegendEntry(
        int AccountId,
        int CharId,
        string Name,
        int TotalFame,
        ushort ObjectType,
        int[] Equipment,
        bool IsOwnLegend);

    // Flash reference: LegendsTab.as. Colors: over 0xFCCB19, selected/down
    // white, idle 0xB2B2B2. Bold 20-point label offset by x = 2.
    private sealed class LegendsTab : Container {

        public Timespan Timespan { get; }

        public event Action<LegendsTab> Selected;

        private readonly SimpleText _label;

        private bool _isOver;
        private bool _isDown;
        private bool _isSelected;

        public LegendsTab(Timespan timespan) {
            Timespan = timespan;

            _label = new SimpleText(new TextConfig {
                Text = timespan.Name,
                FontSize = TabFontSize,
                FontType = FontType.Bold,
                Color = TabOutColor,
                X = 2,
            });

            AddChild(_label);
            Resize(_label.Width + 2, _label.Height);
            MouseEnabled = true;

            AddEventListener(MouseEvent.MouseOver, OnMouseOver);
            AddEventListener(MouseEvent.MouseOut, OnMouseOut);
            AddEventListener(MouseEvent.LeftDown, OnLeftDown);
            AddEventListener(MouseEvent.LeftUp, OnLeftUp);
            AddEventListener(MouseEvent.LeftClick, OnClick);
        }

        public void SetIsSelected(bool selected) {
            _isSelected = selected;
            Redraw();
        }

        private void OnMouseOver() {
            _isOver = true;
            Redraw();
        }

        private void OnMouseOut() {
            _isOver = false;
            _isDown = false;
            Redraw();
        }

        private void OnLeftDown() {
            _isDown = true;
            Redraw();
        }

        private void OnLeftUp() {
            _isDown = false;
            Redraw();
        }

        private void OnClick() {
            Selected?.Invoke(this);
        }

        private void Redraw() {
            if (_isOver) {
                _label.SetColor(TabOverColor);
            } else if (_isSelected || _isDown) {
                _label.SetColor(TabSelectedColor);
            } else {
                _label.SetColor(TabOutColor);
            }
        }
    }

    // Flash reference: LegendListItem.as (756x56). Transparent black fill at
    // alpha 0.4 on hover, 0.001 otherwise; click selects the legend.
    private sealed class LegendRow : Container {

        private static readonly ContainerConfig RowConfig = new() {
            Width = ListWidth,
            Height = RowHeight
        };

        public event Action<LegendEntry> Clicked;

        private readonly LegendEntry _legend;
        private readonly ColorRect _background;

        public LegendRow(LegendEntry legend, int place) : base(RowConfig) {
            _legend = legend;

            _background = new ColorRect(new ColorRectConfig {
                Width = ListWidth,
                Height = RowHeight,
                Color = 0x000000,
                Alpha = 0.001f
            });

            AddChild(_background);

            var textColor = legend.IsOwnLegend
                ? OwnLegendColor
                : place == 1 ? FirstPlaceColor : RowTextColor;

            var placeText = new SimpleText(new TextConfig {
                Text = $"{place}.",
                FontSize = RowFontSize,
                FontType = FontType.Bold,
                Color = textColor,
                DropShadow = TextShadow,
            });

            placeText.X = PlaceRightX - placeText.Width;
            placeText.Y = RowHeight / 2 - placeText.Height / 2;
            AddChild(placeText);

            AddPortrait();

            var nameText = new SimpleText(new TextConfig {
                Text = legend.Name,
                FontSize = RowFontSize,
                FontType = FontType.Bold,
                Color = textColor,
                DropShadow = TextShadow,
                X = NameX,
            });

            nameText.Y = RowHeight / 2 - nameText.Height / 2;
            AddChild(nameText);

            var grid = new EquippedGrid(LookupItems(legend.Equipment), LookupSlotTypes(legend.ObjectType)) {
                X = GridX,
                Y = GridY
            };

            AddChild(grid);

            var fameText = new SimpleText(new TextConfig {
                Text = legend.TotalFame.ToString(),
                FontSize = RowFontSize,
                FontType = FontType.Bold,
                Color = textColor,
                DropShadow = TextShadow,
            });

            fameText.X = FameRightX - fameText.Width;
            fameText.Y = RowHeight / 2 - fameText.Height / 2;
            AddChild(fameText);

            var fameIcon = new ObjectRect(new ObjectRectConfig {
                Texture = TextureHelper.FromGameAtlas("lofiObj3", 0xE0),
                Width = FameIconSize,
                Height = FameIconSize,
                X = FameIconX,
                Y = RowHeight / 2 - FameIconSize / 2,
            });

            AddChild(fameIcon);

            MouseEnabled = true;
            AddEventListener(MouseEvent.MouseOver, OnMouseOver);
            AddEventListener(MouseEvent.MouseOut, OnMouseOut);
            AddEventListener(MouseEvent.LeftClick, OnClick);
        }

        private void AddPortrait() {
            if (!ObjectLibrary.TypeToTextureData.TryGetValue(_legend.ObjectType, out var textureData)) {
                return;
            }

            var frames = textureData.AnimatedTextures.FaceDown;
            if (frames == null || frames.Length == 0) {
                return;
            }

            var portrait = new ObjectRect(new ObjectRectConfig {
                Texture = TextureHelper.Create(frames[0], TextureType.GameAtlas),
                Width = PortraitSize,
                Height = PortraitSize,
                X = PortraitX,
                Y = RowHeight / 2 - PortraitSize / 2 - 2,
                OutlineEnabled = false,
                GlowEnabled = false,
            });

            AddChild(portrait);
        }

        private static ItemDesc[] LookupItems(int[] equipment) {
            var items = new ItemDesc[4];
            for (var i = 0; i < items.Length; i++) {
                var id = i < equipment.Length ? equipment[i] : -1;
                items[i] = id > 0 && ObjectLibrary.TypeToItem.TryGetValue((ushort)id, out var desc) ? desc : null;
            }

            return items;
        }

        private static List<int> LookupSlotTypes(ushort objectType) {
            var slotTypes = new List<int>([0, 0, 0, 0]);
            if (ObjectLibrary.TypeToObjectProps.TryGetValue(objectType, out var props) && props.SlotTypes is not null) {
                for (var i = 0; i < slotTypes.Count && i < props.SlotTypes.Count; i++) {
                    slotTypes[i] = props.SlotTypes[i];
                }
            }

            return slotTypes;
        }

        private void OnMouseOver() {
            _background.Alpha = 0.4f;
        }

        private void OnMouseOut() {
            _background.Alpha = 0.001f;
        }

        private void OnClick() {
            Clicked?.Invoke(_legend);
        }
    }
}
