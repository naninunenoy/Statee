namespace RogueGame.Logic;

/// <summary>フロア上の敵1体。位置と HP は可変。</summary>
public sealed class Enemy
{
    public Enemy(EnemyId id, GridPos pos, int hp, int attack)
    {
        Id = id;
        Pos = pos;
        Hp = hp;
        Attack = attack;
    }

    /// <summary>安定 ID。倒されるまで変わらない。</summary>
    public EnemyId Id { get; }

    /// <summary>現在位置。</summary>
    public GridPos Pos { get; internal set; }

    /// <summary>残り HP。0 以下で倒れる。</summary>
    public int Hp { get; internal set; }

    /// <summary>攻撃力。プレイヤーへ与えるダメージ(乱数なし)。</summary>
    public int Attack { get; }
}
