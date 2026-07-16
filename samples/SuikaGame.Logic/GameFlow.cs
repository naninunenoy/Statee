using R3;

namespace SuikaGame.Logic;

/// <summary>
/// 画面遷移の状態機械。タイトル → プレイの遷移だけを規則として持ち、
/// 実際のシーン差し替え・プロセス終了は Godot 層が Phase を購読して行う。
/// </summary>
public sealed class GameFlow : IDisposable
{
    private readonly ReactiveProperty<GamePhase> _phase = new(GamePhase.Title);

    /// <summary>現在のフェーズ。初期状態はタイトル。</summary>
    public ReadOnlyReactiveProperty<GamePhase> Phase => _phase;

    /// <summary>ゲームを開始する。タイトルにいるときだけ Playing へ遷移し true を返す。</summary>
    public bool StartGame()
    {
        if (_phase.Value != GamePhase.Title)
        {
            return false;
        }

        _phase.Value = GamePhase.Playing;
        return true;
    }

    /// <summary>ポーズする。プレイ中だけ Paused へ遷移し true を返す。</summary>
    public bool PauseGame()
    {
        if (_phase.Value != GamePhase.Playing)
        {
            return false;
        }

        _phase.Value = GamePhase.Paused;
        return true;
    }

    /// <summary>ポーズを解除して続ける。ポーズ中だけ Playing へ遷移し true を返す(盤面はそのまま)。</summary>
    public bool ResumeGame()
    {
        if (_phase.Value != GamePhase.Paused)
        {
            return false;
        }

        _phase.Value = GamePhase.Playing;
        return true;
    }

    /// <summary>やり直す。ポーズ中だけ Playing へ遷移し true を返す。盤面のリセットは呼び出し側の責任。</summary>
    public bool RestartGame()
    {
        if (_phase.Value != GamePhase.Paused)
        {
            return false;
        }

        _phase.Value = GamePhase.Playing;
        return true;
    }

    /// <summary>ゲームオーバーにする。プレイ中だけ GameOver へ遷移し true を返す。</summary>
    public bool EndGame()
    {
        if (_phase.Value != GamePhase.Playing)
        {
            return false;
        }

        _phase.Value = GamePhase.GameOver;
        return true;
    }

    /// <summary>タイトルへ戻る。ゲームオーバー中だけ Title へ遷移し true を返す。盤面のリセットは呼び出し側の責任。</summary>
    public bool GoToTitle()
    {
        if (_phase.Value != GamePhase.GameOver)
        {
            return false;
        }

        _phase.Value = GamePhase.Title;
        return true;
    }

    public void Dispose() => _phase.Dispose();
}
