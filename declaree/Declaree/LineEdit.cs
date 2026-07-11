namespace Declaree;

/// <summary>
/// 一行テキスト入力。Declaree はリアクティブな値バインディングを持たない(D-035)ため、
/// 入力後の値はホスト側が Name(D-038)で Godot の LineEdit コントロールを直接参照して読む。
/// </summary>
public record LineEdit(string Text = "") : UiNode
{
    /// <summary>未入力時のプレースホルダ文字列。</summary>
    public string PlaceholderText { get; init; } = "";
}
