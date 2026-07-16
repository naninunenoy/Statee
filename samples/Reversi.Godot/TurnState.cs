using Statee.Core;

namespace Reversi;

/// <summary>
/// 対局進行の State 公開(game/turn)。手番・手数・勝敗・確定手ログを公開する。
/// MoveLog は「そのまま再生可能」規約(D-049)の表記で、
/// ネット対戦(D-050)の確定手ログと同一のものになる。
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
        string EndReason,
        string[] MoveLog
    );

    private volatile Snapshot _current = new(
        "Title",
        "LocalTwoPlayer",
        "None",
        0,
        "None",
        "Complete",
        []
    );

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
    public string EndReason => _current.EndReason;

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
            game.EndReason.ToString(),
            [.. game.MoveLog]
        );
    }
}
