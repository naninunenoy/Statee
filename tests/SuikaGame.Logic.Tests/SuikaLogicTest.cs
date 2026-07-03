using R3;
using Shouldly;

namespace SuikaGame.Logic.Tests;

public class SuikaLogicTest
{
    private static SuikaLogic CreateLogic(SuikaConfig? config = null) => new(seed: 42, config);

    // --- 抽選(next キュー) ---

    [Fact]
    public void PeekNext_消費しない_何度呼んでも同じ種類を返す()
    {
        using var logic = CreateLogic();

        var first = logic.PeekNext();
        logic.PeekNext().ShouldBe(first);
    }

    [Fact]
    public void PeekNext_初期候補_落下候補は先頭5種のいずれか()
    {
        using var logic = CreateLogic();

        for (var i = 0; i < 100; i++)
        {
            ((int)logic.PeekNext()).ShouldBeLessThan((int)FruitKind.Apple);
            logic.SpawnNext();
        }
    }

    [Fact]
    public void SpawnNext_同じシード_同じ種類の系列になる()
    {
        using var a = new SuikaLogic(seed: 7);
        using var b = new SuikaLogic(seed: 7);

        for (var i = 0; i < 20; i++)
        {
            var expected = a.PeekNext();
            b.PeekNext().ShouldBe(expected);
            a.SpawnNext();
            b.SpawnNext();
        }
    }

    [Fact]
    public void SpawnNext_呼び出し_PeekNextの種類で場に追加されキューが進む()
    {
        using var logic = CreateLogic();

        var next = logic.PeekNext();
        var id = logic.SpawnNext();

        logic.Fruits.ShouldContain(new FruitSnapshot(id, next));
    }

    // --- 生成と ID ---

    [Fact]
    public void Spawn_種類を指定_その種類で場に追加される()
    {
        using var logic = CreateLogic();

        var id = logic.Spawn(FruitKind.Melon);

        logic.Fruits.ShouldBe([new FruitSnapshot(id, FruitKind.Melon)]);
    }

    [Fact]
    public void Spawn_連続生成_IDは重複しない()
    {
        using var logic = CreateLogic();

        var ids = Enumerable.Range(0, 50).Select(_ => logic.Spawn(FruitKind.Cherry)).ToList();

        ids.Distinct().Count().ShouldBe(50);
    }

    // --- 合体 ---

    [Fact]
    public void ReportContact_同種の接触_一段大きい種に合体する()
    {
        using var logic = CreateLogic();
        var a = logic.Spawn(FruitKind.Cherry);
        var b = logic.Spawn(FruitKind.Cherry);

        logic.ReportContact(a, b);

        var fruit = logic.Fruits.ShouldHaveSingleItem();
        fruit.Kind.ShouldBe(FruitKind.Strawberry);
        fruit.Id.ShouldNotBe(a);
        fruit.Id.ShouldNotBe(b);
    }

    [Fact]
    public void ReportContact_同種の接触_結果種の三角数がスコアに加算される()
    {
        using var logic = CreateLogic();

        // Cherry+Cherry → Strawberry(1点)、Melon+Melon → Watermelon(55点)
        logic.ReportContact(logic.Spawn(FruitKind.Cherry), logic.Spawn(FruitKind.Cherry));
        logic.Score.CurrentValue.ShouldBe(1);

        logic.ReportContact(logic.Spawn(FruitKind.Melon), logic.Spawn(FruitKind.Melon));
        logic.Score.CurrentValue.ShouldBe(1 + 55);
    }

    [Fact]
    public void ReportContact_異種の接触_何も起きない()
    {
        using var logic = CreateLogic();
        var a = logic.Spawn(FruitKind.Cherry);
        var b = logic.Spawn(FruitKind.Grape);

        logic.ReportContact(a, b);

        logic.Fruits.Count.ShouldBe(2);
        logic.Score.CurrentValue.ShouldBe(0);
    }

    [Fact]
    public void ReportContact_スイカ同士_両方消えて新フルーツは生まれない()
    {
        using var logic = CreateLogic();
        var a = logic.Spawn(FruitKind.Watermelon);
        var b = logic.Spawn(FruitKind.Watermelon);

        logic.ReportContact(a, b);

        logic.Fruits.ShouldBeEmpty();
        logic.Score.CurrentValue.ShouldBe(66);
    }

