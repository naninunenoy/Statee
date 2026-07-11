namespace Reversi.Logic;

/// <summary>ゲームモード。CPU 対戦は作らない。</summary>
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

/// <summary>Result に至った理由(D-050 の切断検知)。</summary>
public enum GameEndReason
{
    /// <summary>双方に合法手がなくなった通常の終局。</summary>
    Complete,

    /// <summary>ネット対戦で相手が切断したことによる不戦勝。</summary>
    Disconnected,
}

/// <summary>
/// 対局全体の状態機械。手番管理・自動パス・終局判定・勝敗・着手ログを担う。
/// 着手ログは「そのまま再生可能」規約(D-049)に従い、ネット対戦(D-050)の
/// 確定手ログにもそのまま使える表記にする。
/// </summary>
public sealed class ReversiGame
{
    private readonly List<string> _moveLog = [];

    public GamePhase Phase { get; private set; } = GamePhase.Title;
    public GameMode Mode { get; private set; } = GameMode.LocalTwoPlayer;
    public Board Board { get; private set; } = Board.CreateInitial();

    /// <summary>現在の手番。Playing 以外では None。</summary>
    public Disc CurrentPlayer { get; private set; } = Disc.None;

    /// <summary>着手数(パスを含まない)。</summary>
    public int MoveCount { get; private set; }

    /// <summary>確定した手のログ。例: "place 2 3 black", "pass white"。</summary>
    public IReadOnlyList<string> MoveLog => _moveLog;

    /// <summary>勝者。引き分けは None。Result 以外では None。</summary>
    public Disc Winner { get; private set; } = Disc.None;

    /// <summary>Result に至った理由。Result 以外では意味を持たない。</summary>
    public GameEndReason EndReason { get; private set; } = GameEndReason.Complete;

    /// <summary>Title から対局を開始する。</summary>
    public void Start(GameMode mode)
    {
        Mode = mode;
        Board = Board.CreateInitial();
        CurrentPlayer = Disc.Black;
        MoveCount = 0;
        Winner = Disc.None;
        EndReason = GameEndReason.Complete;
        _moveLog.Clear();
        Phase = GamePhase.Playing;
    }

    /// <summary>
    /// ネット対戦で相手が切断したときに呼ぶ(D-050)。切断した側の相手を勝者として Result へ
    /// 遷移する。Playing 以外では何もしない。
    /// </summary>
    public void EndByDisconnect(Disc disconnectedPlayer)
    {
        if (Phase != GamePhase.Playing)
        {
            return;
        }
        Winner = disconnectedPlayer.Opponent();
        EndReason = GameEndReason.Disconnected;
        CurrentPlayer = Disc.None;
        Phase = GamePhase.Result;
    }

    /// <summary>任意の局面から Playing 状態で復元する(テスト・将来の途中復帰用)。</summary>
    public static ReversiGame Restore(Board board, Disc currentPlayer, GameMode mode) =>
        new()
        {
            Board = board,
            CurrentPlayer = currentPlayer,
            Mode = mode,
            Phase = GamePhase.Playing,
        };

    /// <summary>
    /// 現在の手番で着手する。非合法手・Playing 以外では盤を変えず false。
    /// 着手後、相手に合法手がなければ自動パス(ログに記録)、
    /// 双方に合法手がなければ終局して Result へ遷移する。
    /// </summary>
    public bool TryPlace(int x, int y)
    {
        if (Phase != GamePhase.Playing)
        {
            return false;
        }
        var player = CurrentPlayer;
        if (!Board.TryPlace(x, y, player))
        {
            // 手番側に合法手が1つもない異常局面(Restore 由来)はここで終局を検出する
            if (
                Board.GetLegalMoves(player).Count == 0
                && Board.GetLegalMoves(player.Opponent()).Count == 0
            )
            {
                Finish();
            }
            return false;
        }
        MoveCount++;
        _moveLog.Add($"place {x} {y} {Name(player)}");
        AdvanceTurn(player);
        return true;
    }

    /// <summary>Result からタイトルへ戻る。</summary>
    public void BackToTitle()
    {
        if (Phase != GamePhase.Result)
        {
            return;
        }
        Phase = GamePhase.Title;
        CurrentPlayer = Disc.None;
    }

    /// <summary>着手後の手番決定。相手 → (パスして)自分 → 双方不可なら終局。</summary>
    private void AdvanceTurn(Disc mover)
    {
        var opponent = mover.Opponent();
        if (Board.GetLegalMoves(opponent).Count > 0)
        {
            CurrentPlayer = opponent;
            return;
        }
        if (Board.GetLegalMoves(mover).Count > 0)
        {
            _moveLog.Add($"pass {Name(opponent)}");
            CurrentPlayer = mover;
            return;
        }
        Finish();
    }

    private void Finish()
    {
        var black = Board.Count(Disc.Black);
        var white = Board.Count(Disc.White);
        Winner =
            black > white ? Disc.Black
            : white > black ? Disc.White
            : Disc.None;
        CurrentPlayer = Disc.None;
        Phase = GamePhase.Result;
    }

    private static string Name(Disc disc) => disc == Disc.Black ? "black" : "white";
}
