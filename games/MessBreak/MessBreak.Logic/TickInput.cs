using System.Numerics;

namespace MessBreak.Logic;

/// <summary>
/// 1 tick 分のプレイヤー入力。ツインスティック型: MoveDir(移動)と AimDir(照準)は独立で、
/// どちらも正規化済みでなくてよい(ロジック側で正規化)。AimDir が零なら照準は前回を維持する。
/// AimPoint はレティクルの絶対位置(あれば)。スキルの爆心指定に使い、
/// ないデバイス(ゲームパッド等)では null のまま=向いている方向へのフォールバックになる。
/// </summary>
public readonly record struct TickInput(
    Vector2 MoveDir = default,
    Vector2 AimDir = default,
    bool Fire = false,
    bool Dodge = false,
    bool Sprint = false,
    bool Skill = false,
    Vector2? AimPoint = null
)
{
    public static TickInput None => new();
}
