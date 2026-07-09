namespace RogueGame.Logic;

/// <summary>
/// ローグライクの中核状態機械。アクション列 → 状態遷移の決定論的関数(docs/adr/D-044.md)。
/// フロアは離脱後も状態を保持する(帰路の増援・覚醒のため使い捨てない)。
/// </summary>
public sealed class RogueLogic
{
    /// <summary>本番用。シードからフロアを生成する。</summary>
    public RogueLogic(int seed)
        : this(floorNumber => DungeonGenerator.Generate(seed, floorNumber)) { }

    /// <summary>フロア地形の供給元を注入する(テストで手組みマップを使うためのシーム)。</summary>
    public RogueLogic(Func<int, DungeonMap> mapFactory) { }

    /// <summary>現在のフロア番号(1 起点。地上に最も近いのが 1)。</summary>
    public int CurrentFloor => default;

    /// <summary>現在フロアの地形。</summary>
    public DungeonMap Map => default!;

    /// <summary>プレイヤーの現在位置。</summary>
    public GridPos PlayerPos => default;

    /// <summary>
    /// 指定方向へ1マス移動する。壁方向なら何も起きない。
    /// 下り階段に乗ると次フロアへ、上り階段に乗ると前フロアへ自動遷移する
    /// (フロア1の上り階段は現時点では何も起きない)。
    /// </summary>
    public void Move(Direction direction) { }
}
