using R3;
using Shouldly;

namespace SuikaGame.Logic.Tests;

public class GameCommandRouterTest
{
    [Fact]
    public async Task Receive_タイトル中にStartGameCommand_Playingへ遷移する()
    {
        using var flow = new GameFlow();
        using var router = new GameCommandRouter(flow);

        await router.Router.PublishAsync(new StartGameCommand());

        flow.Phase.CurrentValue.ShouldBe(GamePhase.Playing);
    }

    [Fact]
    public async Task Receive_プレイ中のStartGameCommand_無視されPlayingのまま()
    {
        using var flow = new GameFlow();
        using var router = new GameCommandRouter(flow);
        await router.Router.PublishAsync(new StartGameCommand());

        await router.Router.PublishAsync(new StartGameCommand());

        flow.Phase.CurrentValue.ShouldBe(GamePhase.Playing);
    }

    [Fact]
    public async Task Receive_ExitGameCommand_終了要求が通知される()
    {
        using var flow = new GameFlow();
        using var router = new GameCommandRouter(flow);
        var exitRequested = false;
        using var subscription = router.ExitRequests.Subscribe(_ => exitRequested = true);

        await router.Router.PublishAsync(new ExitGameCommand());

        exitRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task Receive_ExitGameCommand_フェーズは変わらない()
    {
        using var flow = new GameFlow();
        using var router = new GameCommandRouter(flow);

        await router.Router.PublishAsync(new ExitGameCommand());

        flow.Phase.CurrentValue.ShouldBe(GamePhase.Title);
    }
}
