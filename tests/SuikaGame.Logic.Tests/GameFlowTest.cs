using Shouldly;

namespace SuikaGame.Logic.Tests;

public class GameFlowTest
{
    // --- 初期状態 ---

    [Fact]
    public void Phase_初期状態_タイトルである()
    {
        using var flow = new GameFlow();

        flow.Phase.CurrentValue.ShouldBe(GamePhase.Title);
    }

    // --- 遷移(0-switch 全カバー) ---

    [Fact]
    public void StartGame_タイトル中_Playingへ遷移してtrueを返す()
    {
        using var flow = new GameFlow();

        var transitioned = flow.StartGame();

        transitioned.ShouldBeTrue();
        flow.Phase.CurrentValue.ShouldBe(GamePhase.Playing);
    }

    [Fact]
    public void StartGame_プレイ中_無視されfalseを返す()
    {
        using var flow = new GameFlow();
        flow.StartGame();

        var transitioned = flow.StartGame();

        transitioned.ShouldBeFalse();
        flow.Phase.CurrentValue.ShouldBe(GamePhase.Playing);
    }

    [Fact]
    public void PauseGame_プレイ中_Pausedへ遷移してtrueを返す()
    {
        using var flow = new GameFlow();
        flow.StartGame();

        var transitioned = flow.PauseGame();

        transitioned.ShouldBeTrue();
        flow.Phase.CurrentValue.ShouldBe(GamePhase.Paused);
    }

    [Fact]
    public void PauseGame_タイトル中_無視されfalseを返す()
    {
        using var flow = new GameFlow();

        var transitioned = flow.PauseGame();

        transitioned.ShouldBeFalse();
        flow.Phase.CurrentValue.ShouldBe(GamePhase.Title);
    }

    [Fact]
    public void RestartGame_ポーズ中_Playingへ遷移してtrueを返す()
    {
        using var flow = new GameFlow();
        flow.StartGame();
        flow.PauseGame();

        var transitioned = flow.RestartGame();

        transitioned.ShouldBeTrue();
        flow.Phase.CurrentValue.ShouldBe(GamePhase.Playing);
    }

    [Fact]
    public void RestartGame_プレイ中_無視されfalseを返す()
    {
        using var flow = new GameFlow();
        flow.StartGame();

        var transitioned = flow.RestartGame();

        transitioned.ShouldBeFalse();
        flow.Phase.CurrentValue.ShouldBe(GamePhase.Playing);
    }
}
