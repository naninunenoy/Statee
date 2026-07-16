using System.Linq;
using Statee.Core;
using TodoApp.Logic;

namespace TodoApp;

/// <summary>
/// TODO アプリの State 公開(game/todoapp)。タスクは1件1文字列
/// 「Id:done|todo:タイトル」で人間/AI が読める形にする(タイトルは末尾なので ':' を含んでよい)。
/// CaptureState はソケットスレッドで走るため、メインスレッドが差し替える
/// 不変スナップショットを読むだけにする(docs/USING.md)。
/// </summary>
[StateeState("game/todoapp")]
public partial class GameState
{
    private sealed record Snapshot(
        string[] Tasks,
        string[] VisibleTasks,
        string Filter,
        int EditingId,
        int PendingDeleteId,
        bool IsDialogOpen,
        int FontSize
    );

    private volatile Snapshot _current = new([], [], nameof(TodoFilter.All), 0, 0, false, 0);

    [StateeField]
    public string[] Tasks => _current.Tasks;

    [StateeField]
    public string[] VisibleTasks => _current.VisibleTasks;

    [StateeField]
    public string Filter => _current.Filter;

    /// <summary>編集フォームを開いているタスクの Id。編集中でなければ 0。</summary>
    [StateeField]
    public int EditingId => _current.EditingId;

    /// <summary>削除確認ダイアログの対象タスクの Id。ダイアログが閉じていれば 0。</summary>
    [StateeField]
    public int PendingDeleteId => _current.PendingDeleteId;

    [StateeField]
    public bool IsDialogOpen => _current.IsDialogOpen;

    [StateeField]
    public int FontSize => _current.FontSize;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(TodoLogic logic)
    {
        _current = new Snapshot(
            [.. logic.Items.Select(Format)],
            [.. logic.VisibleItems.Select(Format)],
            logic.Filter.ToString(),
            logic.EditingId ?? 0,
            logic.PendingDeleteId ?? 0,
            logic.IsDialogOpen,
            logic.FontSize
        );
    }

    private static string Format(TodoItem item) =>
        $"{item.Id}:{(item.Completed ? "done" : "todo")}:{item.Title}";
}
