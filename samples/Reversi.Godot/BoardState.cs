using Statee.Core;

namespace Reversi;

/// <summary>
/// 盤面の State 公開(game/board)。8x8 を1行1文字列('.'=空 'B'=黒 'W'=白)で
/// 人間/AI が読める形にする。CaptureState はソケットスレッドで走るため、
/// メインスレッドが差し替える不変スナップショットを読むだけにする(docs/USING.md)。
/// </summary>
[StateeState("game/board")]
public partial class BoardState
{
    private sealed record Snapshot(string[] Rows, int Black, int White);

    private volatile Snapshot _current = new([], 0, 0);

    [StateeField]
    public string[] Rows => _current.Rows;

    [StateeField]
    public int Black => _current.Black;

    [StateeField]
    public int White => _current.White;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(Logic.Board board)
    {
        var rows = new string[Logic.Board.Size];
        for (var y = 0; y < Logic.Board.Size; y++)
        {
            var chars = new char[Logic.Board.Size];
            for (var x = 0; x < Logic.Board.Size; x++)
            {
                chars[x] = board[x, y] switch
                {
                    Logic.Disc.Black => 'B',
                    Logic.Disc.White => 'W',
                    _ => '.',
                };
            }
            rows[y] = new string(chars);
        }
        _current = new Snapshot(rows, board.Count(Logic.Disc.Black), board.Count(Logic.Disc.White));
    }
}
