namespace TpsGame.Logic;

/// <summary>
/// 1 Tick ぶんの入力状態のスナップショット。キー押下イベントではなく
/// 「その Tick に押されているか」の集合として論理へ渡す。
/// </summary>
public readonly record struct InputState(
    bool Forward = false,
    bool Back = false,
    bool Left = false,
    bool Right = false,
    bool Shoot = false,
    bool Quit = false
);
