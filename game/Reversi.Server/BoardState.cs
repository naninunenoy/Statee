using Statee.Core;

namespace Reversi.Server;

/// <summary>
/// 盤面の State 公開(game/board)。Reversi.Godot の BoardState と同一形式
/// (8x8 を1行1文字列)。権威サーバも同じ観測形にすることで、クライアント側と
/// State を素朴に突き合わせられるようにする(N-6 のクロスインスタンス wait の前提)。
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
