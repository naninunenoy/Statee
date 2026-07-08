namespace SuikaGame.Logic;

/// <summary>ゲーム全体の画面フェーズ。</summary>
public enum GamePhase
{
    /// <summary>タイトル画面。開始または終了を選択する。</summary>
    Title,

    /// <summary>プレイ中。盤面が動いている。</summary>
    Playing,

    /// <summary>ポーズ中。盤面は凍結し、やり直すか終了を選択する。</summary>
    Paused,
}
