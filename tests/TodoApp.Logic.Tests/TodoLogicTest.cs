using Shouldly;

namespace TodoApp.Logic.Tests;

public class TodoLogicTest
{
    private static TodoLogic CreateWithItems(params string[] titles)
    {
        var logic = new TodoLogic();
        foreach (var title in titles)
        {
            logic.Add(title);
        }
        return logic;
    }

    // ---- Add ----

    [Fact]
    public void Add_通常のタイトル_末尾に追加されIdを返す()
    {
        var logic = CreateWithItems("既存");

        var id = logic.Add("新規");

        id.ShouldNotBeNull();
        logic.Items[^1].ShouldBe(new TodoItem(id.Value, "新規", Completed: false));
    }

    [Fact]
    public void Add_前後に空白があるタイトル_トリムして追加される()
    {
        var logic = new TodoLogic();

        logic.Add("  牛乳を買う  ");

        logic.Items[^1].Title.ShouldBe("牛乳を買う");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_空または空白のみ_追加されずnullを返す(string title)
    {
        var logic = new TodoLogic();

        var id = logic.Add(title);

        id.ShouldBeNull();
        logic.Items.ShouldBeEmpty();
    }

    [Fact]
    public void Add_複数回_Idは一意()
    {
        var logic = CreateWithItems("a", "b", "c");

        logic.Items.Select(x => x.Id).ShouldBeUnique();
    }

    [Fact]
    public void Add_ダイアログ中_追加されずnullを返す()
    {
        var logic = CreateWithItems("a");
        logic.RequestDelete(logic.Items[0].Id);

        var id = logic.Add("b");

        id.ShouldBeNull();
        logic.Items.Count.ShouldBe(1);
    }

    // ---- Toggle ----

    [Fact]
    public void Toggle_未完了のタスク_完了になる()
    {
        var logic = CreateWithItems("a");

        var ok = logic.Toggle(logic.Items[0].Id);

        ok.ShouldBeTrue();
        logic.Items[0].Completed.ShouldBeTrue();
    }

    [Fact]
    public void Toggle_完了済みのタスク_未完了に戻る()
    {
        var logic = CreateWithItems("a");
        logic.Toggle(logic.Items[0].Id);

        logic.Toggle(logic.Items[0].Id);

        logic.Items[0].Completed.ShouldBeFalse();
    }

    [Fact]
    public void Toggle_存在しないId_falseを返す()
    {
        var logic = CreateWithItems("a");

        logic.Toggle(999).ShouldBeFalse();
    }

    [Fact]
    public void Toggle_ダイアログ中_変更されずfalseを返す()
    {
        var logic = CreateWithItems("a");
        logic.RequestDelete(logic.Items[0].Id);

        var ok = logic.Toggle(logic.Items[0].Id);

        ok.ShouldBeFalse();
        logic.Items[0].Completed.ShouldBeFalse();
    }

    // ---- Filter / VisibleItems ----

    [Fact]
    public void VisibleItems_初期状態_全件が見える()
    {
        var logic = CreateWithItems("a", "b");

        logic.Filter.ShouldBe(TodoFilter.All);
        logic.VisibleItems.Count.ShouldBe(2);
    }

    [Fact]
    public void SetFilter_Active_未完了のみが見える()
    {
        var logic = CreateWithItems("a", "b", "c");
        logic.Toggle(logic.Items[1].Id);

        logic.SetFilter(TodoFilter.Active);

        logic.VisibleItems.Select(x => x.Title).ShouldBe(["a", "c"]);
    }

    [Fact]
    public void SetFilter_Completed_完了のみが見える()
    {
        var logic = CreateWithItems("a", "b", "c");
        logic.Toggle(logic.Items[1].Id);

        logic.SetFilter(TodoFilter.Completed);

        logic.VisibleItems.Select(x => x.Title).ShouldBe(["b"]);
    }

    [Fact]
    public void SetFilter_ダイアログ中_変わらずfalseを返す()
    {
        var logic = CreateWithItems("a");
        logic.RequestDelete(logic.Items[0].Id);

        var ok = logic.SetFilter(TodoFilter.Completed);

        ok.ShouldBeFalse();
        logic.Filter.ShouldBe(TodoFilter.All);
    }

    // ---- Move ----

    [Fact]
    public void Move_先頭のタスクを末尾へ_順序が変わる()
    {
        var logic = CreateWithItems("a", "b", "c");

        var ok = logic.Move(logic.Items[0].Id, 2);

        ok.ShouldBeTrue();
        logic.Items.Select(x => x.Title).ShouldBe(["b", "c", "a"]);
    }

    [Fact]
    public void Move_末尾のタスクを先頭へ_順序が変わる()
    {
        var logic = CreateWithItems("a", "b", "c");

        logic.Move(logic.Items[2].Id, 0);

        logic.Items.Select(x => x.Title).ShouldBe(["c", "a", "b"]);
    }

    [Fact]
    public void Move_存在しないId_falseで順序は変わらない()
    {
        var logic = CreateWithItems("a", "b");

        var ok = logic.Move(999, 0);

        ok.ShouldBeFalse();
        logic.Items.Select(x => x.Title).ShouldBe(["a", "b"]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Move_範囲外のIndex_falseで順序は変わらない(int toIndex)
    {
        var logic = CreateWithItems("a", "b");

        var ok = logic.Move(logic.Items[0].Id, toIndex);

        ok.ShouldBeFalse();
        logic.Items.Select(x => x.Title).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Move_ダイアログ中_falseで順序は変わらない()
    {
        var logic = CreateWithItems("a", "b");
        logic.RequestDelete(logic.Items[0].Id);

        var ok = logic.Move(logic.Items[0].Id, 1);

        ok.ShouldBeFalse();
        logic.Items.Select(x => x.Title).ShouldBe(["a", "b"]);
    }

    // ---- 編集フォーム ----

    [Fact]
    public void BeginEdit_存在するId_EditingIdが設定される()
    {
        var logic = CreateWithItems("a");

        var ok = logic.BeginEdit(logic.Items[0].Id);

        ok.ShouldBeTrue();
        logic.EditingId.ShouldBe(logic.Items[0].Id);
    }

    [Fact]
    public void BeginEdit_存在しないId_falseを返す()
    {
        var logic = CreateWithItems("a");

        logic.BeginEdit(999).ShouldBeFalse();
        logic.EditingId.ShouldBeNull();
    }

    [Fact]
    public void BeginEdit_ダイアログ中_falseを返す()
    {
        var logic = CreateWithItems("a");
        logic.RequestDelete(logic.Items[0].Id);

        logic.BeginEdit(logic.Items[0].Id).ShouldBeFalse();
        logic.EditingId.ShouldBeNull();
    }

    [Fact]
    public void CommitEdit_新タイトル_タイトルが更新され編集が終わる()
    {
        var logic = CreateWithItems("a");
        logic.BeginEdit(logic.Items[0].Id);

        var ok = logic.CommitEdit("  a改  ");

        ok.ShouldBeTrue();
        logic.Items[0].Title.ShouldBe("a改");
        logic.EditingId.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void CommitEdit_空白のみ_falseでタイトル不変のまま編集が継続する(string newTitle)
    {
        var logic = CreateWithItems("a");
        logic.BeginEdit(logic.Items[0].Id);

        var ok = logic.CommitEdit(newTitle);

        ok.ShouldBeFalse();
        logic.Items[0].Title.ShouldBe("a");
        logic.EditingId.ShouldBe(logic.Items[0].Id);
    }

    [Fact]
    public void CommitEdit_編集中でない_falseを返す()
    {
        var logic = CreateWithItems("a");

        logic.CommitEdit("b").ShouldBeFalse();
        logic.Items[0].Title.ShouldBe("a");
    }

    [Fact]
    public void CancelEdit_編集中_破棄して編集が終わる()
    {
        var logic = CreateWithItems("a");
        logic.BeginEdit(logic.Items[0].Id);

        logic.CancelEdit();

        logic.EditingId.ShouldBeNull();
        logic.Items[0].Title.ShouldBe("a");
    }

    // ---- 削除確認ダイアログ ----

    [Fact]
    public void RequestDelete_存在するId_ダイアログが開く()
    {
        var logic = CreateWithItems("a");

        var ok = logic.RequestDelete(logic.Items[0].Id);

        ok.ShouldBeTrue();
        logic.IsDialogOpen.ShouldBeTrue();
        logic.PendingDeleteId.ShouldBe(logic.Items[0].Id);
    }

    [Fact]
    public void RequestDelete_存在しないId_ダイアログは開かずfalseを返す()
    {
        var logic = CreateWithItems("a");

        logic.RequestDelete(999).ShouldBeFalse();
        logic.IsDialogOpen.ShouldBeFalse();
    }

    [Fact]
    public void RequestDelete_ダイアログ中に別のId_falseで対象は変わらない()
    {
        var logic = CreateWithItems("a", "b");
        logic.RequestDelete(logic.Items[0].Id);

        var ok = logic.RequestDelete(logic.Items[1].Id);

        ok.ShouldBeFalse();
        logic.PendingDeleteId.ShouldBe(logic.Items[0].Id);
    }

    [Fact]
    public void ConfirmDelete_ダイアログ中_対象が削除されダイアログが閉じる()
    {
        var logic = CreateWithItems("a", "b");
        logic.RequestDelete(logic.Items[0].Id);

        var ok = logic.ConfirmDelete();

        ok.ShouldBeTrue();
        logic.Items.Select(x => x.Title).ShouldBe(["b"]);
        logic.IsDialogOpen.ShouldBeFalse();
    }

    [Fact]
    public void ConfirmDelete_ダイアログが開いていない_falseを返す()
    {
        var logic = CreateWithItems("a");

        logic.ConfirmDelete().ShouldBeFalse();
        logic.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void ConfirmDelete_編集中のタスクを削除_編集も終わる()
    {
        var logic = CreateWithItems("a");
        logic.BeginEdit(logic.Items[0].Id);
        logic.RequestDelete(logic.Items[0].Id);

        logic.ConfirmDelete();

        logic.EditingId.ShouldBeNull();
        logic.Items.ShouldBeEmpty();
    }

    [Fact]
    public void CancelDelete_ダイアログ中_削除されずダイアログが閉じる()
    {
        var logic = CreateWithItems("a");
        logic.RequestDelete(logic.Items[0].Id);

        logic.CancelDelete();

        logic.IsDialogOpen.ShouldBeFalse();
        logic.Items.Count.ShouldBe(1);
    }

    // ---- 文字サイズ ----

    [Fact]
    public void FontSize_初期状態_既定値()
    {
        new TodoLogic().FontSize.ShouldBe(TodoLogic.DefaultFontSize);
    }

    [Theory]
    [InlineData(TodoLogic.MinFontSize, TodoLogic.MinFontSize)]
    [InlineData(20, 20)]
    [InlineData(TodoLogic.MaxFontSize, TodoLogic.MaxFontSize)]
    [InlineData(TodoLogic.MinFontSize - 1, TodoLogic.MinFontSize)]
    [InlineData(0, TodoLogic.MinFontSize)]
    [InlineData(TodoLogic.MaxFontSize + 1, TodoLogic.MaxFontSize)]
    [InlineData(999, TodoLogic.MaxFontSize)]
    public void SetFontSize_指定値_MinMaxへクランプされる(int size, int expected)
    {
        var logic = new TodoLogic();

        logic.SetFontSize(size);

        logic.FontSize.ShouldBe(expected);
    }

    [Fact]
    public void SetFontSize_ダイアログ中_変わらない()
    {
        var logic = CreateWithItems("a");
        logic.RequestDelete(logic.Items[0].Id);

        logic.SetFontSize(TodoLogic.MaxFontSize);

        logic.FontSize.ShouldBe(TodoLogic.DefaultFontSize);
    }
}
