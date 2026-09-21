using System.Collections.Generic;
using AlloyClient.Ui.Components.Dialogs;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;

namespace AlloyClient.Display;

public sealed class DialogManager : Sprite {

    private readonly static Queue<IDialog> Dialogs = [];
    private static IDialog _current;

    public DialogManager() {
        AddEventListener(Event.EnterFrame, OnFrameEnter);
    }

    public static void Enqueue(IDialog dialog) => Dialogs.Enqueue(dialog);

    private void OnFrameEnter() {
        if (_current == null && !TryStart()) return;
        if (_current!.State == DialogState.Closed) OnClosed();
    }

    private bool TryStart() {
        if (!Dialogs.TryDequeue(out var dialog)) return false;

        _current = dialog;
        var view = (Sprite)_current;
        view.Alpha = 0f;
        AddChild(view);
        GTween.Add(Tween.New(view, Easing.SineInOut, 250, 1f, EaseType.Alpha));
        return true;
    }

    private void OnClosed() {
        var view = (Sprite)_current;
        _current.State = DialogState.Finished;
        GTween.Add(Tween.New(view, Easing.SineInOut, 250, 0f, EaseType.Alpha, onFinish: () => {
            RemoveChild(view);
            _current = null;
        }));
    }

}
