using System.Collections.Generic;
using Statee.Core;

namespace SuikaGame;

/// <summary>
/// UI 幾何・テキストの State 公開(GUIDELINE §7-2, D-031 スライス②)。
/// 「画面内に収まっている」「表示されている」を幾何述語で検証可能にする。
/// CaptureState はソケットスレッドで走るため(D-019)、
/// メインスレッドが毎物理フレーム差し替える不変スナップショットを読むだけにする。
/// </summary>
[StateeState("game/ui")]
public partial class UiState
{
    /// <summary>UI 要素1個。Id はノード名由来でフレームを跨いで安定(GUIDELINE 3.4)。</summary>
    public sealed record ElementEntry(
        string Id,
        string Text,
        float X,
        float Y,
        float Width,
        float Height,
        bool Visible,
        bool Interactable
    );

    private sealed record Snapshot(
        float ViewportWidth,
        float ViewportHeight,
        IReadOnlyList<ElementEntry> Elements
    );

    private volatile Snapshot _current = new(0f, 0f, []);

    /// <summary>画面(ビューポート)の幅。「画面内に収まるか」の述語はこれを基準にする。
    /// headless では窓が無いため小さい値になる点に注意。</summary>
    [StateeField]
    public float ViewportWidth => _current.ViewportWidth;

    [StateeField]
    public float ViewportHeight => _current.ViewportHeight;

    [StateeField]
    public IReadOnlyList<ElementEntry> Elements => _current.Elements;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(
        float viewportWidth,
        float viewportHeight,
        IReadOnlyList<ElementEntry> elements
    )
    {
        _current = new Snapshot(viewportWidth, viewportHeight, elements);
    }
}
