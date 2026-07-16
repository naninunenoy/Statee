using System.Numerics;

namespace MessBreak.Logic;

/// <summary>1 tick 分のプレイヤー入力。MoveDir は正規化済みでなくてよい(ロジック側で正規化)。</summary>
public readonly record struct TickInput(Vector2 MoveDir, bool Attack = false, bool Dodge = false)
{
    public static TickInput None => new(Vector2.Zero);
}
