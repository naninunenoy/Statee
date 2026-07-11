namespace Reversi.Logic;

/// <summary>
/// 8x8 のリバーシ盤。合法手の列挙と着手(反転)だけを担い、
/// 手番管理・パス・終局はもたない(ReversiGame の仕事)。
/// </summary>
public sealed class Board
{
    public const int Size = 8;

    private static readonly (int Dx, int Dy)[] Directions =
    [
        (-1, -1),
        (0, -1),
        (1, -1),
        (-1, 0),
        (1, 0),
        (-1, 1),
        (0, 1),
        (1, 1),
    ];

    private readonly Disc[,] _cells;

    private Board(Disc[,] cells)
    {
        _cells = cells;
    }

    /// <summary>初期配置(中央4マスに黒白2枚ずつ)の盤を作る。</summary>
    public static Board CreateInitial()
    {
        var cells = new Disc[Size, Size];
        cells[3, 3] = Disc.White;
        cells[4, 4] = Disc.White;
        cells[4, 3] = Disc.Black;
        cells[3, 4] = Disc.Black;
        return new Board(cells);
    }

    /// <summary>任意の盤面から復元する(テスト・将来の途中復帰用)。配列はコピーされる。</summary>
    public static Board Restore(Disc[,] cells)
    {
        if (cells.GetLength(0) != Size || cells.GetLength(1) != Size)
        {
            throw new ArgumentException($"盤面は {Size}x{Size} で指定する", nameof(cells));
        }
        return new Board((Disc[,])cells.Clone());
    }

    /// <summary>マスの状態。範囲外は例外。</summary>
    public Disc this[int x, int y] => _cells[x, y];

    /// <summary>player が着手できるマスを列挙する(順序は y 行 → x 列の昇順)。</summary>
    public IReadOnlyList<(int X, int Y)> GetLegalMoves(Disc player)
    {
        var moves = new List<(int X, int Y)>();
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (CollectFlips(x, y, player).Count > 0)
                {
                    moves.Add((x, y));
                }
            }
        }
        return moves;
    }

    /// <summary>合法手なら石を置いて挟んだ石を全て反転し true。非合法手なら盤を変えず false。</summary>
    public bool TryPlace(int x, int y, Disc player)
    {
        var flips = CollectFlips(x, y, player);
        if (flips.Count == 0)
        {
            return false;
        }
        _cells[x, y] = player;
        foreach (var (fx, fy) in flips)
        {
            _cells[fx, fy] = player;
        }
        return true;
    }

    /// <summary>指定の石の数を数える。</summary>
    public int Count(Disc disc)
    {
        var count = 0;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (_cells[x, y] == disc)
                {
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>(x,y) に player が置いたとき反転する石を全方向分集める。空でなければ合法手。</summary>
    private List<(int X, int Y)> CollectFlips(int x, int y, Disc player)
    {
        var flips = new List<(int X, int Y)>();
        if (player == Disc.None || !IsInside(x, y) || _cells[x, y] != Disc.None)
        {
            return flips;
        }
        var opponent = player.Opponent();
        foreach (var (dx, dy) in Directions)
        {
            var line = new List<(int X, int Y)>();
            var (cx, cy) = (x + dx, y + dy);
            while (IsInside(cx, cy) && _cells[cx, cy] == opponent)
            {
                line.Add((cx, cy));
                cx += dx;
                cy += dy;
            }
            if (line.Count > 0 && IsInside(cx, cy) && _cells[cx, cy] == player)
            {
                flips.AddRange(line);
            }
        }
        return flips;
    }

    private static bool IsInside(int x, int y) => x is >= 0 and < Size && y is >= 0 and < Size;
}
