namespace MessBreak.Logic;

/// <summary>
/// キャラクター1人ぶんのバランス値。キャラ切り替え=スキルセット差し替えという
/// コンセプトに合わせ、スキルまわりの数値はキャラごとに独立して調整できるようにする。
/// スキルの効果種別(ダメージ/デバフ)は CharacterId で決まり、ここは数値だけを持つ。
/// </summary>
public sealed record CharacterConfig
{
    public int SkillCooldownTicks { get; init; } = 180;

    /// <summary>スキル爆心のプレイヤーからの最大距離。</summary>
    public float SkillRange { get; init; } = 80f;

    /// <summary>スキル効果の半径。</summary>
    public float SkillRadius { get; init; } = 40f;

    /// <summary>アタッカーのスキルダメージ(デバッファーでは未使用)。</summary>
    public int SkillDamage { get; init; } = 3;

    /// <summary>デバフの持続 tick 数(アタッカーでは未使用)。</summary>
    public int DebuffDurationTicks { get; init; } = 300;

    /// <summary>デバフ中の被ダメージ倍率(アタッカーでは未使用)。</summary>
    public int DebuffDamageMultiplier { get; init; } = 2;
}
