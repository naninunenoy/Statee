namespace Declaree;

/// <summary>
/// ドラッグで並び替えできる縦リスト。子の上で押下し、上下に動かして離すと
/// <c>"{OnReorder}:{fromIndex}:{toIndex}"</c> 形式のイベントを dispatch する
/// (イベント ID は文字列であり、パラメータを埋め込んでもシリアライズ可能性(D-035)を保つ)。
/// ドラッグにならない単純クリックは子(Button 等)へそのまま届く。
/// </summary>
public record ReorderList(string OnReorder, params UiNode[] Children) : UiNode;
