using Shouldly;

namespace Reversi.Logic.Tests;

public class BoardTest
{
    /// <summary>文字列から盤を組み立てるテストヘルパ。'.'=None 'B'=Black 'W'=White。</summary>
    internal static Board Parse(params string[] rows)
    {
        rows.Length.ShouldBe(Board.Size, "テスト盤面は8行で書く");
        var board = Board.CreateInitial();
        // 初期配置を消してから流し込むため、内部状態を直接組み立てられる Restore を使う
        var cells = new Disc[Board.Size, Board.Size];
        for (var y = 0; y < Board.Size; y++)
        {
            rows[y].Length.ShouldBe(Board.Size, $"テスト盤面 {y} 行目は8文字で書く");
            for (var x = 0; x < Board.Size; x++)
            {
                cells[x, y] = rows[y][x] switch
                {
                    'B' => Disc.Black,
                    'W' => Disc.White,
                    _ => Disc.None,
                };
            }
        }
        return Board.Restore(cells);
    }

    [Fact]
    public void CreateInitial_中央4マスに黒白2枚ずつ_他は空()
    {
        var board = Board.CreateInitial();

        board[3, 3].ShouldBe(Disc.White);
        board[4, 4].ShouldBe(Disc.White);
        board[3, 4].ShouldBe(Disc.Black);
        board[4, 3].ShouldBe(Disc.Black);
        board.Count(Disc.Black).ShouldBe(2);
        board.Count(Disc.White).ShouldBe(2);
        board.Count(Disc.None).ShouldBe(60);
    }

    [Fact]
    public void GetLegalMoves_初期盤面の黒_定石の4手()
    {
        var board = Board.CreateInitial();

        var moves = board.GetLegalMoves(Disc.Black);

        moves.ShouldBe([(3, 2), (2, 3), (5, 4), (4, 5)]);
    }

    [Fact]
    public void GetLegalMoves_初期盤面の白_4手()
    {
        var board = Board.CreateInitial();

        board.GetLegalMoves(Disc.White).ShouldBe([(4, 2), (5, 3), (2, 4), (3, 5)]);
    }

    [Fact]
    public void TryPlace_初期盤面の黒が2_3へ_縦に挟んだ白1枚が反転する()
    {
        var board = Board.CreateInitial();

        var placed = board.TryPlace(2, 3, Disc.Black);

        placed.ShouldBeTrue();
        board[2, 3].ShouldBe(Disc.Black);
        board[3, 3].ShouldBe(Disc.Black); // 反転した
        board.Count(Disc.Black).ShouldBe(4);
        board.Count(Disc.White).ShouldBe(1);
    }

    [Fact]
    public void TryPlace_非合法手_盤は変わらずfalse()
    {
        var board = Board.CreateInitial();

        board.TryPlace(0, 0, Disc.Black).ShouldBeFalse();
        board.TryPlace(3, 3, Disc.Black).ShouldBeFalse(); // 既に石がある

        board.Count(Disc.Black).ShouldBe(2);
        board.Count(Disc.White).ShouldBe(2);
    }

    [Fact]
    public void TryPlace_全8方向を同時に挟む着手_全方向が反転する()
    {
        // 中央 (3,3) に黒を置くと、8方向すべてで白1枚を挟んで黒に届く盤面
        var board = Parse(
            "........",
            ".B.B.B..",
            "..WWW...",
            ".BW.WB..",
            "..WWW...",
            ".B.B.B..",
            "........",
            "........"
        );

        board.TryPlace(3, 3, Disc.Black).ShouldBeTrue();

        board.Count(Disc.White).ShouldBe(0);
        board.Count(Disc.Black).ShouldBe(6 + 8 + 1);
    }

    [Fact]
    public void TryPlace_挟めない隣接マス_falseで反転しない()
    {
        // 白の隣だが、その先に黒がなく挟めない
        var board = Parse(
            "........",
            "........",
            "........",
            "...W....",
            "........",
            "........",
            "........",
            "........"
        );

        board.TryPlace(2, 3, Disc.Black).ShouldBeFalse();
        board[3, 3].ShouldBe(Disc.White);
    }

    [Fact]
    public void GetLegalMoves_角に置ける局面_角が合法手に含まれる()
    {
        var board = Parse(
            ".WB.....",
            "........",
            "........",
            "........",
            "........",
            "........",
            "........",
            "........"
        );

        board.GetLegalMoves(Disc.Black).ShouldContain((0, 0));
    }
}
