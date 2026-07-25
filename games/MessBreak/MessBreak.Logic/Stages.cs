namespace MessBreak.Logic;

/// <summary>ゲームに同梱するステージ定義。形式は <see cref="StageMap"/> を参照。</summary>
public static class Stages
{
    /// <summary>
    /// 最初の小部屋(縦切り3-1 のミッション)。左がプレイヤー側、右が敵エリアで、
    /// 中央の壁の切れ目(下側の通路)だけで行き来する。
    /// 雑魚は敵エリアに散らして3体置く(全滅で制圧という仕様を実際のミッションで働かせるため)。
    /// </summary>
    public const string Room1Text = """
        ################
        #........#M....#
        #........#..M..#
        #........#.....#
        #...P......T...#
        #........#..B..#
        #........#...M.#
        #........#.....#
        ################
        """;

    /// <summary>Room1 のパース済み定義。</summary>
    public static StageDefinition Room1() => StageMap.Parse(Room1Text);
}
