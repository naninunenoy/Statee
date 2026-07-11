using Shouldly;

namespace ShootingGame.Logic.Tests;

/// <summary>テスト用の Tick 進行ヘルパ。</summary>
public static class ShootingLogicTestExtensions
{
    public static void TickMany(this ShootingLogic logic, int count, InputState input = default)
    {
        for (var i = 0; i < count; i++)
        {
            logic.Tick(input);
        }
    }

    /// <summary>条件成立まで Tick を進める(上限つき。固定 Tick 数への依存を避ける)。</summary>
    public static void TickUntil(
        this ShootingLogic logic,
        Func<ShootingLogic, bool> condition,
        InputState input = default,
        int maxTicks = 2000
    )
    {
        for (var i = 0; i < maxTicks; i++)
        {
            if (condition(logic))
            {
                return;
            }
            logic.Tick(input);
        }
        condition(logic).ShouldBeTrue($"{maxTicks} Tick 以内に条件が成立しなかった");
    }
}
