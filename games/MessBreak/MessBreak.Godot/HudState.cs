using Statee.Core;

namespace MessBreak;

/// <summary>
/// UI の見た目の State 公開(ui/hud)。ロジック値の再掲ではなく、実際に画面へ出している
/// Label の文字列・バーの塗り率・各要素の画面上 Rect を写すことで
/// 「ロジック→UI の写し間違い」「配置の破綻」を検出可能にする。
/// Rect は "x,y,w,h"(実ピクセル・整数丸め)の文字列。
/// CaptureState はソケットスレッドで走るため、メインスレッドが差し替える不変スナップショットを
/// 読むだけにする(GameState と同じ作法)。
/// </summary>
[StateeState("ui/hud")]
public partial class HudState
{
    /// <summary>画面に出ている値・実レイアウトのひとまとまり。Main が組んで Update に渡す。</summary>
    public sealed record Snapshot(
        string MissionText,
        string MissionRect,
        string HpText,
        float HpBarRatio,
        string HpBarRect,
        string Char1Text,
        string Char1Rect,
        string Char2Text,
        string Char2Rect,
        string SwitchText,
        string UiBarRect,
        string GameRect,
        bool PauseMenuVisible
    );

    private volatile Snapshot _current = new("", "", "", 0f, "", "", "", "", "", "", "", "", false);

    /// <summary>ゲーム領域左上のミッションガイドに表示中の文字列。</summary>
    [StateeField]
    public string MissionText => _current.MissionText;

    /// <summary>ミッションガイドの画面上 Rect。</summary>
    [StateeField]
    public string MissionRect => _current.MissionRect;

    /// <summary>UI バーの HP 数値表示(例 "HP 10/10")。</summary>
    [StateeField]
    public string HpText => _current.HpText;

    /// <summary>HP バーの塗り率(0〜1)。数値表示との一致を検証できる。</summary>
    [StateeField]
    public float HpBarRatio => _current.HpBarRatio;

    /// <summary>HP バー(背景)の画面上 Rect。</summary>
    [StateeField]
    public string HpBarRect => _current.HpBarRect;

    /// <summary>キャラ枠1(アタッカー)に表示中の文字列(先頭 ▶ がアクティブ印)。</summary>
    [StateeField]
    public string Char1Text => _current.Char1Text;

    /// <summary>キャラ枠1の画面上 Rect。</summary>
    [StateeField]
    public string Char1Rect => _current.Char1Rect;

    /// <summary>キャラ枠2(デバッファー)に表示中の文字列(先頭 ▶ がアクティブ印)。</summary>
    [StateeField]
    public string Char2Text => _current.Char2Text;

    /// <summary>キャラ枠2の画面上 Rect。</summary>
    [StateeField]
    public string Char2Rect => _current.Char2Rect;

    /// <summary>切替クールダウン表示(表示なしは空文字)。</summary>
    [StateeField]
    public string SwitchText => _current.SwitchText;

    /// <summary>下部 UI バー全体の画面上 Rect。</summary>
    [StateeField]
    public string UiBarRect => _current.UiBarRect;

    /// <summary>ゲーム描画領域(UI バーを除いた部分)の Rect。</summary>
    [StateeField]
    public string GameRect => _current.GameRect;

    /// <summary>ポーズメニューが画面に出ているか。</summary>
    [StateeField]
    public bool PauseMenuVisible => _current.PauseMenuVisible;

    /// <summary>メインスレッドから呼ぶ。画面に出ている値・実レイアウトをそのまま渡すこと。</summary>
    public void Update(Snapshot snapshot)
    {
        _current = snapshot;
    }
}
