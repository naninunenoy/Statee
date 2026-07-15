namespace TodoApp.Logic;

/// <summary>
/// TODO アプリの中核状態機械。タスクの追加・完了トグル・編集・並び替え・フィルタ・
/// 削除確認ダイアログ・文字サイズを、Godot 非依存の純C#で持つ(docs/USING.md「境界の掟」)。
/// 乱数・時刻に依存しない完全決定論。
/// 削除確認ダイアログが開いている間(<see cref="IsDialogOpen"/>)は、
/// ConfirmDelete / CancelDelete 以外の変更操作をすべて拒否する
/// (モーダル中は背面 UI に触れない、という仕様を UI 層任せにせずロジックでも保証する)。
/// </summary>
public sealed class TodoLogic
{
    public const int MinFontSize = 12;
    public const int MaxFontSize = 32;
    public const int DefaultFontSize = 16;

    private readonly List<TodoItem> _items = [];
    private int _nextId = 1;

    /// <summary>全タスク(表示順)。</summary>
    public IReadOnlyList<TodoItem> Items => _items;

    /// <summary>現在のフィルタを適用した表示対象タスク。</summary>
    public IReadOnlyList<TodoItem> VisibleItems =>
        Filter switch
        {
            TodoFilter.Active => [.. _items.Where(x => !x.Completed)],
            TodoFilter.Completed => [.. _items.Where(x => x.Completed)],
            _ => _items,
        };

    /// <summary>現在の表示フィルタ。</summary>
    public TodoFilter Filter { get; private set; } = TodoFilter.All;

    /// <summary>編集フォームを開いているタスクの Id。編集中でなければ null。</summary>
    public int? EditingId { get; private set; }

    /// <summary>削除確認ダイアログの対象タスクの Id。ダイアログが閉じていれば null。</summary>
    public int? PendingDeleteId { get; private set; }

    /// <summary>削除確認ダイアログが開いているか。</summary>
    public bool IsDialogOpen => PendingDeleteId is not null;

    /// <summary>UI 全体の文字サイズ(px)。スライダーで変更する。</summary>
    public int FontSize { get; private set; } = DefaultFontSize;

    /// <summary>
    /// タスクを末尾に追加する。タイトルは前後空白をトリムし、空なら追加せず null を返す。
    /// 追加できたら新しいタスクの Id を返す。
    /// </summary>
    public int? Add(string title)
    {
        var trimmed = title.Trim();
        if (IsDialogOpen || trimmed.Length == 0)
        {
            return null;
        }
        var item = new TodoItem(_nextId++, trimmed, Completed: false);
        _items.Add(item);
        return item.Id;
    }

    /// <summary>完了状態を反転する。Id が存在しなければ false。</summary>
    public bool Toggle(int id)
    {
        if (IsDialogOpen || IndexOf(id) is not { } index)
        {
            return false;
        }
        _items[index] = _items[index] with { Completed = !_items[index].Completed };
        return true;
    }

    /// <summary>表示フィルタを変更する。ダイアログ中は無視して false。</summary>
    public bool SetFilter(TodoFilter filter)
    {
        if (IsDialogOpen)
        {
            return false;
        }
        Filter = filter;
        return true;
    }

    /// <summary>
    /// タスクを Items 内の位置 toIndex(0 始まり)へ移動する(ドラッグ並び替えの着地点)。
    /// Id が存在しない・toIndex が範囲外なら false。
    /// </summary>
    public bool Move(int id, int toIndex)
    {
        if (
            IsDialogOpen
            || toIndex < 0
            || toIndex >= _items.Count
            || IndexOf(id) is not { } fromIndex
        )
        {
            return false;
        }
        var item = _items[fromIndex];
        _items.RemoveAt(fromIndex);
        _items.Insert(toIndex, item);
        return true;
    }

    /// <summary>編集フォームを開く。Id が存在しなければ false。</summary>
    public bool BeginEdit(int id)
    {
        if (IsDialogOpen || IndexOf(id) is null)
        {
            return false;
        }
        EditingId = id;
        return true;
    }

    /// <summary>
    /// 編集中のタイトルを確定する。トリム後に空なら確定せず false(編集は継続)。
    /// </summary>
    public bool CommitEdit(string newTitle)
    {
        var trimmed = newTitle.Trim();
        if (IsDialogOpen || trimmed.Length == 0 || EditingId is not { } id)
        {
            return false;
        }
        // BeginEdit 済みのタスクはダイアログを経ない限り消えないが、防御的に存在を確認する
        if (IndexOf(id) is not { } index)
        {
            EditingId = null;
            return false;
        }
        _items[index] = _items[index] with { Title = trimmed };
        EditingId = null;
        return true;
    }

    /// <summary>編集フォームを閉じる(変更破棄)。</summary>
    public void CancelEdit()
    {
        if (IsDialogOpen)
        {
            return;
        }
        EditingId = null;
    }

    /// <summary>削除確認ダイアログを開く。Id が存在しなければ false。</summary>
    public bool RequestDelete(int id)
    {
        if (IsDialogOpen || IndexOf(id) is null)
        {
            return false;
        }
        PendingDeleteId = id;
        return true;
    }

    /// <summary>ダイアログの対象タスクを削除してダイアログを閉じる。ダイアログが開いていなければ false。</summary>
    public bool ConfirmDelete()
    {
        if (PendingDeleteId is not { } id)
        {
            return false;
        }
        if (IndexOf(id) is { } index)
        {
            _items.RemoveAt(index);
        }
        if (EditingId == id)
        {
            EditingId = null;
        }
        PendingDeleteId = null;
        return true;
    }

    /// <summary>削除をやめてダイアログを閉じる。</summary>
    public void CancelDelete()
    {
        PendingDeleteId = null;
    }

    /// <summary>文字サイズを設定する。Min/Max へクランプする。</summary>
    public void SetFontSize(int size)
    {
        if (IsDialogOpen)
        {
            return;
        }
        FontSize = Math.Clamp(size, MinFontSize, MaxFontSize);
    }

    private int? IndexOf(int id)
    {
        var index = _items.FindIndex(x => x.Id == id);
        return index < 0 ? null : index;
    }
}
