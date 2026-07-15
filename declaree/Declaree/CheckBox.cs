namespace Declaree;

/// <summary>
/// チェックボックス。押下時の振る舞いは Button と同じくイベント ID(<paramref name="OnToggle"/>)で表す。
/// チェック状態は宣言(<see cref="Checked"/>)が正であり、押下後はホストが状態を変えて再構築する。
/// </summary>
public record CheckBox(string Text, string OnToggle) : UiNode
{
    /// <summary>チェック済みか。</summary>
    public bool Checked { get; init; }
}
