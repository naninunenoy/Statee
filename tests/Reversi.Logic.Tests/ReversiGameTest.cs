using Shouldly;

namespace Reversi.Logic.Tests;

public class ReversiGameTest
{
    [Fact]
    public void 初期状態_Titleで手番なし()
    {
        var game = new ReversiGame();

        game.Phase.ShouldBe(GamePhase.Title);
        game.CurrentPlayer.ShouldBe(Disc.None);
    }

    [Fact]
    public void Start_Playingへ遷移し黒番から始まる()
    {
        var game = new ReversiGame();

        game.Start(GameMode.LocalTwoPlayer);

        game.Phase.ShouldBe(GamePhase.Playing);
        game.Mode.ShouldBe(GameMode.LocalTwoPlayer);
        game.CurrentPlayer.ShouldBe(Disc.Black);
        game.MoveCount.ShouldBe(0);
    }

    [Fact]
    public void TryPlace_合法手で手番が交代しログに残る()
    {
        var game = new ReversiGame();
        game.Start(GameMode.LocalTwoPlayer);

        game.TryPlace(2, 3).ShouldBeTrue();

        game.CurrentPlayer.ShouldBe(Disc.White);
        game.MoveCount.ShouldBe(1);
        game.MoveLog.ShouldBe(["place 2 3 black"]);
    }

    [Fact]
    public void TryPlace_非合法手_手番は変わらずfalse()
    {
        var game = new ReversiGame();
        game.Start(GameMode.LocalTwoPlayer);

        game.TryPlace(0, 0).ShouldBeFalse();

        game.CurrentPlayer.ShouldBe(Disc.Black);
        game.MoveCount.ShouldBe(0);
        game.MoveLog.ShouldBeEmpty();
    }

    [Fact]
    public void TryPlace_Title中は着手できない()
    {
        var game = new ReversiGame();

        game.TryPlace(2, 3).ShouldBeFalse();
    }

    [Fact]
    public void 相手に合法手がない_自動パスがログに残り手番が戻る()
    {
        // 黒が (1,0) に置くと白は残り1枚(白の手の終端に使える白石がない)で合法手がなくなり、
        // 白の自動パスで黒番が続く。黒には (3,4) など合法手が残る局面
        var game = ReversiGame.Restore(
            BoardTest.Parse(
                "..WB....",
                "...B....",
                "...B....",
                "...W....",
                "........",
                "........",
                "........",
                "........"
            ),
            currentPlayer: Disc.Black,
            mode: GameMode.LocalTwoPlayer
        );

        game.TryPlace(1, 0).ShouldBeTrue();

        game.Phase.ShouldBe(GamePhase.Playing);
        game.CurrentPlayer.ShouldBe(Disc.Black);
        game.MoveLog.ShouldBe(["place 1 0 black", "pass white"]);
    }

    [Fact]
    public void 双方に合法手がない_終局してResultへ遷移し勝敗が出る()
    {
        // 黒が (7,0) を打つと白が全滅し、双方打てなくなって終局する局面
        var game = ReversiGame.Restore(
            BoardTest.Parse(
                "BBBBBW..",
                "........",
                "........",
                "........",
                "........",
                "........",
                "........",
                "........"
            ),
            currentPlayer: Disc.Black,
            mode: GameMode.LocalTwoPlayer
        );

        game.TryPlace(6, 0).ShouldBeTrue();

        game.Phase.ShouldBe(GamePhase.Result);
        game.Winner.ShouldBe(Disc.Black);
        game.CurrentPlayer.ShouldBe(Disc.None);
    }

    [Fact]
    public void 引き分け_WinnerはNone()
    {
        // 上半分が黒32・下半分が白32 で埋まった終局盤面から復元
        var game = ReversiGame.Restore(
            BoardTest.Parse(
                "BBBBBBBB",
                "BBBBBBBB",
                "BBBBBBBB",
                "BBBBBBBB",
                "WWWWWWWW",
                "WWWWWWWW",
                "WWWWWWWW",
                "WWWWWWWW"
            ),
            currentPlayer: Disc.Black,
            mode: GameMode.LocalTwoPlayer
        );

        // 盤が埋まっており黒に合法手がない → Restore 時点で終局扱いにはしない設計のため、
        // 着手を試みることで終局判定を踏ませる
        game.TryPlace(0, 0).ShouldBeFalse();
        game.Phase.ShouldBe(GamePhase.Result);
        game.Winner.ShouldBe(Disc.None);
    }

