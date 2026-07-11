using System.Numerics;
using Arch.Core;
using VitalRouter;

namespace ShootingGame.Logic;

/// <summary>
/// 横スクロール STG の規則エンジン(D-048)。固定タイムステップ(60Hz)の
/// Tick(InputState) でだけ状態が進む完全決定論。運動・衝突は自前の数式で、
/// Godot 物理を使わない。弾・敵は Arch の Entity、イベントは VitalRouter で流す。
/// </summary>
public sealed class ShootingLogic : IDisposable
{
    public ShootingLogic(int seed, ShootingConfig? config = null)
    {
        Seed = seed;
        Config = config ?? new ShootingConfig();
        EventLog = new EventLog(Config.EventLogCapacity);
    }

    /// <summary>生成に使ったシード。再現性検証のため State で公開する。</summary>
    public int Seed { get; }

    /// <summary>適用中のルール定数。</summary>
    public ShootingConfig Config { get; }

    /// <summary>進んだ Tick 数(60Hz)。</summary>
    public int TickCount => default;

    /// <summary>自機の位置。</summary>
    public Vector2 PlayerPosition => default;

    /// <summary>残機。</summary>
    public int Lives => default;

    /// <summary>スコア。</summary>
    public int Score => default;

    /// <summary>被弾後の無敵中か。</summary>
    public bool IsInvincible => default;

    /// <summary>残機が尽きたか。true 以降は Tick が状態を変えない(盤面凍結)。</summary>
    public bool IsGameOver => default;

    /// <summary>ゲーム内イベントの発行先。購読者(スコア係・演出係等)はここへ Subscribe する。</summary>
    public Router Router { get; } = new();

    /// <summary>全イベントの記録。State 公開・wait 条件の源。</summary>
    public EventLog EventLog { get; }

    /// <summary>場に出ている自弾。</summary>
    public IReadOnlyList<BulletSnapshot> PlayerBullets => [];

    /// <summary>場に出ている敵。</summary>
    public IReadOnlyList<EnemySnapshot> Enemies => [];

    /// <summary>1 Tick(1/60 秒)進める。運動 → 衝突 → 発射 → 寿命の順で解決する。</summary>
    public void Tick(in InputState input) { }

    /// <summary>敵を出現させる(ウェーブ生成・テスト用)。EnemySpawned を発行する。</summary>
    public int SpawnEnemy(EnemyKind kind, Vector2 position) => default;

    public void Dispose() { }
}
