using System.Numerics;

namespace ShootingGame.Logic;

/// <summary>場に出ているアイテム ⭐ のスナップショット(描画・State 公開用)。</summary>
public readonly record struct ItemSnapshot(int Id, Vector2 Position);