    [Fact]
    public void 完走_常に最初の合法手を打ち続けると必ず終局する()
    {
        var game = new ReversiGame();
        game.Start(GameMode.LocalTwoPlayer);

        var guard = 0;
        while (game.Phase == GamePhase.Playing)
        {
            var moves = game.Board.GetLegalMoves(game.CurrentPlayer);
            moves.ShouldNotBeEmpty("Playing 中の手番には必ず合法手がある(なければ自動処理される)");
            game.TryPlace(moves[0].X, moves[0].Y).ShouldBeTrue();
            (++guard).ShouldBeLessThanOrEqualTo(60, "60手以内に必ず終局する");
        }

        game.Phase.ShouldBe(GamePhase.Result);
        (
            game.Board.Count(Disc.Black)
            + game.Board.Count(Disc.White)
            + game.Board.Count(Disc.None)
        ).ShouldBe(64);
        game.MoveLog.Count.ShouldBeGreaterThanOrEqualTo(game.MoveCount);
    }

    [Fact]
    public void BackToTitle_ResultからTitleへ戻る()
    {
        var game = ReversiGame.Restore(
            BoardTest.Parse(
                "BBBBBW..",
                "........",
                "........",
                "........",
                "........",
                "........",
                "........",
                "........"
            ),
            currentPlayer: Disc.Black,
            mode: GameMode.LocalTwoPlayer
        );
        game.TryPlace(6, 0);
        game.Phase.ShouldBe(GamePhase.Result);

        game.BackToTitle();

        game.Phase.ShouldBe(GamePhase.Title);
    }

    [Fact]
    public void EndByDisconnect_対局中に相手が切断_切断した側の相手が勝ちResultへ遷移する()
    {
        var game = new ReversiGame();
        game.Start(GameMode.Network);
        game.TryPlace(2, 3);

        game.EndByDisconnect(Disc.White);

        game.Phase.ShouldBe(GamePhase.Result);
        game.Winner.ShouldBe(Disc.Black);
        game.CurrentPlayer.ShouldBe(Disc.None);
        game.EndReason.ShouldBe(GameEndReason.Disconnected);
    }

    [Fact]
    public void EndReason_通常の終局はComplete()
    {
        var game = ReversiGame.Restore(
            BoardTest.Parse(
                "BBBBBW..",
                "........",
                "........",
                "........",
                "........",
                "........",
                "........",
                "........"
            ),
            currentPlayer: Disc.Black,
            mode: GameMode.LocalTwoPlayer
        );

        game.TryPlace(6, 0);

        game.EndReason.ShouldBe(GameEndReason.Complete);
    }

    [Fact]
    public void EndByDisconnect_Playing以外では何もしない()
    {
        var game = new ReversiGame();

        game.EndByDisconnect(Disc.White);

        game.Phase.ShouldBe(GamePhase.Title);
    }

    [Fact]
    public void MoveLog_リプレイ_同じログを別インスタンスに適用すると同じ盤面になる()
    {
        var recorded = new ReversiGame();
        recorded.Start(GameMode.LocalTwoPlayer);
        while (recorded.Phase == GamePhase.Playing)
        {
            var moves = recorded.Board.GetLegalMoves(recorded.CurrentPlayer);
            recorded.TryPlace(moves[0].X, moves[0].Y);
        }

        var replayed = new ReversiGame();
        replayed.Start(GameMode.LocalTwoPlayer);
        foreach (var entry in recorded.MoveLog)
        {
            if (entry.StartsWith("place"))
            {
                var parts = entry.Split(' ');
                replayed.TryPlace(int.Parse(parts[1]), int.Parse(parts[2])).ShouldBeTrue();
            }
            // pass はロジックが自動処理するため再生不要
        }

        replayed.Phase.ShouldBe(GamePhase.Result);
        replayed.Winner.ShouldBe(recorded.Winner);
        for (var y = 0; y < Board.Size; y++)
        {
            for (var x = 0; x < Board.Size; x++)
            {
                replayed.Board[x, y].ShouldBe(recorded.Board[x, y]);
            }
        }
    }
}
