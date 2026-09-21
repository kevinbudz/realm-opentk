using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Display;
using AlloyClient.Game.Components;
using AlloyClient.Game.Components.Hud.Panels;
using AlloyClient.Ui.Components.Buttons;

namespace AlloyClient.Ui.Components.Dialogs;

// Flash parity (com.company.assembleegameclient.account.ui.ChooseNameFrame):
// the NameChangerPanel button opens this dialog instead of typing inline.
// Title, Cancel/Choose actions, an A-Za-z max-10 name field, the three hint
// lines, and inline error text. Submit sends ChooseName via the panel's gated
// path; the NameResult success closes the dialog while a failure re-enables
// Choose and shows the server error (ChooseNameFrame.setError).
public sealed class ChooseNameDialog : UiElement, IDialog {
    private const int BoxWidth = 300;
    private const int BoxPadding = 16;
    private const int TopPadding = 10;
    private const int TextGap = 14;
    private const int HintGap = 2;
    private const int ActionGap = 18;
    private const int BottomPadding = 10;
    private const int ErrorReserve = 20;

    private static readonly string[] Hints = [
        "Maximum 10 characters",
        "No numbers, spaces or punctuation",
        "Racism or profanity gets you banned"
    ];

    public DialogState State { get; set; } = DialogState.Active;

    // Guards the panel against queueing a second copy while one is open.
    public static bool IsOpen { get; private set; }

    private readonly TextInput _input;
    private readonly SimpleText _error;
    private readonly TextButton _chooseButton;
    private readonly TextButton _cancelButton;
    private bool _awaitingResult;

    public ChooseNameDialog() {
        X = Settings.DefaultScreenWidth / 2;
        Y = Settings.DefaultScreenHeight / 2;
        SetAnchor(UiAnchor.Middle);

        var titleText = new SimpleText(new TextConfig {
            Text = "Choose a unique account name",
            FontSize = 20,
            FontType = FontType.Bold,
            Color = 0x78D67A,
            MaxWidth = BoxWidth - BoxPadding * 2,
            Anchor = UiAnchor.MiddleTop
        });

        _input = new TextInput(new InputConfig {
            FontSize = 18,
            FontType = FontType.Normal,
            Width = BoxWidth - BoxPadding * 2,
            MaxCharacters = (byte)NameChangerPanel.MaxNameLength,
            OnFocus = () => UserInput.SetManualFocus(false),
            OnUnfocus = () => UserInput.SetManualFocus(true),
            Anchor = UiAnchor.MiddleTop
        });

        _error = new SimpleText(new TextConfig {
            Text = string.Empty,
            FontSize = 14,
            Color = 0xFC4242,
            MaxWidth = BoxWidth - BoxPadding * 2,
            Anchor = UiAnchor.MiddleTop
        });

        _chooseButton = new TextButton(new TextButtonConfig {
            Text = "Choose",
            FontSize = 22,
            FontType = FontType.Bold,
            OnClicked = Submit,
            Anchor = UiAnchor.MiddleBottom
        });

        _cancelButton = new TextButton(new TextButtonConfig {
            Text = "Cancel",
            FontSize = 22,
            FontType = FontType.Bold,
            OnClicked = () => State = DialogState.Closed,
            Anchor = UiAnchor.MiddleBottom
        });

        var contentY = TopPadding + titleText.Height + TextGap + _input.Height + TextGap;
        foreach (var hint in Hints) {
            var line = new SimpleText(new TextConfig {
                Text = hint,
                FontSize = 14,
                Color = 0xD0D0D0,
                Anchor = UiAnchor.MiddleTop
            });
            line.X = BoxWidth / 2;
            line.Y = contentY;
            contentY += line.Height + HintGap;
        }
        contentY += ErrorReserve;

        var actionHeight = Math.Max(_chooseButton.Height, _cancelButton.Height);
        var boxHeight = contentY + ActionGap + actionHeight + BottomPadding;
        var panel = new Container(new ContainerConfig {
            Width = BoxWidth,
            Height = boxHeight
        });
        AddChild(panel);

        panel.AddChild(new CutEdgeRect(new CutEdgeConfig {
            Width = BoxWidth,
            Height = boxHeight,
            CutX = 7,
            CutY = 7,
            Color = 0xE6E6E6,
            Alpha = 0.9f
        }));
        panel.AddChild(new CutEdgeRect(new CutEdgeConfig {
            X = 1,
            Y = 1,
            Width = BoxWidth - 2,
            Height = boxHeight - 2,
            CutX = 6,
            CutY = 6,
            Color = 0x303030,
            Alpha = 0.98f
        }));

        titleText.X = BoxWidth / 2;
        titleText.Y = TopPadding;
        panel.AddChild(titleText);

        _input.X = BoxWidth / 2;
        _input.Y = TopPadding + titleText.Height + TextGap;
        panel.AddChild(_input);

        var hintY = _input.Y + _input.Height + TextGap;
        foreach (var hint in Hints) {
            var line = new SimpleText(new TextConfig {
                Text = hint,
                FontSize = 14,
                Color = 0xD0D0D0,
                Anchor = UiAnchor.MiddleTop
            });
            line.X = BoxWidth / 2;
            line.Y = hintY;
            panel.AddChild(line);
            hintY += line.Height + HintGap;
        }

        _error.X = BoxWidth / 2;
        _error.Y = hintY;
        panel.AddChild(_error);

        var actionY = boxHeight - BottomPadding;
        _chooseButton.X = BoxWidth * 3 / 4;
        _chooseButton.Y = actionY;
        panel.AddChild(_chooseButton);

        _cancelButton.X = BoxWidth / 4;
        _cancelButton.Y = actionY;
        panel.AddChild(_cancelButton);

        IsOpen = true;
        NameChangerPanel.OnNameResult += HandleNameResult;
        AddEventListener(Event.AddedToStage, () => {
            _input.Focus();
            AddEventListener(Event.EnterFrame, WatchClose);
        });
        AddEventListener(Event.RemovedFromStage, () => RemoveEventListener(Event.EnterFrame, WatchClose));
    }

    private void Submit() {
        if (_awaitingResult)
            return;
        if (!NameChangerPanel.TrySendChooseName(_input.Text)) {
            _error.SetText(NameChangerPanel.LastError ?? "");
            return;
        }
        _awaitingResult = true;
        _error.SetText(string.Empty);
        _chooseButton.Deactivate();
        _cancelButton.Deactivate();
    }

    private void HandleNameResult(bool success) {
        if (!_awaitingResult || State == DialogState.Closed)
            return;
        if (success) {
            State = DialogState.Closed;
            return;
        }
        _awaitingResult = false;
        _error.SetText(NameChangerPanel.LastError ?? "");
        _chooseButton.Activate();
        _cancelButton.Activate();
    }

    private void WatchClose() {
        if (State != DialogState.Closed)
            return;
        RemoveEventListener(Event.EnterFrame, WatchClose);
        NameChangerPanel.OnNameResult -= HandleNameResult;
        IsOpen = false;
    }

    protected override void OnResize(ResizeEvent args) {
        Scale = Stage.ScreenScale;
        X = args.Width / 2;
        Y = args.Height / 2;
    }
}
