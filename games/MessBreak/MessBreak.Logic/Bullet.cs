using System.Numerics;

namespace MessBreak.Logic;

/// <summary>
/// プレイヤーの弾。Id はフレームを跨いで安定な一意値(docs/GUIDELINE.md 3.4)。
/// Dir は正規化済みの進行方向。
/// </summary>
public readonly record struct Bullet(int Id, Vector2 Pos, Vector2 Dir);
