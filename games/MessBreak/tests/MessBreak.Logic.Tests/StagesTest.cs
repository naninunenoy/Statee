using Shouldly;

namespace MessBreak.Logic.Tests;

public class StagesTest
{
    [Fact]
    public void Room1_パースでき全配置が床の上にある()
    {
        var stage = Stages.Room1();

        stage.MobSpawns.ShouldNotBeEmpty();
        stage.IsSolidAt(stage.PlayerSpawn).ShouldBeFalse();
        stage.IsSolidAt(stage.BossSpawn).ShouldBeFalse();
        stage.IsSolidAt(stage.TurretSlot).ShouldBeFalse();
        foreach (var mob in stage.MobSpawns)
        {
            stage.IsSolidAt(mob).ShouldBeFalse();
        }
    }
}
