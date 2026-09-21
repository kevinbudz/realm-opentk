using AlloyClient.Ui.Components.Dialogs;

namespace AlloyClient.Display;

/// <summary>
/// Dialog-queue membership: the view must expose the open/closed state the
/// <see cref="DialogManager"/> pumps. Implemented by <see cref="Dialog"/> and
/// input-capable dialogs such as the choose-name frame.
/// </summary>
public interface IDialog {
    DialogState State { get; set; }
}
