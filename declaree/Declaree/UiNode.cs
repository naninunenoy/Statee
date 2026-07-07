namespace Declaree;

/// <summary>
/// 宣言的 UI の中間表現(IR)。Godot 非依存のツリーで、
/// UI 定義はこのツリーを返す純粋な C# 式として書く(docs/adr/D-035.md)。
/// </summary>
public abstract record UiNode
{
    /// <summary>可視性。false は Godot の Visible=false に対応する。</summary>
    public bool Visible { get; init; } = true;

    /// <summary>最小幅(px)。Godot の CustomMinimumSize.X に対応する。</summary>
    public int? MinWidth { get; init; }

    /// <summary>最小高さ(px)。Godot の CustomMinimumSize.Y に対応する。</summary>
    public int? MinHeight { get; init; }

    /// <summary>人間向けの説明ヒント(D-032 の Explain)。描画には影響せず、記述子にのみ現れる。</summary>
    public string? Explain { get; init; }
}
