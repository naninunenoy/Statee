using System.Numerics;

namespace TpsGame.Logic;

/// <summary>的のスナップショット(描画・State 公開用)。</summary>
public readonly record struct TargetSnapshot(
    int Id,
    Vector3 Position,
    bool Alive,
    int RespawnTicksLeft
);
