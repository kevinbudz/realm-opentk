using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;
using AlloyClient.AppEngine;
using AlloyClient.Assets.Libraries;
using AlloyClient.Data;
using AlloyClient.Display;
using AlloyClient.Game;
using AlloyClient.Screens.Components;
using AlloyClient.Screens.Components.CharacterList;
using AlloyClient.Screens.Components.Containers;
using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Ui.Components.Dialogs;
using AlloyClient.Ui.Components.Graphics;
using AlloyClient.Ui.Flash;
using AlloyClient.Utils;

namespace AlloyClient.Screens;

public class CharacterListScreen : TitleScreenBase {
    private readonly Container _content = new(new ContainerConfig { Width = 800, Height = 600 });
    private readonly Container _selection = new();
    private readonly CurrencyDisplay _currency = new();
    private readonly ColorRect _divider = new(new ColorRectConfig { Y = CharacterSelectionLayout.DividerY, Width = 800, Height = 2, Color = 0x545454 });
    private readonly SimpleText _name;
    private readonly SimpleText _status;
    private ClassContainer _classes;
    private CharacterListScrollbar _scrollbar;
    private bool _busy;
    private bool _removed;
    private int _generation;
    private int _selectedId;

    public CharacterListScreen() {
        MenuBar.Visible = false;
        AddChild(_divider);
        AddChild(_content);
        AddChild(_currency);
        _content.AddChild(_selection);
        _name = SelectionGraphics.Text("", 22, 400, 24, bold: true);
        _content.AddChild(_name);
        _status = SelectionGraphics.Text("", 18, 400, 275);
        _status.SetAnchor(UiAnchor.MiddleTop);
        _content.AddChild(_status);
        AddEventListener(Event.AddedToStage, () => {
            _removed = false;
            // Flash's EnterGameCommand shows this screen with the
            // already-loaded char list; refetch only when data is missing
            // (logout, failed boot) or after a server-state change.
            if (GlobalData.Contains<CharacterListData>()) {
                HideCharacterCreate();
                SetBusy(false);
                Populate();
            } else {
                Refresh();
            }
        });
        AddEventListener(Event.RemovedFromStage, () => { _removed = true; _generation++; });
        Overlay.AddEventListener(AccountOverlay.AccountChangedEvent, Refresh);
        AddEventListener(MouseEvent.ScrollVertical, (MouseEvent e) => {
            if (!_busy && _classes == null) _scrollbar?.Wheel(e);
        });
    }

    protected override void OnResize(ResizeEvent args) {
        base.OnResize(args);
        var scale = Stage.ScreenScale;
        _content.Scale = scale;
        _content.X = (int)MathF.Round(FlashLayout.ContentOffsetX(args.Width, scale.X));
        _divider.Scale = scale;
        _divider.Y = (int)MathF.Round(CharacterSelectionLayout.DividerY * scale.Y);
        _divider.Resize((int)Math.Ceiling(args.Width / scale.X), 2);
        _currency.Scale = scale;
        var currencyPos = CharacterSelectionLayout.CurrencyPosition(args.Width, scale.X, scale.Y);
        _currency.X = currencyPos.X;
        _currency.Y = currencyPos.Y;
    }

    private void Refresh() {
        if (_removed || _busy) return;
        HideCharacterCreate();
        GlobalData.Remove<AppRequestFailedFlag>();
        SetBusy(true, "Loading characters...");
        var generation = ++_generation;
        Observe(AppRequests.GetCharList(), response => {
            if (_removed || generation != _generation) return;
            SetBusy(false);
            if (!response.Success) {
                ShowError(response.Message, Refresh);
                return;
            }
            Populate();
        });
    }

    private void Populate() {
        _selection.RemoveChildren();
        _scrollbar = null;
        var data = GlobalData.Get<CharacterListData>();
        var account = GlobalData.Get<AccountData>();
        if (data == null) return;
        _selectedId = data.Characters.FirstOrDefault()?.Id ?? -1;
        _name.SetText(string.IsNullOrEmpty(account?.Name) ? "Undefined" : account.Name);
        _name.X = (800 - _name.Width) / 2;
        _currency.SetAccount(account);
        _selection.AddChild(SelectionGraphics.Text("Characters", 18, 10, 79, bold: true));
        _selection.AddChild(SelectionGraphics.Text("News", 18, 410, 79, bold: true));
        var rows = new Container();
        var height = CharacterSelectionLayout.ListHeight(data.Characters.Length, data.AvailableSlots);
        var viewport = new Container(new ContainerConfig {
            X = 10, Y = 112, Width = 356, Height = 430, EnableClip = height > 400
        });
        viewport.AddChild(rows);
        _selection.AddChild(viewport);
        var index = 0;
        foreach (var character in data.Characters) {
            rows.AddChild(new CharacterRect(character, () => Play(character.Id), () => Delete(character)) {
                Y = CharacterSelectionLayout.RowY(index++)
            });
        }
        for (var i = 0; i < data.AvailableSlots; i++) {
            rows.AddChild(new CharacterRect(ShowCharacterCreate) { Y = CharacterSelectionLayout.RowY(index++) });
        }
        rows.AddChild(new CharacterRect(data.MaxNumChars, BuySlot) { Y = CharacterSelectionLayout.RowY(index) });
        _selection.AddChild(new NewsList(GlobalData.Get<NewsData>(), OpenNews) { X = 400, Y = 112 });
        _selection.AddChild(new ColorRect(new ColorRectConfig { X = 399, Y = 107, Width = 2, Height = 419, Color = 0x545454 }));
        AddMenuButton("play", 36, 400, () => {
            if (_selectedId < 0) ShowCharacterCreate();
            else Play(_selectedId);
        }, true);
        AddMenuButton("back", 22, 306, () => ScreenManager.FadeToScreen(new TitleScreen(), Easing.SineInOut, 1000, 0));
        AddMenuButton("classes", 22, 496, ShowCharacterCreate);
        if (height > 400) {
            _scrollbar = new CharacterListScrollbar(height, offset => rows.Y = -offset);
            _selection.AddChild(_scrollbar);
        }
    }

