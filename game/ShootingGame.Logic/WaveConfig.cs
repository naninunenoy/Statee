namespace ShootingGame.Logic;

/// <summary>
/// 1ウェーブの構成。出現タイミング・Y 位置・種類の抽選はシード由来の乱数で行い、
/// 同一シードなら同一スケジュールになる(D-048)。
/// </summary>
/// <param name="EnemyCount">このウェーブで湧く敵の数。</param>
/// <param name="Kinds">抽選対象の敵種(一様抽選)。</param>
public sealed record WaveConfig(int EnemyCount, EnemyKind[] Kinds);
