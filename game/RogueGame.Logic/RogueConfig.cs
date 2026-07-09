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

    /// <summary>プレイヤーの初期 HP。</summary>
    public const int PlayerHp = 10;

    /// <summary>プレイヤーの初期攻撃力。敵へ与えるダメージ(乱数なし)。</summary>
    public const int PlayerAttack = 2;

    /// <summary>ポーションの回復量。初期 HP を超えては回復しない。</summary>
    public const int PotionHeal = 5;

    /// <summary>剣を拾ったときの攻撃力の上昇量。</summary>
    public const int SwordAttackBonus = 1;

    /// <summary>フロアあたりのポーションの数。</summary>
    public const int PotionsPerFloor = 1;

    /// <summary>剣が落ちているフロア。</summary>
    public const int SwordFloor = 2;
}
