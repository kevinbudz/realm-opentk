using Alloy.UiLib.Core;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

//Regression test for the inventory drag-start crash: ItemTile detached the
//dragged icon (which clears its Stage) before StartDrag read Stage.Mouse,
//throwing NullReferenceException the moment any drag began. Starting (or
//ending) a drag on a stage-less sprite must be a safe no-op instead.
internal static class SpriteDragTests {
    public static void Run() {
        DetachedDragStartIsSafeNoOp();
    }

    private static void DetachedDragStartIsSafeNoOp() {
        var stage = new Stage();
        stage.SetSize(new Vector2i(800, 600), Vector2.One);

        var parent = new Sprite();
        stage.AddChild(parent);
        var icon = new Sprite();
        parent.AddChild(icon);
        if (icon.Stage == null)
            throw new Exception("attached icon should have a Stage");

        //This is the ItemTile.OnBeginDrag order that crashed: detach first,
        //which clears the icon's Stage (see RemoveChild).
        parent.RemoveChild(icon);
        if (icon.Stage != null)
            throw new Exception("detached icon should be stage-less");

        icon.StartDrag();
        icon.StartDrag<Sprite>();
        icon.EndDrag();
    }
}
