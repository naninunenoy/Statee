namespace ShootingGame.Logic;

/// <summary>
/// ゲームルールの定数。論理座標系はビューポートと同じ 960x540(左上原点・Y 下向き)。
/// 速度はすべて「1 Tick(1/60 秒)あたりのピクセル数」で表す。
/// </summary>
public sealed record ShootingConfig
{
    /// <summary>フィールド幅(論理座標)。</summary>
    public float FieldWidth { get; init; } = 960f;

    /// <summary>フィールド高さ(論理座標)。</summary>
    public float FieldHeight { get; init; } = 540f;

    /// <summary>自機の移動速度(px/Tick)。</summary>
    public float PlayerSpeed { get; init; } = 4f;

    /// <summary>自機の当たり判定半径。見た目より小さめにする(D-048)。</summary>
    public float PlayerRadius { get; init; } = 10f;

    /// <summary>自機の初期位置 X。</summary>
    public float PlayerStartX { get; init; } = 120f;

    /// <summary>自弾の速度(px/Tick。右向き)。</summary>
    public float PlayerBulletSpeed { get; init; } = 10f;

    /// <summary>自弾の当たり判定半径。</summary>
    public float PlayerBulletRadius { get; init; } = 4f;

    /// <summary>ショット押しっぱなし時の発射間隔(Tick)。</summary>
    public int FireIntervalTicks { get; init; } = 6;

    /// <summary>直進敵 👾 の速度(px/Tick。左向き)。</summary>
    public float StraightEnemySpeed { get; init; } = 2f;

    /// <summary>サイン波敵 🛸 の X 速度(px/Tick。左向き)。</summary>
    public float SineEnemySpeed { get; init; } = 2f;

    /// <summary>サイン波敵 🛸 の振幅(px)。</summary>
    public float SineAmplitude { get; init; } = 80f;

    /// <summary>サイン波敵 🛸 の周期(Tick)。</summary>
    public int SinePeriodTicks { get; init; } = 120;

    /// <summary>シューター敵 🦑 の X 速度(px/Tick。左向き)。</summary>
    public float ShooterEnemySpeed { get; init; } = 1f;

    /// <summary>シューター敵 🦑 の発射間隔(Tick)。出現から間隔経過ごとに1発。</summary>
    public int ShooterFireIntervalTicks { get; init; } = 90;

    /// <summary>敵弾 🔴 の速度(px/Tick。発射時の自機方向へ等速直進、ホーミングしない)。</summary>
    public float EnemyBulletSpeed { get; init; } = 4f;

    /// <summary>敵弾 🔴 の当たり判定半径。</summary>
    public float EnemyBulletRadius { get; init; } = 5f;

    /// <summary>敵の当たり判定半径。</summary>
    public float EnemyRadius { get; init; } = 14f;

    /// <summary>敵撃破1体のスコア。</summary>
    public int EnemyScore { get; init; } = 100;

    /// <summary>初期残機。</summary>
    public int InitialLives { get; init; } = 3;

    /// <summary>被弾後の無敵時間(Tick)。</summary>
    public int InvincibleTicks { get; init; } = 90;

    /// <summary>イベントログの保持件数(リングバッファ)。</summary>
    public int EventLogCapacity { get; init; } = 256;

    /// <summary>ウェーブ構成(先頭から順に進む)。空ならウェーブ進行なし(テスト用)。</summary>
    public IReadOnlyList<WaveConfig> Waves { get; init; } =
    [
        new(8, [EnemyKind.Straight]),
        new(8, [EnemyKind.Straight, EnemyKind.Sine]),
        new(10, [EnemyKind.Straight, EnemyKind.Sine, EnemyKind.Shooter]),
    ];

    /// <summary>ウェーブ内の敵の基本出現間隔(Tick)。これに 0〜SpawnJitterTicks の乱数が足される。</summary>
    public int SpawnIntervalTicks { get; init; } = 40;

    /// <summary>出現間隔に足される乱数の上限(Tick。この値は含まない)。</summary>
    public int SpawnJitterTicks { get; init; } = 30;

    /// <summary>敵の出現 Y のフィールド上下端からのマージン(px)。</summary>
    public float SpawnMarginY { get; init; } = 60f;
}
