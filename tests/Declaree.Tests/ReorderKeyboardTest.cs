using Shouldly;

namespace Declaree.Tests;

public class ReorderKeyboardTest
{
    [Fact]
    public void Grab_行を掴む_Updatedが発生しドロップ先は掴んだ行()
    {
        var keyboard = new ReorderKeyboard();

        var ev = keyboard.Grab(1, count: 3);

        ev.ShouldBe(new ReorderKeyEvent.Updated(1, 1));
        keyboard.IsGrabbing.ShouldBeTrue();
        keyboard.From.ShouldBe(1);
        keyboard.Drop.ShouldBe(1);
    }

    [Fact]
    public void Grab_掴み中に再度掴む_無視される()
    {
        var keyboard = new ReorderKeyboard();
        keyboard.Grab(1, count: 3);

        var ev = keyboard.Grab(2, count: 3);

        ev.ShouldBeNull();
        keyboard.From.ShouldBe(1);
    }

    [Fact]
    public void Move_下へ動かす_ドロップ先が進みUpdatedが発生する()
    {
        var keyboard = new ReorderKeyboard();
        keyboard.Grab(0, count: 3);

        var ev = keyboard.Move(+1);

        ev.ShouldBe(new ReorderKeyEvent.Updated(0, 1));
        keyboard.Drop.ShouldBe(1);
    }

    [Fact]
    public void Move_末尾からさらに下_クランプされ何も発生しない()
    {
        var keyboard = new ReorderKeyboard();
        keyboard.Grab(2, count: 3);

        var ev = keyboard.Move(+1);

        ev.ShouldBeNull();
        keyboard.Drop.ShouldBe(2);
    }

    [Fact]
    public void Move_先頭からさらに上_クランプされ何も発生しない()
    {
        var keyboard = new ReorderKeyboard();
        keyboard.Grab(0, count: 3);

        var ev = keyboard.Move(-1);

        ev.ShouldBeNull();
    }

    [Fact]
    public void Move_掴んでいない_何も発生しない()
    {
        var keyboard = new ReorderKeyboard();

        var ev = keyboard.Move(+1);

        ev.ShouldBeNull();
        keyboard.IsGrabbing.ShouldBeFalse();
    }

    [Fact]
    public void Commit_移動先が異なる_Committedが発生し掴みが解ける()
    {
        var keyboard = new ReorderKeyboard();
        keyboard.Grab(0, count: 3);
        keyboard.Move(+2);

        var ev = keyboard.Commit();

        ev.ShouldBe(new ReorderKeyEvent.Committed(0, 2));
        keyboard.IsGrabbing.ShouldBeFalse();
        keyboard.From.ShouldBeNull();
        keyboard.Drop.ShouldBeNull();
    }

    [Fact]
    public void Commit_同位置のまま_Canceledになる()
    {
        var keyboard = new ReorderKeyboard();
        keyboard.Grab(1, count: 3);

        var ev = keyboard.Commit();

        ev.ShouldBe(new ReorderKeyEvent.Canceled());
        keyboard.IsGrabbing.ShouldBeFalse();
    }

    [Fact]
    public void Commit_掴んでいない_何も発生しない()
    {
        var keyboard = new ReorderKeyboard();

        keyboard.Commit().ShouldBeNull();
    }

    [Fact]
    public void Cancel_掴み中_Canceledが発生し掴みが解ける()
    {
        var keyboard = new ReorderKeyboard();
        keyboard.Grab(1, count: 3);
        keyboard.Move(+1);

        var ev = keyboard.Cancel();

        ev.ShouldBe(new ReorderKeyEvent.Canceled());
        keyboard.IsGrabbing.ShouldBeFalse();
    }

    [Fact]
    public void Cancel_掴んでいない_何も発生しない()
    {
        var keyboard = new ReorderKeyboard();

        keyboard.Cancel().ShouldBeNull();
    }

    [Fact]
    public void Grab_確定後に掴み直す_新しい掴みが始まる()
    {
        var keyboard = new ReorderKeyboard();
        keyboard.Grab(0, count: 3);
        keyboard.Move(+1);
        keyboard.Commit();

        var ev = keyboard.Grab(2, count: 3);

        ev.ShouldBe(new ReorderKeyEvent.Updated(2, 2));
    }
}
