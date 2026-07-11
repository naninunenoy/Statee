using Reversi.Logic;
using Shouldly;
using Syncee;
using Syncee.Fake;

namespace Reversi.Server.Tests;

public class ReversiAuthorityTest
{
    private static void Send(
        ITransport transport,
        string command,
        Dictionary<string, string>? args
    ) => transport.Send(SyncWire.Serialize(new CommandRequest(command, args)));

    [Fact]
    public void 接続する_ConnectedClientCountが増える()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new ReversiAuthority(serverTransport);

        serverTransport.Connect();
        serverTransport.Connect();

        authority.ConnectedClientCount.ShouldBe(2);
    }

    [Fact]
    public void 最初の接続がstartを送る_Playingへ遷移し全クライアントへ配布される()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new ReversiAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        var client2 = serverTransport.Connect();
        CommandEnvelope? received1 = null;
        CommandEnvelope? received2 = null;
        client1.Received += bytes => received1 = SyncWire.DeserializeEnvelope(bytes);
        client2.Received += bytes => received2 = SyncWire.DeserializeEnvelope(bytes);

        Send(client1, "start", null);

        authority.Game.Phase.ShouldBe(GamePhase.Playing);
        received1!.Command.ShouldBe("start");
        received2!.Command.ShouldBe("start");
    }

    [Fact]
    public void 黒番のclient1が着手する_盤に反映され両クライアントへ配布される()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new ReversiAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        var client2 = serverTransport.Connect();
        Send(client1, "start", null);

        CommandEnvelope? received = null;
        client2.Received += bytes => received = SyncWire.DeserializeEnvelope(bytes);
        Send(client1, "place", new Dictionary<string, string> { ["x"] = "2", ["y"] = "3" });

        authority.Game.Board[2, 3].ShouldBe(Disc.Black);
        authority.Game.CurrentPlayer.ShouldBe(Disc.White);
        received!.Command.ShouldBe("place");
    }

    [Fact]
    public void 非合法な着手_盤は変わらずCommittedCountも増えない()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new ReversiAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        serverTransport.Connect();
        Send(client1, "start", null);
        var before = authority.CommittedCount;

        Send(client1, "place", new Dictionary<string, string> { ["x"] = "0", ["y"] = "0" });

        authority.CommittedCount.ShouldBe(before);
        authority.Game.Board[0, 0].ShouldBe(Disc.None);
    }

    [Fact]
    public void 対局中に白番クライアントが切断する_黒の不戦勝でResultへ遷移する()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new ReversiAuthority(serverTransport);
        var client1 = serverTransport.Connect(); // 黒
        var client2 = serverTransport.Connect(); // 白
        Send(client1, "start", null);

        CommandEnvelope? received = null;
        client1.Received += bytes => received = SyncWire.DeserializeEnvelope(bytes);
        client2.Disconnect();

        authority.Game.Phase.ShouldBe(GamePhase.Result);
        authority.Game.Winner.ShouldBe(Disc.Black);
        authority.Game.EndReason.ShouldBe(GameEndReason.Disconnected);
        received!.Command.ShouldBe("disconnect");
        authority.ConnectedClientCount.ShouldBe(1);
    }

    [Fact]
    public void Title中の切断_対局は終了しない()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new ReversiAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        serverTransport.Connect();

        client1.Disconnect();

        authority.Game.Phase.ShouldBe(GamePhase.Title);
    }
}
