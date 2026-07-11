namespace Reversi.Logic;

/// <summary>ゲームモード。CPU 対戦は作らない(REVERSI_ROADMAP.md)。</summary>
public enum GameMode
{
    /// <summary>1画面で2人が交互に着手する。</summary>
    LocalTwoPlayer,

    /// <summary>ネット対戦(D-050)。ロジック層はコマンドの供給元を区別しない。</summary>
    Network,
}

/// <summary>対局フロー(スイカゲームの GamePhase 相当)。</summary>
public enum GamePhase
{
    Title,
    Playing,
    Result,
}

/// <summary>
/// 対局全体の状態機械。手番管理・自動パス・終局判定・勝敗・着手ログを担う。
/// 着手ログは「そのまま再生可能」規約(D-049)に従い、ネット対戦(D-050)の
/// 確定手ログにもそのまま使える表記にする。
/// </summary>
public sealed class ReversiGame
{
    public GamePhase Phase => throw new NotImplementedException();
    public GameMode Mode => throw new NotImplementedException();
    public Board Board => throw new NotImplementedException();

    /// <summary>現在の手番。Playing 以外では None。</summary>
    public Disc CurrentPlayer => throw new NotImplementedException();

    /// <summary>着手数(パスを含まない)。</summary>
    public int MoveCount => throw new NotImplementedException();

    /// <summary>確定した手のログ。例: "place 2 3 black", "pass white"。</summary>
    public IReadOnlyList<string> MoveLog => throw new NotImplementedException();

    /// <summary>勝者。引き分けは None。Result 以外では None。</summary>
    public Disc Winner => throw new NotImplementedException();

    /// <summary>Title から対局を開始する。</summary>
    public void Start(GameMode mode) => throw new NotImplementedException();

    /// <summary>
    /// 現在の手番で着手する。非合法手・Playing 以外では盤を変えず false。
    /// 着手後、相手に合法手がなければ自動パス(ログに記録)、
    /// 双方に合法手がなければ終局して Result へ遷移する。
    /// </summary>
    public bool TryPlace(int x, int y) => throw new NotImplementedException();

    /// <summary>Result からタイトルへ戻る。</summary>
    public void BackToTitle() => throw new NotImplementedException();
}
