namespace ShootingGame.Logic;

/// <summary>
/// ボス 🐙 の定数。全ウェーブクリア後に右端から入場し、アンカー X で停止して
/// 上下に蛇行しながらフェーズ(残 HP)に応じた弾幕を撃つ。撃破でゲームクリア。
/// </summary>
public sealed record BossConfig
{
    /// <summary>最大 HP。2/3 以下でフェーズ2、1/3 以下でフェーズ3になる。</summary>
    public int Hp { get; init; } = 50;

    /// <summary>入場後に停止する X。</summary>
    public float AnchorX { get; init; } = 820f;

    /// <summary>当たり判定半径(通常敵の EnemyRadius より大きい)。</summary>
    public float Radius { get; init; } = 40f;

    /// <summary>入場時の速度(px/Tick。左向き)。</summary>
    public float EntrySpeed { get; init; } = 2f;

    /// <summary>弾幕の発射間隔(Tick)。</summary>
    public int FireIntervalTicks { get; init; } = 45;

    /// <summary>停止後の上下蛇行の振幅(px)。</summary>
    public float SineAmplitude { get; init; } = 150f;

    /// <summary>上下蛇行の周期(Tick)。</summary>
    public int SinePeriodTicks { get; init; } = 240;

    /// <summary>撃破スコア。</summary>
    public int Score { get; init; } = 1000;
}
