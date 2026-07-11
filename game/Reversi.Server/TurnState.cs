using Statee.Core;

namespace Reversi.Server;

/// <summary>
/// 対局進行の State 公開(game/turn)。Reversi.Godot の TurnState と同一形式。
/// MoveLog はクライアントの確定手ログと突き合わせられるよう「そのまま再生可能」規約
/// (D-049)の表記を維持する。
/// </summary>
[StateeState("game/turn")]
public partial class TurnState
{
    private sealed record Snapshot(
        string Phase,
        string Mode,
        string CurrentPlayer,
        int MoveCount,
        string Winner,
        string[] MoveLog
    );

    private volatile Snapshot _current = new("Title", "Network", "None", 0, "None", []);

    [StateeField]
    public string Phase => _current.Phase;

    [StateeField]
    public string Mode => _current.Mode;

    [StateeField]
    public string CurrentPlayer => _current.CurrentPlayer;

    [StateeField]
    public int MoveCount => _current.MoveCount;

    [StateeField]
    public string Winner => _current.Winner;

    [StateeField]
    public string[] MoveLog => _current.MoveLog;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(Logic.ReversiGame game)
    {
        _current = new Snapshot(
            game.Phase.ToString(),
            game.Mode.ToString(),
            game.CurrentPlayer.ToString(),
            game.MoveCount,
            game.Winner.ToString(),
            [.. game.MoveLog]
        );
    }
}
