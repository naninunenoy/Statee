namespace MessBreak.Logic;

public enum BattleEventKind
{
    /// <summary>弾を発射した(Pos = 発射位置)。</summary>
    BulletFired,

    /// <summary>弾が的に命中した(Pos = 命中位置)。</summary>
    TargetHit,

    /// <summary>的を撃破した(Pos = 的の位置)。</summary>
    TargetKilled,

    /// <summary>スキルの範囲爆発が発生した(Pos = 爆発中心)。</summary>
    SkillBurst,

    /// <summary>キャラクターを切り替えた(Pos = プレイヤー位置)。</summary>
    CharacterSwitched,

    /// <summary>的にデバフ(被ダメージ増幅)が付与された(Pos = 的の位置)。</summary>
    TargetDebuffed,

    /// <summary>弾が敵に命中した(Pos = 命中位置)。</summary>
    EnemyHit,

    /// <summary>敵を撃破した(Pos = 敵の位置)。</summary>
    EnemyKilled,

    /// <summary>敵にデバフ(被ダメージ増幅)が付与された(Pos = 敵の位置)。</summary>
    EnemyDebuffed,

    /// <summary>雑魚を倒して敵エリアを制圧した=設置スロット解放(Pos = 雑魚の位置)。</summary>
    ZoneCaptured,

    /// <summary>タレットを設置した(Pos = スロット位置)。</summary>
    TurretPlaced,

    /// <summary>タレットが弾を発射した(Pos = タレット位置)。</summary>
    TurretFired,

    /// <summary>出現ポイントのアトラクトで強敵が出現した(Pos = 出現位置)。</summary>
    BossAppeared,

    /// <summary>強敵を撃破してミッション達成(Pos = 強敵の位置)。</summary>
    MissionCleared,
}

/// <summary>
/// その tick に起きた出来事。ロジックが「何が起きたか」を決め、Godot 層が音・エフェクトに
/// 翻訳する(効果音の再生自体は表現なので Godot 層の責務)。状態の差分から推測させない。
/// </summary>
public readonly record struct BattleEvent(BattleEventKind Kind, System.Numerics.Vector2 Pos);
