using Shouldly;

namespace TodoApp.Logic.Tests;

public class GameLogicTest
{
    [Fact]
    public void Step_1回進める_StepCountが1になる()
    {
        var game = new GameLogic(seed: 1);

        game.Step();

        game.StepCount.ShouldBe(1);
    }
}