    [Fact]
    public void ReportContact_未知または消滅済みのID_無視される()
    {
        using var logic = CreateLogic();
        var a = logic.Spawn(FruitKind.Cherry);
        var b = logic.Spawn(FruitKind.Cherry);
        logic.ReportContact(a, b);

        // a, b は合体で消滅済み。同じ報告が物理から重複して届いても何も起きない
        logic.ReportContact(a, b);

        logic.Fruits.Count.ShouldBe(1);
        logic.Score.CurrentValue.ShouldBe(1);
    }

    [Fact]
    public void ReportContact_同一IDどうし_無視される()
    {
        using var logic = CreateLogic();
        var a = logic.Spawn(FruitKind.Cherry);

        logic.ReportContact(a, a);

        logic.Fruits.Count.ShouldBe(1);
        logic.Score.CurrentValue.ShouldBe(0);
    }

    [Fact]
    public void Merges_合体発生_削除IDと生成IDを載せたイベントが発行される()
    {
        using var logic = CreateLogic();
        var a = logic.Spawn(FruitKind.Cherry);
        var b = logic.Spawn(FruitKind.Cherry);
        MergeEvent? received = null;
        using var subscription = logic.Merges.Subscribe(e => received = e);

        logic.ReportContact(a, b);

        received.ShouldNotBeNull();
        var merge = received.Value;
        new[] { merge.RemovedA, merge.RemovedB }.ShouldBe([a, b], ignoreOrder: true);
        merge.CreatedKind.ShouldBe(FruitKind.Strawberry);
        merge.Created.ShouldBe(logic.Fruits.Single().Id);
    }

    // --- ゲームオーバー ---

    [Fact]
    public void Tick_溢れ状態が猶予時間連続_ゲームオーバーになる()
    {
        using var logic = CreateLogic(new SuikaConfig { OverflowGraceSeconds = 1.0 });
        var id = logic.Spawn(FruitKind.Cherry);
        logic.SetOverflowing(id, true);

        logic.Tick(0.6);
        logic.IsGameOver.CurrentValue.ShouldBeFalse();
        logic.Tick(0.6);

        logic.IsGameOver.CurrentValue.ShouldBeTrue();
    }

    [Fact]
    public void Tick_猶予時間内に溢れが解消_ゲームオーバーにならない()
    {
        using var logic = CreateLogic(new SuikaConfig { OverflowGraceSeconds = 1.0 });
        var id = logic.Spawn(FruitKind.Cherry);
        logic.SetOverflowing(id, true);
        logic.Tick(0.9);

        // 解消するとタイマーはリセットされ、再度溢れても猶予は最初から
        logic.SetOverflowing(id, false);
        logic.SetOverflowing(id, true);
        logic.Tick(0.9);

        logic.IsGameOver.CurrentValue.ShouldBeFalse();
    }

    [Fact]
    public void Tick_溢れたフルーツが合体で消滅_ゲームオーバーにならない()
    {
        using var logic = CreateLogic(new SuikaConfig { OverflowGraceSeconds = 1.0 });
        var a = logic.Spawn(FruitKind.Cherry);
        var b = logic.Spawn(FruitKind.Cherry);
        logic.SetOverflowing(a, true);
        logic.Tick(0.9);

        logic.ReportContact(a, b);
        logic.Tick(0.9);

        logic.IsGameOver.CurrentValue.ShouldBeFalse();
    }

    [Fact]
    public void ReportContact_ゲームオーバー後_盤面が凍結して無視される()
    {
        using var logic = CreateLogic(new SuikaConfig { OverflowGraceSeconds = 1.0 });
        var a = logic.Spawn(FruitKind.Cherry);
        var b = logic.Spawn(FruitKind.Cherry);
        logic.SetOverflowing(a, true);
        logic.Tick(1.0);
        logic.IsGameOver.CurrentValue.ShouldBeTrue();

        logic.ReportContact(a, b);

        logic.Fruits.Count.ShouldBe(2);
        logic.Score.CurrentValue.ShouldBe(0);
    }
}
