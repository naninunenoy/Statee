namespace RogueGame.Logic;

/// <summary>ゲームバランスとダンジョン生成のパラメータ。</summary>
public static class RogueConfig
{
    /// <summary>マップの幅(マス数)。</summary>
    public const int MapWidth = 40;

    /// <summary>マップの高さ(マス数)。</summary>
    public const int MapHeight = 24;

    /// <summary>フロア数。最下層(この値)に 💎 がある。</summary>
    public const int FloorCount = 5;

    /// <summary>フロアあたりの敵の数(フロア番号に比例して増える)。</summary>
    public static int EnemyCount(int floorNumber) => 2 + floorNumber;

    /// <summary>敵の初期 HP。</summary>
    public const int EnemyHp = 3;

    /// <summary>敵の攻撃力。</summary>
    public const int EnemyAttack = 1;
}
