namespace Reversi.Logic;

/// <summary>
/// 8x8 のリバーシ盤。合法手の列挙と着手(反転)だけを担い、
/// 手番管理・パス・終局はもたない(ReversiGame の仕事)。
/// </summary>
public sealed class Board
{
    public const int Size = 8;

    /// <summary>初期配置(中央4マスに黒白2枚ずつ)の盤を作る。</summary>
    public static Board CreateInitial() => throw new NotImplementedException();

    /// <summary>任意の盤面から復元する(テスト・将来の途中復帰用)。配列はコピーされる。</summary>
    public static Board Restore(Disc[,] cells) => throw new NotImplementedException();

    /// <summary>マスの状態。範囲外は例外。</summary>
    public Disc this[int x, int y] => throw new NotImplementedException();

    /// <summary>player が着手できるマスを列挙する(順序は y 行 → x 列の昇順)。</summary>
    public IReadOnlyList<(int X, int Y)> GetLegalMoves(Disc player) =>
        throw new NotImplementedException();

    /// <summary>合法手なら石を置いて挟んだ石を全て反転し true。非合法手なら盤を変えず false。</summary>
    public bool TryPlace(int x, int y, Disc player) => throw new NotImplementedException();

    /// <summary>指定の石の数を数える。</summary>
    public int Count(Disc disc) => throw new NotImplementedException();
}
