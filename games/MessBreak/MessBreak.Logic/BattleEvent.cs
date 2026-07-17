namespace MessBreak.Logic;

public enum BattleEventKind
{
    /// <summary>弾を発射した(Pos = 発射位置)。</summary>
    BulletFired,

    /// <summary>弾が的に命中した(Pos = 命中位置)。</summary>
    TargetHit,

    /// <summary>的を撃破した(Pos = 的の位置)。</summary>
    TargetKilled,
}

/// <summary>
/// その tick に起きた出来事。ロジックが「何が起きたか」を決め、Godot 層が音・エフェクトに
/// 翻訳する(効果音の再生自体は表現なので Godot 層の責務)。状態の差分から推測させない。
/// </summary>
public readonly record struct BattleEvent(BattleEventKind Kind, System.Numerics.Vector2 Pos);
