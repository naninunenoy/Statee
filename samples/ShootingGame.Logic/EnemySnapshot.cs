using System.Numerics;

namespace ShootingGame.Logic;

/// <summary>場に出ている敵のスナップショット(描画・State 公開用)。</summary>
public readonly record struct EnemySnapshot(int Id, EnemyKind Kind, Vector2 Position, int Hp);
