namespace Declaree;

/// <summary>
/// ドラッグで並び替えできる縦リスト。イベントは3種類で、いずれも
/// <see cref="OnReorder"/> を接頭辞にした文字列として dispatch する
/// (イベント ID は文字列であり、パラメータを埋め込んでもシリアライズ可能性(D-035)を保つ):
/// <c>"{OnReorder}:update:{from}:{to}"</c>(ドラッグ開始・ドロップ先変化)/
/// <c>"{OnReorder}:commit:{from}:{to}"</c>(確定)/ <c>"{OnReorder}:cancel"</c>(不成立)。
/// ドラッグ中の見た目は宣言が正: ホストが update を受けて
/// <see cref="DraggingIndex"/> / <see cref="DropIndex"/> を設定し直すと、
/// 移動中の行が半透明・ドロップ先の行がハイライトになる(D-062)。
/// ドラッグにならない単純クリックは子(Button 等)へそのまま届く。
/// </summary>
public record ReorderList(string OnReorder, params UiNode[] Children) : UiNode
{
    /// <summary>ドラッグ中(移動中)の行のインデックス。ドラッグしていなければ null。</summary>
    public int? DraggingIndex { get; init; }

    /// <summary>現在のドロップ先の行のインデックス。ドラッグしていなければ null。</summary>
    public int? DropIndex { get; init; }
}
