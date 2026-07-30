using System.Numerics;

namespace TpsGame.Logic;

/// <summary>場に出ている弾のスナップショット(描画・State 公開用)。</summary>
public readonly record struct BulletSnapshot(int Id, Vector3 Position, float Yaw);
