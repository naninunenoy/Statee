using Shouldly;

namespace SuikaGame.Logic.Tests;

public class SuikaLogicResetTest
{
    private static SuikaLogic CreateLogic(SuikaConfig? config = null) => new(seed: 42, config);

    [Fact]
    public void Reset_フルーツとスコアがある状態_全消去されスコア0になる()
    {
        using var logic = CreateLogic();
        var a = logic.Spawn(FruitKind.Cherry);
        var b = logic.Spawn(FruitKind.Cherry);
        logic.ReportContact(a, b);
        logic.Score.CurrentValue.ShouldBeGreaterThan(0);

        logic.Reset();

        logic.Fruits.ShouldBeEmpty();
        logic.Score.CurrentValue.ShouldBe(0);
    }

    [Fact]
    public void Reset_ゲームオーバー後_解除され再びプレイできる()
    {
        using var logic = CreateLogic();
        var id = logic.Spawn(FruitKind.Cherry);
        logic.SetOverflowing(id, true);
        logic.Tick(10.0);
        logic.IsGameOver.CurrentValue.ShouldBeTrue();

        logic.Reset();

        logic.IsGameOver.CurrentValue.ShouldBeFalse();
        var a = logic.Spawn(FruitKind.Cherry);
        var b = logic.Spawn(FruitKind.Cherry);
        logic.ReportContact(a, b);
        logic.Score.CurrentValue.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Reset_ID採番_リセット前と重複しない()
    {
        using var logic = CreateLogic();
        var before = logic.Spawn(FruitKind.Cherry);

        logic.Reset();

        var after = logic.Spawn(FruitKind.Cherry);
        after.ShouldNotBe(before);
    }
}
