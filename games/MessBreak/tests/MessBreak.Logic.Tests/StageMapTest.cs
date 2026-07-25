using System.Numerics;
using Shouldly;

namespace MessBreak.Logic.Tests;

public class StageMapTest
{
    /// <summary>5x5・タイル 40 の最小ステージ。中央 (2,2) に壁、各マーカーを1つずつ配置。</summary>
    private const string Small = """
        #####
        #P.M#
        #.#B#
        #T..#
        #####
        """;

    // ---- 形状 ----

    [Fact]
    public void Parse_サイズ_セル数とタイルサイズの積になる()
    {
        var stage = StageMap.Parse(Small);

        stage.Width.ShouldBe(200f);
        stage.Height.ShouldBe(200f);
    }

    [Theory]
    [InlineData(0, 0, true)] // 外周の壁
    [InlineData(2, 2, true)] // 内部の壁
    [InlineData(1, 1, false)] // マーカー(P)のセルは床
    [InlineData(2, 1, false)] // 床
    public void Parse_形状_壁セルと床セルを判定できる(int col, int row, bool solid)
    {
        var stage = StageMap.Parse(Small);

        stage.IsSolidCell(col, row).ShouldBe(solid);
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(5, 2)]
    [InlineData(2, -1)]
    [InlineData(2, 5)]
    public void Parse_グリッド範囲外のセル_壁扱いになる(int col, int row)
    {
        var stage = StageMap.Parse(Small);

        stage.IsSolidCell(col, row).ShouldBeTrue();
    }

    // ---- 配置マーカー ----

    [Fact]
    public void Parse_マーカー_セル中心のワールド座標になる()
    {
        var stage = StageMap.Parse(Small);

        stage.PlayerSpawn.ShouldBe(new Vector2(60f, 60f));
        stage.MobSpawns.ShouldBe([new Vector2(140f, 60f)]);
        stage.BossSpawn.ShouldBe(new Vector2(140f, 100f));
        stage.TurretSlot.ShouldBe(new Vector2(60f, 140f));
    }

    [Fact]
    public void Parse_タイルサイズ指定_座標とサイズに反映される()
    {
        var stage = StageMap.Parse(Small, tileSize: 10f);

        stage.Width.ShouldBe(50f);
        stage.PlayerSpawn.ShouldBe(new Vector2(15f, 15f));
    }

    [Fact]
    public void Parse_雑魚マーカーが複数_すべて拾う()
    {
        var stage = StageMap.Parse("""
            M.M
            PBT
            """);

        stage.MobSpawns.ShouldBe([new Vector2(20f, 20f), new Vector2(100f, 20f)]);
    }

    [Fact]
    public void Parse_雑魚マーカーなし_MobSpawnsは空()
    {
        var stage = StageMap.Parse("""
            P.
            BT
            """);

        stage.MobSpawns.ShouldBeEmpty();
    }

    // ---- 不正なテキスト ----

    [Theory]
    [InlineData("#P\n#", "行長の不揃い")]
    [InlineData("PX\nBT", "未知の文字")]
    [InlineData("..\nBT", "Pなし")]
    [InlineData("PP\nBT", "Pが複数")]
    [InlineData("P.\n.T", "Bなし")]
    [InlineData("P.\nB.", "Tなし")]
    public void Parse_不正なテキスト_StageMapExceptionになる(string text, string reason)
    {
        _ = reason;

        Should.Throw<StageMapException>(() => StageMap.Parse(text));
    }

    // ---- ワールド座標での判定 ----

    [Theory]
    [InlineData(100f, 100f, true)] // 壁セル (2,2) の中
    [InlineData(60f, 60f, false)] // 床セルの中
    [InlineData(-5f, 100f, true)] // グリッド範囲外は壁
    public void IsSolidAt_点の壁判定(float x, float y, bool solid)
    {
        var stage = StageMap.Parse(Small);

        stage.IsSolidAt(new Vector2(x, y)).ShouldBe(solid);
    }

    [Fact]
    public void OverlapsSolid_円が壁セルに食い込む_trueになる()
    {
        var stage = StageMap.Parse(Small); // 壁セル (2,2) = x 80..120, y 80..120

        stage.OverlapsSolid(new Vector2(70f, 100f), 10.5f).ShouldBeTrue();
    }

    [Fact]
    public void OverlapsSolid_円が壁面に接するだけ_falseになる()
    {
        var stage = StageMap.Parse(Small);

        stage.OverlapsSolid(new Vector2(70f, 100f), 10f).ShouldBeFalse();
    }

    // ---- 直接構築(テスト・手続き生成用の経路) ----

    [Fact]
    public void 直接構築_テキストを介さずに定義できる()
    {
        var stage = new StageDefinition
        {
            Rows = ["....", "..#.", "...."],
            PlayerSpawn = new Vector2(20f, 20f),
            BossSpawn = new Vector2(140f, 20f),
            TurretSlot = new Vector2(100f, 100f),
        };

        stage.Width.ShouldBe(160f);
        stage.Height.ShouldBe(120f);
        stage.IsSolidCell(2, 1).ShouldBeTrue();
        stage.IsSolidCell(0, 0).ShouldBeFalse();
        stage.IsSolidCell(-1, 0).ShouldBeTrue();
    }
}
