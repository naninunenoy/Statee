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
    public async Task Receive_ポーズ中にRestartGameCommand_Playingへ遷移しやり直し要求が通知される()
    {
        using var flow = new GameFlow();
        using var router = new GameCommandRouter(flow);
        flow.StartGame();
        flow.PauseGame();
        var restartRequested = false;
        using var subscription = router.RestartRequests.Subscribe(_ => restartRequested = true);

        await router.Router.PublishAsync(new RestartGameCommand());

        flow.Phase.CurrentValue.ShouldBe(GamePhase.Playing);
        restartRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task Receive_プレイ中のRestartGameCommand_無視されやり直し要求は通知されない()
    {
        using var flow = new GameFlow();
        using var router = new GameCommandRouter(flow);
        flow.StartGame();
        var restartRequested = false;
        using var subscription = router.RestartRequests.Subscribe(_ => restartRequested = true);

        await router.Router.PublishAsync(new RestartGameCommand());

        flow.Phase.CurrentValue.ShouldBe(GamePhase.Playing);
        restartRequested.ShouldBeFalse();
    }

    [Fact]
    public async Task Receive_プレイ中にPauseGameCommand_Pausedへ遷移する()
    {
        using var flow = new GameFlow();
        using var router = new GameCommandRouter(flow);
        flow.StartGame();

        await router.Router.PublishAsync(new PauseGameCommand());

        flow.Phase.CurrentValue.ShouldBe(GamePhase.Paused);
    }

    [Fact]
    public async Task Receive_タイトル中のPauseGameCommand_無視されTitleのまま()
    {
        using var flow = new GameFlow();
        using var router = new GameCommandRouter(flow);

        await router.Router.PublishAsync(new PauseGameCommand());

        flow.Phase.CurrentValue.ShouldBe(GamePhase.Title);
    }

    [Fact]
    public async Task Receive_ポーズ中にResumeGameCommand_Playingへ遷移する()
    {
        using var flow = new GameFlow();
        using var router = new GameCommandRouter(flow);
        flow.StartGame();
        flow.PauseGame();

        await router.Router.PublishAsync(new ResumeGameCommand());

        flow.Phase.CurrentValue.ShouldBe(GamePhase.Playing);
    }

    [Fact]
    public async Task Receive_ポーズ中にResumeGameCommand_やり直し要求は通知されない()
    {
        using var flow = new GameFlow();
        using var router = new GameCommandRouter(flow);
        flow.StartGame();
        flow.PauseGame();
        var restartRequested = false;
        using var subscription = router.RestartRequests.Subscribe(_ => restartRequested = true);

        await router.Router.PublishAsync(new ResumeGameCommand());

        restartRequested.ShouldBeFalse();
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
