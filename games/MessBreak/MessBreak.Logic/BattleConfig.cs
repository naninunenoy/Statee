namespace MessBreak.Logic;

/// <summary>
/// 戦闘のバランス値。ゲームバランスに関わる数値はここに集約し、ロジック内にハードコードしない
/// (docs/DESIGN.md)。時間はすべて tick 数(60 tick = 1 秒想定)、距離・速度はワールド単位。
/// </summary>
public sealed record BattleConfig
{
    /// <summary>1 秒あたりの tick 数。速度(毎秒)を 1 tick の移動量に換算する基準。</summary>
    public int TicksPerSecond { get; init; } = 60;

    // 部屋(原点 (0,0) 起点の矩形)
    public float RoomWidth { get; init; } = 320f;
    public float RoomHeight { get; init; } = 180f;

    // プレイヤー
    public float PlayerRadius { get; init; } = 6f;
    public float PlayerSpeed { get; init; } = 90f;

    /// <summary>スプリント(左 Shift)中の移動速度。</summary>
    public float SprintSpeed { get; init; } = 140f;
    public int PlayerMaxHp { get; init; } = 3;

    // 通常射撃(エイム方向へ弾を撃つ。低ダメージの繋ぎ)
    public int FireCooldownTicks { get; init; } = 12;
    public float BulletSpeed { get; init; } = 240f;
    public float BulletRadius { get; init; } = 2f;
    public int BulletDamage { get; init; } = 1;

    // ドッジ(向いている方向に高速移動・全区間無敵)
    public int DodgeTicks { get; init; } = 12;
    public int DodgeCooldownTicks { get; init; } = 30;
    public float DodgeSpeed { get; init; } = 240f;

    // 敵
    public float EnemyRadius { get; init; } = 8f;
    public float EnemySpeed { get; init; } = 60f;
    public int EnemyMaxHp { get; init; } = 3;
    public float EnemyAggroRange { get; init; } = 100f;
    public float EnemyAttackRange { get; init; } = 24f;
    public int EnemyWindupTicks { get; init; } = 20;
    public int EnemyRecoveryTicks { get; init; } = 30;
    public int EnemyAttackDamage { get; init; } = 1;

    // 初期配置
    public System.Numerics.Vector2 PlayerSpawn { get; init; } = new(80f, 90f);
    public System.Numerics.Vector2 EnemySpawn { get; init; } = new(240f, 90f);
}