    private void AddMenuButton(string text, int size, int centerX, Action action, bool pulse = false) {
        var button = new MenuBarButton(text, size, () => { if (!_busy) action(); }, pulse) { Y = TitleMenuRibbon.MenuCenterY };
        button.SetAnchor(UiAnchor.MiddleLeft);
        button.X = centerX - button.Width / 2;
        _selection.AddChild(button);
    }

    private void Play(int id) {
        if (_busy || _removed) return;
        _busy = true;
        GlobalData.SelectedCharacterId = id;
        ScreenManager.FadeToScreen(new GameScreen(), Easing.SineInOut, 1000, 0);
    }

    public void ShowCharacterCreate() {
        if (_busy || _classes != null || _removed) return;
        _selection.Visible = false;
        _name.Visible = false;
        _classes = new ClassContainer(HideCharacterCreate);
        _content.AddChild(_classes);
    }

    private void HideCharacterCreate() {
        if (_classes != null) {
            _content.RemoveChild(_classes);
            _classes = null;
        }
        _selection.Visible = !_busy;
        _name.Visible = true;
    }

    private void Delete(Character character) {
        if (_busy || _removed) return;
        var name = GlobalData.Get<AccountData>()?.Name;
        var className = ObjectLibrary.TypeToObjectProps.GetValueOrDefault(character.ObjectType)?.DisplayName ?? "Unknown";
        Confirm("Verify Deletion", $"Are you really sure you want to delete {name} the {className}?", "Delete",
            () => Mutate(() => AppRequests.DeleteCharacter(character.Id), "Deleting Character..."));
    }

    private void BuySlot() {
        if (_busy || _removed) return;
        var login = GlobalData.Get<LoginData>();
        if (string.IsNullOrEmpty(login?.Username)) {
            ShowError("Please register or sign in to buy a character slot.");
            return;
        }
        if ((GlobalData.Get<AccountData>()?.Stats.Fame ?? 0) < CharacterSelectionLayout.SlotPrice) {
            ShowError("Not enough fame");
            return;
        }
        Mutate(AppRequests.PurchaseCharSlot, "Purchasing Character Slot...");
    }

    private void Confirm(string title, string message, string action, Action confirmed) {
        SetBusy(true);
        DialogManager.Enqueue(new Dialog(title, message, new DialogOption(action, () => {
            if (_removed) return;
            SetBusy(false);
            confirmed();
        }), new DialogOption("Cancel", () => { if (!_removed) SetBusy(false); })));
    }

    private void Mutate(Func<Task<AppResponse>> request, string message) {
        if (_removed) return;
        SetBusy(true, message);
        Observe(request(), response => {
            if (_removed) return;
            SetBusy(false);
            if (!response.Success) ShowError(response.Message);
            else Refresh();
        });
    }

    private void ShowError(string message, Action retry = null) {
        if (_removed) return;
        SetBusy(true);
        DialogManager.Enqueue(new Dialog("Error", string.IsNullOrWhiteSpace(message) ? "Request failed." : message,
            new DialogOption(retry == null ? "OK" : "Retry", () => {
                if (_removed) return;
                SetBusy(false);
                retry?.Invoke();
            }), retry == null ? null : new DialogOption("back", () => {
                if (!_removed) ScreenManager.SetScreen(new TitleScreen());
            })));
    }

    private void OpenNews(NewsItem item) {
        if (_busy || _removed) return;
        if (item.TryGetFameCharacter(out var id)) {
            ScreenManager.FadeToScreen(new CharacterFameScreen(GlobalData.Get<AccountData>()?.AccountId ?? 0, id), Easing.SineInOut, 1000, 0);
        } else if (item.TryGetWebLink(out var uri)) {
            try {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            } catch (Exception) {
                ShowError("Unable to open the news link.");
            }
        }
    }

    private void SetBusy(bool busy, string message = "") {
        _busy = busy;
        _selection.Visible = !busy && _classes == null;
        _status.SetText(message);
    }

    private void Observe(Task<AppResponse> task, Action<AppResponse> callback) {
        AddEventListener(task, (TaskState state) => callback(task.IsCompletedSuccessfully ? task.Result
            : new AppResponse { Message = "Failed to contact server." }));
    }
}
