namespace ShootingGame.Logic;

/// <summary>
/// 1 Tick ぶんの入力状態のスナップショット(D-048)。キー押下イベントではなく
/// 「その Tick に押されているか」の集合として論理へ渡す。
/// 記録・注入・リプレイの単位になる。
/// </summary>
public readonly record struct InputState(
    bool Left = false,
    bool Right = false,
    bool Up = false,
    bool Down = false,
    bool Shoot = false
);
