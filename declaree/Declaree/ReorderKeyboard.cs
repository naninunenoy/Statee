namespace Declaree;

/// <summary>
/// <see cref="ReorderList"/> のキーボード/ゲームパッド並び替え(掴みモード)の状態機械。
/// 「掴む → 上下でドロップ先を移動 → 確定 / 中止」の判定を Godot 非依存で持ち、
/// 結果をマウスドラッグと同じ3イベント(update / commit / cancel。D-062)に写像する。
/// 入力デバイスからの呼び出しは Godot 層(ReorderListContainer)が行う。
/// </summary>
public sealed class ReorderKeyboard
{
    /// <summary>掴み中の行(移動元)。掴んでいなければ null。</summary>
    public int? From => throw new NotImplementedException();

    /// <summary>現在のドロップ先。掴んでいなければ null。</summary>
    public int? Drop => throw new NotImplementedException();

    /// <summary>掴み中かどうか。</summary>
    public bool IsGrabbing => throw new NotImplementedException();

    /// <summary>行 <paramref name="index"/> を掴む(行数 <paramref name="count"/>)。</summary>
    public ReorderKeyEvent? Grab(int index, int count) => throw new NotImplementedException();

    /// <summary>ドロップ先を <paramref name="delta"/> 行ぶん動かす(範囲内にクランプ)。</summary>
    public ReorderKeyEvent? Move(int delta) => throw new NotImplementedException();

    /// <summary>掴みを確定する。移動元と同位置なら cancel になる。</summary>
    public ReorderKeyEvent? Commit() => throw new NotImplementedException();

    /// <summary>掴みを中止する。</summary>
    public ReorderKeyEvent? Cancel() => throw new NotImplementedException();
}

/// <summary>掴みモードの操作が生むイベント。D-062 の3イベントに1対1対応する。</summary>
public abstract record ReorderKeyEvent
{
    /// <summary>ドラッグ開始・ドロップ先の変化(= "{id}:update:{from}:{to}")。</summary>
    public sealed record Updated(int From, int To) : ReorderKeyEvent;

    /// <summary>確定(= "{id}:commit:{from}:{to}")。</summary>
    public sealed record Committed(int From, int To) : ReorderKeyEvent;

    /// <summary>不成立(= "{id}:cancel")。</summary>
    public sealed record Canceled : ReorderKeyEvent;
}
