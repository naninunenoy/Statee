namespace ShootingGame.Logic;

/// <summary>敵の種類(D-048)。運動・攻撃パターンを定める。</summary>
public enum EnemyKind
{
    /// <summary>等速で左へ直進する 👾。</summary>
    Straight,

    /// <summary>サイン波で蛇行する 🛸。</summary>
    Sine,

    /// <summary>自機狙い弾を撃つ 🦑。</summary>
    Shooter,

    /// <summary>ボス 🐙。フェーズで弾幕が変わる。</summary>
    Boss,
}
